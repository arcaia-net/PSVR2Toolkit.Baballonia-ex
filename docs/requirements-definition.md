# PSVR2Toolkit.Baballonia 要件定義

作成日: 2026-06-22

## 1. 目的

`PSVR2Toolkit.Baballonia` をフォークし、PlayStation VR2 のアイトラッキングカメラ映像を Baballonia の capture provider として安定して利用できる状態にする。

現状は `dotnet build` が通る試作モジュールであり、Baballonia の `Modules` フォルダに手動配置すれば認識される可能性がある。ただし、provider 判定、停止時例外処理、ready 判定、配布手順、実機フレーム仕様の確認が不足している。

## 2. 対象範囲

### 対象に含める

- `PSVR2Toolkit.Baballonia` のフォークと保守ブランチ作成。
- `Vr2CaptureFactory.CanConnect()` の実装。
- `Vr2Capture` の capture loop、停止処理、ready 判定、ログ出力の堅牢化。
- `PSVR2Toolkit.CAPI.dll` を含む配置、ビルド、リリース手順の整備。
- Baballonia Desktop の `Modules` フォルダで provider として検出されることの確認。
- 実機または fake C API を使ったフレーム仕様の検証。
- README または docs に利用手順を追加する。

### 対象に含めない

- PSVR2Toolkit 本体の gaze image API 仕様変更。
- Baballonia の推論モデル変更。
- SteamVR driver の導入自動化。
- PSVR2 以外の camera provider 改修。
- クロスプラットフォーム対応。`PSVR2Toolkit.CAPI.dll` が Windows DLL であるため、初期対象は Windows とする。

## 3. 前提

- 開発環境に .NET 10 SDK がある。
- このリポジトリの隣に Baballonia リポジトリがある。現在の参照は `..\..\Baballonia\src\Baballonia.SDK\Baballonia.SDK.csproj`。
- `PSVR2Toolkit.CAPI.dll` は実行時に module DLL と同じ場所、またはプロセスの DLL 検索パス上に存在する。
- Baballonia Desktop は `AppContext.BaseDirectory/Modules/*.dll` を読み込み、`ICaptureFactory` 実装を capture provider として登録する。
- Baballonia の inference pipeline は `GetFrame(ColorType.Gray8)` を要求し、`SingleCameraSource` は 1ch `Mat` を Gray8 として扱える。

## 4. ユーザー要求

### R-001: PSVR2 provider の誤選択を防ぐ

Baballonia が通常カメラや URL を開こうとしたとき、PSVR2 provider が誤って選ばれないこと。

受け入れ条件:
- `CanConnect()` が `true` 固定ではない。
- 明示的な PSVR2 source だけを受け付ける。
- provider 名指定時も、source が不正なら作成に失敗する。

推奨 source 形式:
- `psvr2://gaze`
- `psvr2`
- `playstation-vr2`
- 空文字を許可する場合は、UI 仕様として「空文字は PSVR2 専用」と明記する。

### R-002: capture の正常停止でエラーログを出さない

`StopCapture()` によるキャンセルは正常な操作として扱うこと。

受け入れ条件:
- `OperationCanceledException` と `TaskCanceledException` は error log にしない。
- 停止後に `_captureTask`、`_cts` の状態が再開始可能な形で整理される。
- UI から停止、再開始しても余計なエラーが残らない。

### R-003: 初回 valid frame 取得後に ready にする

実フレームが取れていない状態で Baballonia 側へ ready を通知しないこと。

受け入れ条件:
- `StartCapture()` 直後に無条件で `IsReady = true` にしない。
- `VI` ヘッダを持つ valid frame を取得して `SetRawMat()` した後、または C API の ready status を確認した後に `IsReady = true` にする。
- 一定時間 valid frame が来ない場合は timeout として扱い、原因が追えるログを出す。

### R-004: フレーム仕様を確定する

`CAPI_GetGazeImage()` が返す gaze image の仕様を実機または C API 仕様で確認すること。

受け入れ条件:
- `VI` ヘッダの有無を確認する。
- ヘッダサイズ `0x100` の妥当性を確認する。
- 画像サイズ 400x200、左右目の並び、stride を確認する。
- 実データが未圧縮 Gray8 か、BC4 圧縮かを確認する。
- 必要なら BC4 展開または色変換を実装する。

### R-005: Baballonia の module 配置を再現可能にする

手動コピーだけに依存しない、または手動コピー手順が明確な状態にすること。

受け入れ条件:
- `PSVR2Toolkit.Baballonia.dll` と `PSVR2Toolkit.CAPI.dll` を `Modules` へ置く方法が明文化されている。
- 開発用には build 後 copy target または script を用意する。
- リリース用には zip/package に含めるファイル一覧を定義する。
- `Baballonia.SDK.dll` は host 側のものを使う方針を明記し、`Modules` へ重複同梱しない。

### R-006: 導入手順を利用者向けに説明する

PSVR2Toolkit、SteamVR、Baballonia、module DLL の関係を利用者が再現できるようにすること。

受け入れ条件:
- README または docs に Windows 向け導入手順がある。
- 必要な外部コンポーネントと確認方法が書かれている。
- 失敗時の確認ポイントがある。

## 5. 非機能要件

### 安定性

- capture loop は長時間実行でメモリが増え続けないこと。
- `Mat` の所有権は Baballonia SDK の `SetRawMat()` / `AcquireRawMat()` の規約に従うこと。
- C API 呼び出し失敗時に UI を巻き込んでクラッシュしないこと。

### 保守性

- C API への直接依存は薄い adapter/interface に寄せ、fake 実装でテスト可能にする。
- magic number は定数化し、根拠をコメントまたは docs に残す。
- 未公開 API に依存する箇所は明示する。

### ログ

- 通常停止は debug/info までにする。
- gaze image が未準備、ヘッダ不正、DLL 未ロード、C API 例外は識別できるログにする。
- 連続失敗ログは rate limit または backoff する。

### 配布

- 初期リリース対象は Windows x64 とする。
- MIT License 表記を維持する。
- `PSVR2Toolkit.CAPI.dll` の再配布可否と由来を release note に記載する。

## 6. 受け入れ条件まとめ

- `dotnet build PSVR2Toolkit.Baballonia.sln -c Release` が成功する。
- Baballonia Desktop の `Modules` 配下に配置したとき、`PlayStation VR2` provider が検出される。
- 通常カメラ入力で PSVR2 provider が誤選択されない。
- PSVR2 source を指定したときだけ `Vr2Capture` が作成される。
- `StopCapture()` でエラー扱いのログが出ない。
- valid frame が来るまで `IsReady` が true にならない。
- 実機または fake C API で `VI` ヘッダありの 400x200 frame が Baballonia の `GetFrame(ColorType.Gray8)` まで届く。
- リリース zip または手順書から、別環境で同じ配置を再現できる。

## 7. 参考にした調査結果

- `docs/codex-survey-results.md`
- `docs/claude-survey-results.md`
- `PSVR2Toolkit.Baballonia/CAPI.cs`
- `PSVR2Toolkit.Baballonia/Vr2Capture.cs`
- `PSVR2Toolkit.Baballonia/Vr2CaptureFactory.cs`
- `..\Baballonia\src\Baballonia.SDK\Capture.cs`
- `..\Baballonia\src\Baballonia.Desktop\Captures\DesktopConnector.cs`
- `..\Baballonia\src\Baballonia\Services\Inference\VideoSources\SingleCameraSource.cs`
- `..\Baballonia\src\Baballonia\Services\Inference\EyeProcessingPipeline.cs`
