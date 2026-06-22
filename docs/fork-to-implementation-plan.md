# フォークから実装までの計画

作成日: 2026-06-22

## 1. リポジトリ方針

初期実装は `PSVR2Toolkit.Baballonia` のフォークで進める。Baballonia 本体への変更は、module 配置の自動化が必要になった段階で別 PR として扱う。

理由:
- 既知問題の多くは `PSVR2Toolkit.Baballonia` 側で閉じている。
- Baballonia 本体は `Modules` 配下の DLL を読み込む設計なので、module 側だけでも検証できる。
- 本体に `ProjectReference` を追加する案は便利だが、別リポジトリを強く結合するため、まずは配布物として成立させる方が安全。

## 2. フォーク直後の準備

1. GitHub で `BnuuySolutions/PSVR2Toolkit.Baballonia` を fork する。
2. fork を clone する。
3. upstream remote を追加する。
4. Baballonia リポジトリを同じ親ディレクトリに置く。
5. .NET 10 SDK を確認する。
6. `dotnet build PSVR2Toolkit.Baballonia.sln -c Release` で baseline を取る。

推奨ブランチ:

```text
feature/psvr2-provider-hardening
```

推奨 remote:

```text
origin   自分の fork
upstream BnuuySolutions/PSVR2Toolkit.Baballonia
```

## 3. 実装マイルストーン

### M1: provider 判定の修正

対象:
- `PSVR2Toolkit.Baballonia/Vr2CaptureFactory.cs`

作業:
- `CanConnect()` の `true` 固定をやめる。
- 受け付ける source 文字列を定義する。
- 大文字小文字、空白、末尾 slash を正規化する。

推奨仕様:

```text
true:
- psvr2
- psvr2://gaze
- playstation-vr2
- PlayStation VR2

false:
- 通常のファイルパス
- http:// または rtsp://
- 数値 camera index
- 未定義の文字列
```

空文字を許可するかは UI 運用次第。許可する場合は、通常 camera provider との優先順位衝突を避けるため、provider 名指定時だけ許可する設計が望ましい。

### M2: capture loop の停止処理を修正

対象:
- `PSVR2Toolkit.Baballonia/Vr2Capture.cs`

作業:
- `OperationCanceledException` / `TaskCanceledException` を正常終了として分離する。
- `StopCapture()` が UI thread を長く block しないよう、必要なら async 化または timeout 付き wait を検討する。
- `_cts` と `_captureTask` の dispose/clear を行い、再開始時に古い token を使わないようにする。

注意:
- `StopCapture()` は SDK で `Task<bool>` を返すため、`async` 実装に変更できる。
- 現状の `.Wait()` は例外を `AggregateException` として再送出する可能性がある。

### M3: ready 判定を実フレーム基準にする

対象:
- `PSVR2Toolkit.Baballonia/Vr2Capture.cs`

作業:
- `StartCapture()` 直後の `IsReady = true` を削除する。
- `VI` ヘッダを確認して `SetRawMat()` したタイミングで `IsReady = true` にする。
- 一定時間 `VI` ヘッダが来ない場合、backoff とログを入れる。

推奨:
- `StartCapture()` は capture loop 起動に成功したら `true` を返す。
- `IsReady` は data readiness として扱う。
- Baballonia 側の `CreateStart()` が最大 13 秒待つため、module 側でも 13 秒未満の範囲で状態ログを出す。

### M4: C API adapter を分離する

対象:
- `PSVR2Toolkit.Baballonia/CAPI.cs`
- 新規 `IGazeImageApi` など

作業:
- P/Invoke を直接呼ぶ static 依存を、薄い adapter に寄せる。
- fake 実装から任意の buffer を返せるようにする。
- `CAPI_Initialize()` の失敗を捕捉し、provider 作成または start 失敗として扱えるようにする。

目的:
- `VI` ヘッダあり/なし、例外、timeout、キャンセルを単体テストできるようにする。
- 未公開 API 依存を局所化する。

### M5: フレーム仕様を確定する

対象:
- `PSVR2Toolkit.Baballonia/Vr2Capture.cs`
- docs

作業:
- 実機フレームを保存またはログ化し、先頭 bytes、画像寸法、左右並びを確認する。
- 実データが未圧縮 Gray8 なら現実装を維持する。
- 実データが BC4 圧縮なら、OpenCV `Mat` に渡す前に展開処理を追加する。

判断:
- Baballonia の `SingleCameraSource` は 1ch `Mat` を Gray8 として扱えるため、BGR 変換は必須ではない。
- ただし `Capture.cs` のコメントは BGR を想定しているため、module 側 docs に「この provider は Gray8 を返す」と明記する。

### M6: 配置とリリースを整える

対象:
- `.csproj`
- README
- release package

作業:
- `PSVR2Toolkit.Baballonia.dll` と `PSVR2Toolkit.CAPI.dll` の配置手順を README に追加する。
- 開発用 copy target または PowerShell script を追加する。
- release zip の内容を定義する。

推奨 release zip:

```text
PSVR2Toolkit.Baballonia.dll
PSVR2Toolkit.CAPI.dll
PSVR2Toolkit.Baballonia.pdb
LICENSE
README.md
```

`Baballonia.SDK.dll` は含めない。host 側と module 側で SDK assembly が重複すると、type identity 問題が起きる可能性がある。

## 4. 実装順序

1. baseline build を保存する。
2. `CanConnect()` を修正する。
3. 停止時例外処理を修正する。
4. ready 判定を初回 valid frame 基準にする。
5. C API adapter と fake を追加する。
6. fake frame による単体テストを追加する。
7. module 配置手順を README/docs に追加する。
8. Baballonia `Modules` 配下で provider 検出を smoke test する。
9. 実機で gaze image を確認し、フレーム仕様を確定する。
10. release package を作成する。

## 5. 推奨タスク分割

### PR 1: Safety fixes

- `CanConnect()` 実装。
- cancellation handling 修正。
- ready 判定修正。
- README に最低限の使い方を追加。

### PR 2: Testability

- C API adapter 導入。
- fake API による unit tests 追加。
- `VI` ヘッダ、ヘッダ不正、キャンセル、例外のテスト。

### PR 3: Packaging

- 開発用 copy target または script。
- release package 定義。
- 導入手順とトラブルシュート整備。

### PR 4: Hardware validation

- 実機フレーム仕様の反映。
- 必要なら BC4 展開または rotation/crop preset。
- 実機チェックリストの結果を docs に追記。

## 6. Baballonia 本体へ変更する場合

Baballonia 本体に変更するのは、以下のどれかが必要になったときに限定する。

- 公式配布物に PSVR2 module を同梱する。
- `Baballonia.Desktop.csproj` の `CopyModulesToFolder` / `CopyModulesToFolderPublish` に module を組み込む。
- UI に `PlayStation VR2` 専用 source preset を追加する。
- module 探索時に native DLL を同じ folder から確実に解決する仕組みが必要になる。

その場合も、module の安定化を先に終えてから本体側 PR を分ける。

## 7. 作業完了の定義

- fork 上で release branch が作成されている。
- release build が成功している。
- `Modules` 配置で Baballonia に provider が出る。
- 誤 provider 選択が再現しない。
- 停止/再開始でエラーログが出ない。
- fake または実機で frame が Baballonia pipeline まで届く。
- 導入手順、検証手順、既知制約が docs に残っている。
