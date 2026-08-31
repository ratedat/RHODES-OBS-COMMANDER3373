# MAA GPU推論・PCクライアント対応仕様

## 目的

RHODES OBS COMMANDER3373のMAA-OCRを、既定の安定性を維持したままDirectMLで比較検証できるようにする。
また、Androidエミュレーターとは別に、アークナイツPCクライアントのウィンドウを選択し、既存の取得プロファイルをタップ・スワイプ・撮影・認識まで実行できるようにする。

## 根拠として確認した現状

- MaaFramework 5.13.0-beta.5のWindows runtimeには`DirectML.dll`と`MaaWin32ControlUnit.dll`が含まれる。
- C# binding 5.13.0-preview.1には、Resource読込前の推論provider指定、`MaaWin32Controller`、`AnchoredTouch`が公開されている。
- MAA v6.16.8の中国版PC対応は、既に提供されている中国PCクライアントの`明日方舟`ウィンドウを検索し、HWNDと撮影・入力方式をWin32 Controllerへ渡す。
- MAA中国版の既定撮影はFramePoolで、選択肢はFramePool、PrintWindow、ScreenDC、DesktopDupWindowである。
- MAA v6.16.8の既定入力は、マウスがSendMessageWithCursorPos、キーボードがSendMessageである。
- MAAの日本版タイトル対応は開発branchへ追加済みだがv6.16.8には含まれないため、RHODESではタイトルを1件に決め打ちしない。
- MAAでは複数GPU環境の誤選択や一部GPUでの認識異常が修正対象になっている。GPUを無条件の既定値にはしない。

## 2026-08-31 MAA更新方針

- PC版取得の最適化を優先するユーザー方針に基づき、C# binding `Maa.Framework 5.13.0-preview.1`とWindows runtime `Maa.Framework.Runtimes 5.13.0-beta.5`を検証用参照へ採用する。
- beta.4で追加されたWin32 WindowPos入力修正と、beta.5で追加された`AnchoredTouch`を利用可能にする。開発版であるため、依存更新ごとに契約テスト、全Service test、PC実機取得を再確認する。
- `AnchoredTouch`はカーソルを動かさないタップ／スワイプ方式として選択可能にするが、実機取得を完走するまでは既定値にせず、確認済みの`SendMessageWithCursorPos`を維持する。
- 認識規則とオペレーター名は、プロジェクト設定どおりMaaAssistantArknightsの`dev-v2`から同期する。
- `third_party/maa/resource/tasks/tasks.json`の同期はOCR規則だけを抽出した差分ではなく、掉線処理や基地関連など3373が使用しない上流task定義も含む広範囲同期である。
- 3373が実行するのは、ローカル認識定義から生成して契約検証した`rhodes-generated.json`のtaskである。上流の共通`tasks.json`をPC版取得経路から直接実行しない。
- 将来、上流共通taskを実行経路へ追加する場合は、同期更新とは分けて挙動・安全方針・回帰試験を再審査する。

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
- 既定撮影方式は、MAA v6.16.8と実機の完全Frame取得結果に合わせてFramePoolとする。
- マウス入力方式はSendMessageWithCursorPos、AnchoredTouch、SendMessageWithWindowPos、Seizeを選択可能にし、既定をSendMessageWithCursorPosとする。
- キーボード入力方式はSendMessage、PostMessage、Seizeを選択可能にし、既定をSendMessageとする。ただしRHODESの取得処理からキー操作は発行しない。
- Controller出力は1280x720へ正規化する。
- 既存の認識プロファイルが定義する矩形内ランダムタップとランダムスワイプだけを、ADBと同じ取得経路で実行する。
- PCクライアントの自動起動、終了、キーボード操作、戦闘操作、認識プロファイル外の入力は行わない。
- 日本版実機で確認したtitle/classを既定候補にし、最小化時や排他フルスクリーン時の挙動は別途検証する。

## UIと保存

- 取得接続先としてADBとPC版を明示選択でき、過去設定との互換性のため既定はADBとする。
- ADB設定とは独立した「PC版ウィンドウ接続・操作」セクションを設ける。
- GPU推論設定はADB撮影とPC撮影の両方へ共通適用する。
- 設定JSONに接続先、推論provider、device index、優先window title/class、Win32撮影・マウス・キーボード方式を保存する。
- 過去の設定JSONはADB接続・Auto推論・日本版タイトル候補・FramePool・MAA既定入力方式へ移行する。

## 実装構造とコマンド

- 設定モデルと方式catalog: `apps/rhodes-suki/Models/SukiMaaRuntimeSettings.cs`
- ウィンドウ選択と接続plan: `apps/rhodes-suki/Services/RhodesMaaDesktopWindowCatalog.cs`
- MaaFramework Controller: `apps/rhodes-suki/Services/RhodesMaaSession.cs`
- 接続先の選択、再接続、取得フロー: `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs`
- UI: `apps/rhodes-suki/Views/Workspaces/RuntimeWorkspaceView.axaml`
- 小さい契約テスト: `dotnet run --project tests/rhodes-suki/RhodesSuki.ServiceTests.csproj`
- Node契約テスト: `node --test tests/suki-maaframework-shell.test.mjs`
- Build: `dotnet build apps/rhodes-suki/RhodesSuki.csproj`

方式IDは保存互換性のため小文字kebab-caseとし、MaaFramework enumへの変換はcatalogと接続policyへ閉じ込める。

```csharp
var plan = RhodesMaaPcConnectionPolicy.Resolve(screencapId, mouseId, keyboardId);
```

設定変更後は既存Controllerを再利用せず、次の取得時に選択済み方式で接続し直す。

## 成功条件

- 既存ADB接続・オフライン再認識が既定設定で従来どおり動く。
- Resource読込前に推論設定が適用される。
- PCウィンドウ列挙で空タイトルを除外し、アークナイツ候補を上位に並べる。
- PC ControllerはFramePool、SendMessageWithCursorPos、SendMessageを既定として生成される。
- PC接続を選択した状態で「取得して反映」を実行すると、ADBへ切り替わらずPC Controllerで接続する。
- PC Controllerから実行できる操作は、既存認識プロファイルの矩形内ランダムタップとランダムスワイプに限る。
- 接続・撮影・入力方式の変更は再接続後に反映される。
- 1280x720の認識座標系を維持する。
- 現在の日本版PCクライアントのサルカズマップから、基本値・固有値・オペレーター・秘宝の取得と状態反映を完走できる。
- ユーザー向け本体説明・同梱ガイド・配布案内にAndroid Back/keyeventの説明を表示しない。

## 2026-08-12 保存Frame実測

- オペレーター名、秘宝一覧、基本値の3 OCR taskを、同じ保存Frameへ各3回実行し中央値を比較した。
- CPUは合計649.07ms、DirectML adapter 0は358.46ms（1.81倍）、adapter 2は374.08ms（1.74倍）だった。
- DirectML adapter 1は2051.43ms（CPU比0.32倍）で、GPU番号によって大幅に遅くなることも確認した。
- 全providerで、3 taskのOCR証拠ハッシュはCPUと一致した。
- CPU再測定は843.61msだったため、速度値は固定性能値ではなく、このPC・この保存Frameでの採用判断材料として扱う。
- Win32 runtimeのロードとトップレベルウィンドウ列挙は成功した。

## 2026-08-13 日本版PCクライアント実測

- ゲーム本体は`Arknights.exe`、window titleは`アークナイツ`、window classは`UnityWndClass`だった。
- ランチャーにも同じtitleの非表示`Chrome_WidgetWin_1`があるため、class未指定時も`UnityWndClass`を優先する。
- 2560x1440で表示中のゲーム本体へ接続し、FramePool、DXGI Window、ScreenDCで完全な1280x720 Frameを保存できた。
- 背景撮影の組み合わせ方式は成功扱いでも下部メニューが欠落したため、既定値には採用しない。
- 入力方式`None`はnative logで`Unknown input method: 0`となり、取得操作には使用できないことを確認した。
- 実装前のサルカズの保存FrameをDXGI Windowで認識し、源石錐20、構想2、位置測定分隊を取得できた。
- 実装後はFramePool、SendMessageWithCursorPos、SendMessageの経路で一括取得を2回完走した。初回48.1秒、最終確認43.45秒で、源石錐23、構想4、難易度18、位置測定分隊、オペレーター5名、秘宝1件、思考5件を状態へ反映した。
- 秘宝0件ではボタン自体が無効になる。保存済みの0件Frameと現在の1件Frameを入力なしで比較し、マップ上のオペレーター人数と秘宝サムネイル領域から、それぞれ`Empty`と`HasOwnedRelics`へ分離できた。
- 秘宝件数付近のOCRは、1件を`.`、0件を`,C`などと読むため、0件確定の根拠には使わない。画像を復号できない場合も`Unknown`として既存状態を保持する。

## PC版取得の高速化目標

- 最終目標はPC版の一括取得を精度維持のまま短縮し、操作待ちを感じにくい時間へ収めることである。2026-08-13のIS#5実測43.45秒を旧基準値とし、現行コードは次の実機取得で改めて測定する。
- 認識証跡にはプロファイル総時間、MAA task合計時間、両者の差分を保存する。差分には画面遷移、撮影、スクロール、候補変換、証跡保存が含まれるため、総時間だけでOCRを原因と決めつけない。
- 精度を落とさない高速化として、認識定義のメモリキャッシュ、所持数一致による早期終了、FramePool撮影、実測済み物理GPUを指定したDirectMLを優先する。
- `runStatusFull`の分隊アイコン照合は、現在選択中のISに属するtemplateだけを実行する。共通の分隊名・説明OCRは維持し、低信頼時の再撮影も従来どおり行う。別ISのtemplate結果は候補変換時に除外されていたため、実行前に絞ることで結果を変えずに無駄な照合を省く。
- 人数や件数だけをキャッシュ判定に使わない。同数でも昇進状態、アーミヤ職分、秘宝の交換、スタック数が変わるため、将来の結果キャッシュは対象画面の視覚fingerprintと認識プロファイル版を組み合わせる。
- MAA taskの並列実行、待機時間の短縮、スクロール省略は、Tasker/Controllerの直列性と画面アニメーションを実機で確認してから採用する。未検証の短縮を既定値へ入れない。

## 未確認事項

- GPUの速度差はPC、adapter index、認識taskによって変わるため、保存Frameの比較結果を実測値として扱う。
- PCクライアントの最小化・排他フルスクリーン時の撮影可否は未確認である。
- 管理者権限で起動されたゲームへ非管理者のRHODESから入力できるかは保証せず、権限差がある場合は明示して入力を開始しない。

## 一時的なリモートデバッグ運用

- これは製品機能ではなく、PC版デバッグ期間だけの運用とする。
- ゲームと検証用3373を同じWindows対話sessionで起動し、ゲームが管理者権限の場合は3373も管理者として起動する。
- 画面操作と取得は既存の3373 UIとMaaWin32Controllerだけで行い、別の遠隔入力API、IPCブリッジ、任意座標操作は追加しない。
- Codex側は3373が保存したFrame、認識証跡、ログを読み取り、必要なコード修正と再検証を行う。
- 操作が必要な場面だけユーザーが3373上で取得を開始し、入力不要の保存Frame再認識はCodex側で継続する。
