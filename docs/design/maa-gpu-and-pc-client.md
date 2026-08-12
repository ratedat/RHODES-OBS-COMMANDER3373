# MAA GPU推論・PCクライアント先行対応仕様

## 目的

RHODES OBS COMMANDER3373のMAA-OCRを、既定の安定性を維持したままDirectMLで比較検証できるようにする。
また、Androidエミュレーターとは別に、アークナイツPCクライアントのウィンドウを選択して撮影・認識へ渡せるようにする。

## 根拠として確認した現状

- MaaFramework 5.12.3のWindows runtimeには`DirectML.dll`と`MaaWin32ControlUnit.dll`が含まれる。
- C# binding 5.10.0には、Resource読込前の推論provider指定と`MaaWin32Controller`が公開されている。
- MAA v6.16.8の中国版PC対応は、既に提供されている中国PCクライアントの`明日方舟`ウィンドウを検索し、HWNDと撮影・入力方式をWin32 Controllerへ渡す。
- MAA中国版の既定撮影はFramePoolで、選択肢はFramePool、PrintWindow、ScreenDC、DesktopDupWindowである。
- MAAの日本版タイトル対応は2026-08-12時点で未マージのため、RHODESではタイトルを1件に決め打ちしない。
- MAAでは複数GPU環境の誤選択や一部GPUでの認識異常が修正対象になっている。GPUを無条件の既定値にはしない。

## 対応範囲

### GPU推論

- `自動（推奨）`、`CPU固定`、`DirectML`を選択できる。
- 既定値は`自動（推奨）`を維持する。
- DirectMLではDXGI adapter indexを明示できる。
- 推論設定は、OCR modelを含むResource bundleの読込より前に適用する。
- 保存済みFrameをCPUとDirectMLで同じ認識taskへ渡し、成功可否・認識結果・所要時間を比較できる。
- DirectMLが初期化できない、またはCPUと結果が一致しない場合は自動採用しない。
- CUDAは現在の配布runtimeに対応DLLが含まれないため、UIへ出さない。

### PCクライアント

- MaaToolkitで列挙したトップレベルウィンドウから対象を選択する。
- `アークナイツ`、`Arknights`、`明日方舟`、`명일방주`を候補として優先するが、実際の列挙結果から手動選択できる。
- 選択したHWNDを`MaaWin32Controller`へ渡す。
- 撮影方式は背景撮影、FramePool、PrintWindow、ScreenDC、DXGI windowを選択可能にする。
- Controller出力は1280x720へ正規化する。
- 先行対応では撮影・認識だけを扱う。PCクライアントの自動起動、終了、マウス、キーボード操作は行わない。
- PC版公開後に、実際のタイトル、window class、最適な撮影方式、最小化時の挙動を実機で再確認する。

## UIと保存

- ADB設定とは独立した「PC版ウィンドウ撮影」セクションを設ける。
- GPU推論設定はADB撮影とPC撮影の両方へ共通適用する。
- 設定JSONに推論provider、device index、優先window title/class、Win32撮影方式を保存する。
- 過去の設定JSONはAuto推論・日本版タイトル候補・背景撮影へ移行する。

## 成功条件

- 既存ADB接続・オフライン再認識が既定設定で従来どおり動く。
- Resource読込前に推論設定が適用される。
- PCウィンドウ列挙で空タイトルを除外し、アークナイツ候補を上位に並べる。
- PC Controllerは入力方式`None`で生成され、撮影だけを実行する。
- 1280x720の認識座標系を維持する。
- ユーザー向け本体説明・同梱ガイド・配布案内にAndroid Back/keyeventの説明を表示しない。

## 2026-08-12 保存Frame実測

- オペレーター名、秘宝一覧、基本値の3 OCR taskを、同じ保存Frameへ各3回実行し中央値を比較した。
- CPUは合計649.07ms、DirectML adapter 0は358.46ms（1.81倍）、adapter 2は374.08ms（1.74倍）だった。
- DirectML adapter 1は2051.43ms（CPU比0.32倍）で、GPU番号によって大幅に遅くなることも確認した。
- 全providerで、3 taskのOCR証拠ハッシュはCPUと一致した。
- CPU再測定は843.61msだったため、速度値は固定性能値ではなく、このPC・この保存Frameでの採用判断材料として扱う。
- Win32 runtimeのロードとトップレベルウィンドウ列挙は成功した。PCクライアントを起動していないため、ゲームウィンドウへの接続・撮影は未確認である。

## 未確認事項

- 日本版正式クライアントの実際のwindow title/classは公開後の実機確認待ち。
- GPUの速度差はPC、adapter index、認識taskによって変わるため、保存Frameの比較結果を実測値として扱う。
- PCクライアントの最小化・排他フルスクリーン時の撮影可否は公開後の実機確認待ち。
