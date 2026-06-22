# PSVR2 eye-tracking camera feed survey

調査日: 2026-06-22

## 結論

このリポジトリは、隣接する `..\Baballonia` リポジトリと .NET 10 SDK がある環境では Debug/Release ともにビルドできました。確認コマンドは `dotnet build PSVR2Toolkit.Baballonia.sln` と `dotnet build PSVR2Toolkit.Baballonia.sln -c Release` で、どちらも警告 0 / エラー 0 です。

ただし、「ビルドしてそのまま Baballonia で使用できる状態」とまでは言いにくいです。主な理由は、Baballonia 本体のビルド/公開処理にこのモジュールが組み込まれておらず、生成物を `Modules` フォルダへ配置する手順やリリース成果物も用意されていないためです。また、実行時は同梱の `PSVR2Toolkit.CAPI.dll` と PSVR2Toolkit 側の実行状態に依存しますが、managed 側には接続可否や gaze image status の検証がまだ入っていません。

短く言うと、現状は「開発環境でビルド可能な試作モジュール」です。手動配置と実機確認で動く可能性はありますが、配布・自動同梱・安定運用まで持っていくならフォークして修正するのが妥当です。

## 現状の実装

- `PSVR2Toolkit.Baballonia.csproj` は `net10.0` を対象にし、`..\..\Baballonia\src\Baballonia.SDK\Baballonia.SDK.csproj` を直接参照しています。`PSVR2Toolkit.CAPI.dll` はビルド出力にコピーされます。
- `CAPI.cs` は `PSVR2Toolkit.CAPI.dll` の `CAPI_Initialize()` と `CAPI_GetGazeImage(byte[])` を P/Invoke しています。
- `Vr2Capture` は静的コンストラクタで C API を初期化し、`StartCapture()` で即 `IsReady = true` にして取得ループを開始します。
- 取得ループは `0x200100` バイトのバッファを C API に渡し、先頭 2 バイトが `VI` のときだけ、ヘッダ `0x100` バイトを飛ばして 400x200 の `CV_8UC1` `Mat` として `SetRawMat()` に渡します。
- `Vr2CaptureFactory.CanConnect()` は TODO のまま常に `true` を返します。provider 名は `PlayStation VR2` です。

## Baballonia 側との接続

Baballonia Desktop は `AppContext.BaseDirectory/Modules/*.dll` を列挙し、exported type の中から `ICaptureFactory` 実装を探して読み込みます。つまり `PSVR2Toolkit.Baballonia.dll` が `Modules` に入っていれば、構造上は capture provider として発見される設計です。

一方、Baballonia Desktop の `.csproj` で `Modules` に移動される DLL は標準の `Baballonia.OpenCVCapture`、`Baballonia.IPCameraCapture`、`Baballonia.SerialCameraCapture`、`Baballonia.VFTCapture`、Linux の `Baballonia.LibV4L2Capture` だけです。PSVR2 モジュールはこの一覧に含まれていません。

映像形式については、Baballonia の eye pipeline は `GetFrame(ColorType.Gray8)` を要求します。UI 側も 1ch 画像を左半分/右半分に分けて表示する処理を持っています。そのため、PSVR2 側が 400x200 の横並びグレースケールフレームを返す設計なら、Baballonia の後段処理とはおおむね噛み合います。

## そのまま試す場合の流れ

1. `..\Baballonia` がこのリポジトリの隣にある状態で、`.NET SDK 10` を使って `dotnet build PSVR2Toolkit.Baballonia.sln -c Release` を実行します。
2. `PSVR2Toolkit.Baballonia\bin\Release\net10.0\PSVR2Toolkit.Baballonia.dll` を Baballonia Desktop の `Modules` フォルダへ配置します。
3. `PSVR2Toolkit.Baballonia\bin\Release\net10.0\PSVR2Toolkit.CAPI.dll` も、同じ `Modules` フォルダ、または Baballonia 実行ファイルの検索パス上に配置します。
4. `Baballonia.SDK.dll` は Baballonia 本体側のものを使う想定です。モジュール出力フォルダを丸ごと `Modules` にコピーすると、SDK の二重読み込みや type identity 問題を誘発する可能性があるため、まずは module DLL と CAPI DLL だけを置くのが安全です。
5. PSVR2Toolkit 側の改造済み PS VR2 driver/app を導入し、SteamVR/PSVR2Toolkit が gaze data を出せる状態で Baballonia を起動します。
6. Baballonia のカメラ入力欄に任意の値を入れると、現状の `CanConnect()` は常に true なので `PlayStation VR2` が候補に出る可能性があります。複数 provider が出る場合は capture method で `PlayStation VR2` を選びます。
7. 左目/右目とも同じ PSVR2 source を指定して開始し、プレビューが左右に分割表示されるか確認します。

## 足りない点

- 自動同梱がありません。Baballonia Desktop の build/publish target に PSVR2 モジュールと `PSVR2Toolkit.CAPI.dll` が含まれていません。
- 使用手順と release packaging がありません。GitHub 上の `PSVR2Toolkit.Baballonia` には調査時点で release がありませんでした。
- `CanConnect()` が常に true です。他 provider より先に選ばれたり、通常カメラ/URL/空文字にも反応したりするため、UI 上の誤選択を起こしやすいです。
- C API の status を見ていません。同梱 native DLL には文字列上 `CAPI_GetGazeStatus` も見えますが、managed 側では未宣言です。
- `StartCapture()` は実フレーム取得前に `IsReady = true` を設定します。そのため、C API が未準備でも Baballonia 側は最大 13 秒待ってから失敗します。
- フレーム仕様の検証が必要です。コードコメントでは BC4 と書かれていますが、実装は `0x100` バイト後ろから `400 * 200` バイトをそのまま `CV_8UC1` にコピーしています。実際の PSVR2Toolkit gaze image が未圧縮 8bit グレースケールとしてこの位置に並ぶかは、実機または CAPI 仕様で確認が必要です。
- PSVR2Toolkit upstream の公開 C API header は gaze state 系が中心で、`CAPI_GetGazeImage` は現在の公開 header では確認できませんでした。現状は同梱 DLL の未公開/独自 API に依存している扱いです。

## フォークして直す場合の推奨フロー

### 1. 最小統合

- Baballonia 側に `PSVR2Toolkit.Baballonia` の `ProjectReference` を追加するか、別リポジトリのまま module artifact を生成する publish script を用意します。
- Baballonia Desktop の `CopyModulesToFolder` / `CopyModulesToFolderPublish` 相当の処理に `PSVR2Toolkit.Baballonia.dll` と必要なら `.pdb` を追加します。
- `PSVR2Toolkit.CAPI.dll` は module DLL と同じ場所にコピーします。
- `Baballonia.SDK.dll` は host 側に任せ、`Modules` に同梱しない方針を明記します。

### 2. Provider 判定

- `CanConnect()` を `true` 固定から、`psvr2`、`playstation-vr2`、`PlayStation VR2` など明示的な source のみ受ける実装に変更します。
- 可能なら `CAPI_GetGazeStatus` または軽量 probe を managed 側に公開し、PSVR2Toolkit が起動していない場合は provider 候補や start を失敗扱いにします。

### 3. Capture の堅牢化

- `StartCapture()` は「初回 valid frame を得た」または「C API status が ready」の時点で `IsReady = true` にします。
- cancel 時の `TaskCanceledException` / `OperationCanceledException` はエラー扱いにしないよう分離します。
- valid frame が一定時間来ない場合の timeout/backoff と、ログメッセージを追加します。
- `Mat` の生成頻度と破棄方針を見直し、長時間動作でのメモリ/GC 負荷を確認します。

### 4. フレーム仕様の確定

- `VI` ヘッダ、`0x100` ヘッダサイズ、400x200、1ch、左右の並び、stride、BC4 の有無を実機フレームで検証します。
- もし実データが BC4 圧縮なら、OpenCV に渡す前に展開処理を追加します。
- Baballonia の crop/rotation 初期値として、PSVR2 の左右目が自然に収まるプリセットを用意します。

### 5. テスト

- `CAPI` 呼び出しを薄い interface に切り出し、fake image provider で `VI` ヘッダあり/なし、timeout、例外、stop cancellation をテストします。
- Baballonia Desktop の module loader に対する smoke test を作り、`PSVR2Toolkit.Baballonia.dll` が `ICaptureFactory` として検出されることを確認します。
- 実機チェックリストを用意します: PSVR2Toolkit 導入、SteamVR 起動、Baballonia provider 表示、左右プレビュー、tracking 出力、停止/再開始。

## 調査根拠

ローカルで確認した主なファイル:

- `PSVR2Toolkit.Baballonia/PSVR2Toolkit.Baballonia.csproj`
- `PSVR2Toolkit.Baballonia/CAPI.cs`
- `PSVR2Toolkit.Baballonia/Vr2Capture.cs`
- `PSVR2Toolkit.Baballonia/Vr2CaptureFactory.cs`
- `..\Baballonia\src\Baballonia.Desktop\Captures\DesktopConnector.cs`
- `..\Baballonia\src\Baballonia.Desktop\Baballonia.Desktop.csproj`
- `..\Baballonia\src\Baballonia\Factories\SingleCameraSourceFactory.cs`
- `..\Baballonia\src\Baballonia\Services\Inference\VideoSources\SingleCameraSource.cs`
- `..\Baballonia\src\Baballonia\Services\Inference\EyeProcessingPipeline.cs`
- `..\Baballonia\src\Baballonia\ViewModels\SplitViewPane\HomePageViewModel.cs`

外部で確認した主な情報:

- PSVR2Toolkit.Baballonia: https://github.com/BnuuySolutions/PSVR2Toolkit.Baballonia
- PSVR2Toolkit: https://github.com/BnuuySolutions/PSVR2Toolkit
- PSVR2Toolkit public C API header: https://github.com/BnuuySolutions/PSVR2Toolkit/blob/main/include/psvr2_toolkit_capi.h
- PSVR2Toolkit gaze USB thread: https://github.com/BnuuySolutions/PSVR2Toolkit/blob/main/projects/psvr2_openvr_driver_ex/usb_thread_gaze.cpp
- PSVR2Toolkit shared/custom share stub: https://github.com/BnuuySolutions/PSVR2Toolkit/blob/main/projects/libcustomshare/custom_share_manager.cpp

