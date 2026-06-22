# 未確定事項とリスク

作成日: 2026-06-22

## 1. すぐ決めるべき未確定事項

### Q-001: PSVR2 source 文字列

決めること:
- `CanConnect()` が受け付ける source 形式。

候補:
- `psvr2://gaze`
- `psvr2`
- `playstation-vr2`
- 空文字

推奨:
- `psvr2://gaze` を正式形式にする。
- 互換用に `psvr2` と `playstation-vr2` を許可する。
- 空文字は provider 名指定時だけ許可するか、初期実装では許可しない。

理由:
- `CanConnect()` が `true` 固定だと通常 camera source を横取りする。
- 明示的な source 形式があると設定、ログ、トラブルシュートがしやすい。

### Q-002: C API status を使うか

決めること:
- `CAPI_GetGazeStatus` のような status API を使うか。

現状:
- 同梱 native DLL には status らしき symbol がある可能性が調査で示されている。
- managed 側には宣言されていない。
- upstream 公開 header では `CAPI_GetGazeImage` 自体が確認できていない。

推奨:
- 初期実装は `VI` ヘッダ付き valid frame を ready 判定にする。
- status API は symbol、signature、戻り値を確認できた後に使う。

### Q-003: gaze image は Gray8 か BC4 か

決めること:
- `CAPI_GetGazeImage()` が返す image data を未圧縮 Gray8 として扱ってよいか。

現状:
- コードコメントは BC4 と書いている。
- 実装は `0x100` bytes 後ろから `400 * 200` bytes をそのまま `CV_8UC1` にコピーしている。

推奨:
- 実機 frame dump で確認する。
- 未圧縮 Gray8 なら、コメントを実態に合わせる。
- BC4 なら decoder を入れてから `Mat` にする。

### Q-004: Baballonia 本体へ統合するか

決めること:
- module 単体配布にするか、Baballonia 本体の build/publish に組み込むか。

推奨:
- 初期は module 単体配布。
- 安定後に Baballonia 本体へ optional module として統合する。

理由:
- Baballonia 本体の `CopyModulesToFolder` は同一 solution 内の module を移動する設計。
- 外部 repo を本体に直接参照させると結合が強くなる。
- PSVR2Toolkit と native DLL の導入条件が特殊なため、標準同梱には確認が必要。

## 2. 技術リスク

### RISK-001: 未公開 C API 依存

内容:
- `CAPI_GetGazeImage` は調査時点の upstream 公開 header では確認できていない。
- 同梱 DLL の独自 API に依存している可能性がある。

影響:
- upstream 更新で API が消える、signature が変わる、再配布条件が不明になる。

対策:
- DLL version と由来を docs に記録する。
- C API adapter を分離する。
- symbol/signature を release ごとに確認する。

### RISK-002: native DLL の解決失敗

内容:
- `PSVR2Toolkit.CAPI.dll` が見つからないと P/Invoke が失敗する。

影響:
- provider load または capture start に失敗する。

対策:
- module DLL と同じ `Modules` folder に置く。
- 起動時に分かりやすい log を出す。
- release zip に必ず同梱する。

### RISK-003: type identity 問題

内容:
- `Baballonia.SDK.dll` を module folder に重複配置すると、host 側と module 側で `ICaptureFactory` の型が別物として扱われる可能性がある。

影響:
- `DesktopConnector` が `ICaptureFactory` 実装として認識できない。

対策:
- module package に `Baballonia.SDK.dll` を含めない。
- host の SDK assembly を参照する。
- release 手順に同梱禁止を明記する。

### RISK-004: `IsReady` の意味のずれ

内容:
- 現状は `StartCapture()` 直後に `IsReady = true` になる。
- Baballonia 側は `IsReady` を見て frame を取得しに行く。

影響:
- C API が未準備でも 13 秒待って失敗する。
- 利用者には provider が動いているように見える。

対策:
- 初回 valid frame 後に `IsReady = true`。
- timeout と backoff log を実装する。

### RISK-005: frame format mismatch

内容:
- `Capture.cs` のコメントは BGR を想定している。
- 実際の pipeline は `GetFrame(ColorType.Gray8)` を呼び、`SingleCameraSource` は 1ch `Mat` を扱える。

影響:
- 将来の Baballonia 変更で Gray8 前提が壊れる可能性がある。

対策:
- docs に Gray8 provider であることを明記する。
- integration test で `GetFrame(ColorType.Gray8)` を確認する。
- 必要なら `Bgr24` 変換 option を追加する。

### RISK-006: 実機なしでは最終判断できない

内容:
- gaze image の向き、左右並び、圧縮有無は実機で確認する必要がある。

影響:
- fake test だけでは「Baballonia で推論できる」と言い切れない。

対策:
- 実機チェックリストを必須にする。
- frame dump を保存し、仕様として docs に残す。

## 3. Baballonia 側で見つかった注意点

### NOTE-001: `SingleCameraSource` は Gray8 を扱える

`SingleCameraSource.GetFrame(ColorType.Gray8)` は raw `Mat` が 1ch の場合、そのまま返す。したがって PSVR2 provider が `CV_8UC1` を返すこと自体は現行 pipeline と噛み合う。

### NOTE-002: `Capture.cs` のコメントは BGR 想定

`Capture.AcquireRawMat()` のコメントには BGR color space とある。実装上は Gray8 も扱えるが、SDK 契約として曖昧なので docs に明記する。

### NOTE-003: `Modules` へ移動される module は既存 provider のみ

Baballonia Desktop の `CopyModulesToFolder` / `CopyModulesToFolderPublish` は既存 capture module のみ対象。PSVR2 module は自動同梱されない。

## 4. 判断ログ

| 日付 | 判断 | 根拠 |
|---|---|---|
| 2026-06-22 | 初期対象は Windows x64 | `PSVR2Toolkit.CAPI.dll` が Windows DLL であり、PSVR2Toolkit/SteamVR 依存があるため |
| 2026-06-22 | 初期は module 単体配布 | Baballonia 本体に変更せず `Modules` 配置で検証できるため |
| 2026-06-22 | Gray8 を許容する前提で進める | `EyeProcessingPipeline` が `ColorType.Gray8` を要求し、`SingleCameraSource` が 1ch を扱えるため |
| 2026-06-22 | ready は valid frame 基準 | 現状の即 ready は Baballonia 側の 13 秒 wait と相性が悪いため |
