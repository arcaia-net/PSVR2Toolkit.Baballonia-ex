# PSVR2Toolkit.Baballonia 調査結果

調査日: 2026-06-22

## 概要

`PSVR2Toolkit.Baballonia` は PSVR2 のアイトラッキングカメラ映像を Baballonia に提供する非公式モジュール。
Baballonia のプラグインアーキテクチャ (`ICaptureFactory` / `Capture`) に準拠して実装されている。

---

## ビルド結果

```
dotnet build PSVR2Toolkit.Baballonia.sln
→ ビルドに成功。警告 0 個、エラー 0 個
```

ビルド成功の前提条件:
- 隣のディレクトリ `../Baballonia/` に Baballonia リポジトリが存在すること（確認済み: `C:\Users\arcaia\Documents\GitHub\Baballonia`）
- `.csproj` が `ProjectReference` で `Baballonia.SDK` をローカルパス参照しているため、Baballonia リポジトリがないとビルド不可

---

## アーキテクチャ

### Baballonia のモジュールシステム

`Baballonia.Desktop/Captures/DesktopConnector.cs` が起動時に以下を行う:

1. `AppContext.BaseDirectory/Modules/` 内の `*.dll` をすべてロード
2. 各 DLL をスキャンして `ICaptureFactory` を実装する型を探す
3. `ActivatorUtilities.CreateInstance` でインスタンス生成（DI 注入対応）

つまり **このモジュールは `Modules/` フォルダに DLL を置くだけで認識される**。

### このモジュールの構成

| ファイル | 役割 |
|---|---|
| `CAPI.cs` | `PSVR2Toolkit.CAPI.dll`（ネイティブ C++ DLL）への P/Invoke |
| `Vr2Capture.cs` | `Capture` 抽象クラスを継承。バックグラウンドループで映像フレームを取得 |
| `Vr2CaptureFactory.cs` | `ICaptureFactory` を実装。プロバイダー名 `"PlayStation VR2"` を返す |

### 映像フォーマット

- サイズ: 400×200 px
- フォーマット: BC4（1チャンネル グレースケール）
- バッファ先頭 256 バイト（`0x100`）はヘッダー（先頭 2 バイトが `VI` = `0x56 0x49` のマジックナンバー）
- ネイティブ DLL から 1 回の呼び出しで `0x200100` バイト（約 2 MB）のバッファに格納される

---

## 現状でそのまま使えるか

**ビルドは可能、そのまま動作させるには問題がある。**

### 問題 1: デプロイ手順が手動（重大）

`.csproj` にビルド後の `Modules/` コピーステップがない。  
Baballonia 本体の `Baballonia.Desktop.csproj` には他のモジュール用の `CopyModulesToFolder` ターゲットがあるが、このモジュール用のものは存在しない。

**必要な手動作業:**
1. `bin/Debug/net10.0/PSVR2Toolkit.Baballonia.dll` を Baballonia の `Modules/` フォルダにコピー
2. `PSVR2Toolkit.CAPI.dll`（ネイティブ DLL）も同じ `Modules/` フォルダにコピー

### 問題 2: `CanConnect()` が常に `true` を返す（バグ・TODO）

```csharp
// Vr2CaptureFactory.cs
public bool CanConnect(string address)
{
    // TODO
    return true;
}
```

Baballonia はカメラアドレスに対してすべてのファクトリーの `CanConnect()` を呼び、最初に `true` を返したものを使う。  
常に `true` を返すため、**PSVR2 以外のカメラに対してもこのファクトリーが選ばれてしまう可能性がある**。

### 問題 3: `TaskCanceledException` がキャッチされない（マイナーバグ）

```csharp
// Vr2Capture.cs
// catch (TaskCanceledException)
// {
//     return;
// }
catch (Exception e)
{
    SetRawMat(new Mat());
    IsReady = false;
    Logger.LogError(e.ToString()); // ← 正常終了なのにエラーログが出る
    break;
}
```

`StopCapture()` でキャンセルしたとき `TaskCanceledException` がコメントアウトされたキャッチに引っかからず、汎用 `Exception` ハンドラーに落ちる。  
結果として **正常なキャプチャ停止なのにエラーログが記録され、`IsReady = false` になる**。

### 問題 4: 映像フォーマットの不一致（要確認）

- このモジュールが生成する `Mat`: `CV_8UC1`（1 チャンネル・グレースケール）
- Baballonia `Capture.cs` の仕様コメント: `"Will be dimension in BGR color space"`

Baballonia の推論パイプラインが BGR 3 チャンネル画像を期待している場合、推論が失敗する。  
ただし、Baballonia が映像チャンネルに寛容な実装であれば問題ない可能性もあり、**実際に推論側のコードを確認する必要がある**。

---

## 修正の必要性と優先度

| # | 問題 | 優先度 | 修正概要 |
|---|---|---|---|
| 1 | `CanConnect()` が常に `true` | **高** | PSVR2 特有のデバイスパスやアドレス形式で判定するロジックを実装 |
| 2 | `TaskCanceledException` 未キャッチ | **中** | コメントアウトを外して正常終了時は静かに返す |
| 3 | `Modules/` への自動コピーなし | **中** | `.csproj` にビルド後コピーターゲットを追加（または README に明記） |
| 4 | 映像フォーマット（グレースケール vs BGR） | **要確認** | Baballonia 推論側を確認し、必要なら `CvtColor` で変換 |

---

## 修正手順（フォークする場合）

### ステップ 1: `CanConnect()` を実装する

PSVR2 固有の識別子でフィルタリングする。例えば:
- 専用のアドレス形式（例: `psvr2://` スキームや固定の文字列）を定義
- または空文字・null のときだけ接続可とし、他のアドレスには `false` を返す

```csharp
public bool CanConnect(string address)
{
    // 例: 空アドレスのみ PSVR2 を使うルールにする
    return string.IsNullOrEmpty(address);
}
```

### ステップ 2: `TaskCanceledException` を正しくキャッチする

```csharp
catch (TaskCanceledException)
{
    return; // 正常なキャンセル、ログ不要
}
catch (Exception e)
{
    SetRawMat(new Mat());
    IsReady = false;
    Logger.LogError(e.ToString());
    break;
}
```

### ステップ 3: 映像フォーマットを確認・修正する

Baballonia の推論コードが BGR を要求する場合は変換を追加:

```csharp
var gray = new Mat(IMAGE_HEIGHT, IMAGE_WIDTH, MatType.CV_8UC1);
Marshal.Copy(_imageBuffer, IMAGE_HEADER_SIZE, gray.Data, IMAGE_DATA_SIZE);
var mat = new Mat();
Cv2.CvtColor(gray, mat, ColorConversionCodes.GRAY2BGR);
SetRawMat(mat);
```

### ステップ 4: `.csproj` にビルド後コピーを追加（オプション）

開発中に Baballonia の `Modules/` フォルダへ自動コピーするターゲットを追加すると便利:

```xml
<Target Name="CopyToModules" AfterTargets="AfterBuild">
  <Copy SourceFiles="$(OutputPath)PSVR2Toolkit.Baballonia.dll" DestinationFolder="..\..\Baballonia\src\Baballonia.Desktop\bin\Debug\net10.0\Modules\" />
  <Copy SourceFiles="$(OutputPath)PSVR2Toolkit.CAPI.dll" DestinationFolder="..\..\Baballonia\src\Baballonia.Desktop\bin\Debug\net10.0\Modules\" />
</Target>
```

---

## 結論

- **ビルドは問題なし**。依存関係はすべて満たされている。
- **そのままでは使用不可**。最低限 `CanConnect()` と `TaskCanceledException` の修正が必要。
- **フォークして修正を推奨**。特に問題 1〜3 は比較的小規模な変更で解決できる。
- 問題 4（映像フォーマット）は Baballonia 本体の推論コードを確認後、必要に応じて対応。
