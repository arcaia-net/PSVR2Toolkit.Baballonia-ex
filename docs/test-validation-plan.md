# テスト・検証計画

作成日: 2026-06-22

## 1. 目的

`PSVR2Toolkit.Baballonia` が Baballonia の capture provider として安全に読み込まれ、PSVR2 gaze image を Gray8 frame として推論 pipeline に渡せることを確認する。

## 2. テスト対象

- `Vr2CaptureFactory.CanConnect()`
- `Vr2Capture.StartCapture()`
- `Vr2Capture.StopCapture()`
- C API 呼び出し adapter
- image buffer から `Mat` への変換
- Baballonia Desktop の module loader
- Baballonia `SingleCameraSource.GetFrame(ColorType.Gray8)`
- release package の配置手順

## 3. 単体テスト

### UT-001: provider 判定

確認内容:
- PSVR2 source だけ `true`。
- 通常 camera path、URL、数値 index は `false`。
- 大文字小文字、前後空白、末尾 slash の扱いが仕様どおり。

入力例:

| input | expected |
|---|---|
| `psvr2` | true |
| `psvr2://gaze` | true |
| `playstation-vr2` | true |
| `PlayStation VR2` | true |
| `0` | false |
| `rtsp://example.local/camera` | false |
| `C:\video\sample.mp4` | false |
| `http://example.local` | false |

### UT-002: valid frame の取り込み

fake C API が以下の buffer を返す。

- 先頭 2 bytes: `0x56 0x49`
- header: `0x100` bytes
- image data: 400 x 200 bytes

期待結果:
- `SetRawMat()` 相当で `CV_8UC1` の `Mat` が設定される。
- `Mat.Width == 400`
- `Mat.Height == 200`
- `Mat.Channels() == 1`
- 初回 valid frame 後に `IsReady == true`

### UT-003: invalid header

fake C API が `VI` ではない header を返す。

期待結果:
- `IsReady` は true にならない。
- 空 `Mat` を設定しない。
- error log を連続で出し続けない。
- loop は backoff しながら継続する。

### UT-004: C API 例外

fake C API が例外を投げる。

期待結果:
- capture loop が停止する、または再試行方針どおりに動く。
- `IsReady == false`
- 原因が分かる log が出る。
- UI thread へ未処理例外が漏れない。

### UT-005: cancellation

`StopCapture()` を呼ぶ。

期待結果:
- `OperationCanceledException` / `TaskCanceledException` は error log にならない。
- task が終了する。
- `_cts` が dispose される。
- 再度 `StartCapture()` できる。

## 4. 統合テスト

### IT-001: solution build

コマンド:

```powershell
dotnet build PSVR2Toolkit.Baballonia.sln -c Release
```

期待結果:
- error 0。
- `PSVR2Toolkit.Baballonia.dll` が生成される。
- `PSVR2Toolkit.CAPI.dll` が output folder にコピーされる。

### IT-002: module loader

手順:
1. Baballonia Desktop を build する。
2. `PSVR2Toolkit.Baballonia.dll` を Baballonia Desktop の `Modules` folder に置く。
3. `PSVR2Toolkit.CAPI.dll` も同じ folder に置く。
4. Baballonia Desktop を起動する。

期待結果:
- `DesktopConnector` が module DLL を load する。
- `ICaptureFactory` として `Vr2CaptureFactory` が検出される。
- provider name が `PlayStation VR2` として表示または選択可能になる。

### IT-003: provider 誤選択防止

手順:
1. 通常 camera source または URL を入力する。
2. provider name 未指定で camera source を作成する。

期待結果:
- PSVR2 provider が先に選ばれない。
- 対応する通常 provider が選ばれる。
- PSVR2 provider が source を横取りしない。

### IT-004: Gray8 pipeline

fake または実機 frame で `SingleCameraSource.GetFrame(ColorType.Gray8)` を呼ぶ。

期待結果:
- 1ch `Mat` は変換なしで返る。
- `EyeProcessingPipeline` が `GetFrame(ColorType.Gray8)` で frame を取得できる。
- 取得した frame が空でない。

## 5. 実機検証

### HW-001: 環境確認

- Windows x64。
- .NET 10 SDK。
- Baballonia Desktop が起動できる。
- PSVR2Toolkit 側の driver/app が導入済み。
- SteamVR が起動できる。
- PSVR2 の gaze data が PSVR2Toolkit 側で取得可能。

### HW-002: provider 表示

手順:
1. `PSVR2Toolkit.Baballonia.dll` と `PSVR2Toolkit.CAPI.dll` を `Modules` に置く。
2. Baballonia Desktop を起動する。
3. capture provider 一覧を確認する。

期待結果:
- `PlayStation VR2` が表示される。
- DLL load error が出ない。

### HW-003: frame 取得

手順:
1. source に `psvr2://gaze` など仕様で決めた値を入れる。
2. provider に `PlayStation VR2` を指定する。
3. capture を開始する。

期待結果:
- 13 秒以内に frame が取得される。
- preview に左右目の画像が表示される。
- `IsReady` は valid frame 取得後に true になる。

### HW-004: 左右並び確認

確認内容:
- 左右目が横並びで正しい向きに出る。
- 片目だけ欠けていない。
- 上下反転、左右反転、rotation がないか確認する。

結果によって必要な対応:
- crop preset。
- rotate/flip。
- 左右入れ替え。
- stride 補正。

### HW-005: 停止・再開始

手順:
1. capture 開始。
2. 数秒待つ。
3. capture 停止。
4. 再度 capture 開始。

期待結果:
- 停止時に error log が出ない。
- 再開始後に frame が戻る。
- Baballonia が固まらない。

### HW-006: 長時間実行

手順:
1. capture を 30 分以上継続する。
2. memory、CPU、log を監視する。

期待結果:
- memory が増え続けない。
- log が過剰に増えない。
- frame が極端に詰まらない。

## 6. リリース前チェック

- Release build が成功している。
- release zip の中身が定義どおり。
- `Baballonia.SDK.dll` を module package に重複同梱していない。
- `PSVR2Toolkit.CAPI.dll` の同梱可否と由来を release note に書いた。
- README にインストール、起動、source 指定、トラブルシュートがある。
- 実機検証の結果を docs に記録した。

## 7. 失敗時の調査ポイント

### provider が出ない

- `Modules` folder に DLL があるか。
- `PSVR2Toolkit.CAPI.dll` が同じ folder にあるか。
- Baballonia log に assembly load error がないか。
- `Baballonia.SDK.dll` の type identity 問題が起きていないか。

### frame が来ない

- PSVR2Toolkit 側が gaze image を出しているか。
- `CAPI_Initialize()` が成功しているか。
- `CAPI_GetGazeImage()` が例外を投げていないか。
- buffer 先頭が `VI` になっているか。

### 画像が崩れる

- 実データが BC4 圧縮ではないか。
- header size が `0x100` で正しいか。
- stride が 400 bytes で正しいか。
- 左右目の配置が想定どおりか。
