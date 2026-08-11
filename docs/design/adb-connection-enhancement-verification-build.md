# ADB接続強化の検証用ビルド仕様

## 目的

RHODES OBS COMMANDER3373のSuki/Avalonia版へ、MuMu 12の高速撮影と高速タッチを含むADB接続設定、能力判定、診断、段階回復を追加する。

対象は開発者本人が使う検証用ビルドである。
設定画面から実行条件とフォールバック理由を確認でき、危険な回復処理は明示操作なしでは実行されない状態を完成条件とする。

MAA v6.16.7の接続設定は挙動の参照元として扱う。
MAAのソースや資産はコピーせず、MaaFramework v5.12.3の公開APIとRHODES固有のサービスで同等機能を組み立てる。

## 技術構成

- デスクトップUI：Avalonia、SukiUI、.NET 8
- ADB Controller：Maa.Framework 5.10.0
- Native runtime：Maa.Framework.Runtimes 5.12.3
- 設定保存：`user-data/suki-settings.json`
- 単体および統合テスト：`tests/rhodes-suki/RhodesSuki.ServiceTests.csproj`
- 検証用成果物：既存のportable publish導線から、公開デバッグ版と別名で生成する

## 実行コマンド

```powershell
Set-Location -LiteralPath 'O:\Arknights_Rogue_OBSTool'

dotnet run --project tests/rhodes-suki/RhodesSuki.ServiceTests.csproj
npm test
npm run maa:check
npm run suki:build
npm run suki:publish:portable
```

検証用成果物はportable出力を別ディレクトリへ複製して作成する。
公開デバッグZIPの生成スクリプトは使用しない。

## 実装範囲

### 型付き設定と移行

ADB設定へスキーマバージョン付きの型を追加する。
旧設定のトップレベル値は読み込み互換のため残し、読み込み時に新設定へ移行する。

保存対象は次のとおりとする。

- 自動検出と毎回自動検出
- エミュレーターパス
- MuMu高速撮影とMuMu高速タッチ
- MuMuネットワークブリッジ、インスタンス番号、ゲームpackage、クローン番号
- 撮影方式と入力フォールバック方式
- 再接続回数と待機時間
- ADB server再起動、ADBプロセス再起動、エミュレーター起動
- 管理ADBの使用、終了時ADB停止、軽量ADBの互換表示

### MuMu能力判定

MuMuルートは、明示したエミュレーターパス、ADBパス、実行中プロセスの順で解決する。

能力判定は次の現物を確認する。

- `MuMuManager.exe`
- `external_renderer_ipc.dll`
- `MuMuManager.exe version` が返すバージョン
- MuMu高速タッチの最低バージョン6.3.2.0
- 選択したserialから求めたインスタンス番号

バージョンまたはIPC DLLを確認できない場合、高速タッチを有効な方式としてControllerへ渡さない。
高速撮影もIPC DLLがない場合は通常撮影へ戻す。

### 撮影と入力方式

MuMu高速撮影とMuMu高速タッチは別々に有効化できる。

高速タッチが使えない場合はMiniTouchへ戻す。
利用者が別の入力フォールバック方式を選んだ場合は、その選択を優先する。

設定JSONにはMuMuルート、インスタンス番号、ゲームpackage、クローン番号を反映する。
手入力の高度なJSONは保持するが、型付き設定が所有するキーは型付き設定を正とする。

### 接続検証

スクリーンショットベンチマークは複数回撮影し、成功数、最小、平均、最大時間、画像サイズを表示する。

タッチテストは利用者が1280x720内の矩形を入力し、確認画面で承認した場合だけ実行する。
実行点は指定矩形内でランダム化する。
Android Back、keyevent、座標未指定のタップは実行しない。

### 段階回復

Controller接続に失敗した場合、設定に応じて次の順で回復する。

1. 待機後に再接続する。
2. 選択中ADBで`kill-server`と`start-server`を実行する。
3. 明示的に有効化された場合だけ、同じ実行ファイルのADBプロセスを終了する。
4. 明示的に有効化された場合だけ、設定済みエミュレーターを1回起動する。

一度の接続操作で同じ回復段階を繰り返さない。
エミュレーター起動はゲーム起動を伴わない。

### 高度な設定

「ADB強制置換」は、エミュレーターのADBを上書きする処理として実装しない。
Googleの公式Platform ToolsをRHODESの管理領域へダウンロードし、SHA-256を検証してから選択可能にする。

ADBプロセス再起動と終了時ADB停止は既定で無効にする。
実行対象は、正規化した実行ファイルパスが選択中ADBと一致するプロセスに限定する。

軽量ADBはMaaFrameworkの公開APIに1対1の機能がないため、互換性情報として表示する。
このビルドでは有効化できず、設定を無言で受理しない。

## コード配置

- `Models`：設定、能力判定、ベンチマーク結果の不変データ
- `Services`：設定移行、MuMu能力判定、ADBコマンド、回復ポリシー、管理ADB導入
- `ViewModels`：UI状態、明示確認、サービスの組み合わせ
- `Views/Workspaces`：通常設定と高度な設定の表示
- `tests/rhodes-suki`：副作用を差し替えた単体および統合テスト

## コード規約

副作用を持つ処理はデリゲートまたは小さなサービス境界へ隔離する。
テストでは実ADB、実MuMu、外部ダウンロード、実プロセス終了を使わない。

```csharp
var result = await RhodesAdbRecoveryService.ConnectAsync(
    settings,
    adbPath,
    connectAsync: fakeConnect,
    runAdbCommandAsync: fakeAdb,
    startEmulatorAsync: fakeEmulator,
    killAdbProcessAsync: fakeKill,
    destructiveActionsConfirmed: true,
    cancellationToken: cancellationToken);
```

## テスト方針

- 旧設定から新設定への移行と将来schemaの拒否
- MuMuルート、バージョン、IPC DLL、インスタンス番号の能力判定
- 高速方式とフォールバック方式のController設定
- ベンチマーク集計と失敗混在時の結果
- 安全タップ矩形の境界検証とランダム化
- 回復段階の順序、回数制限、既定OFF
- 管理ADBのSHA-256不一致とZIP traversal拒否
- 選択中ADB以外のプロセスを終了しないこと
- C#とNodeの両経路でAndroid Back、keyevent、`ClickKey`を使用しないこと

## 境界

常に行うことは、安全な既定値、入力値検証、操作ログ、フォールバック理由の表示、既存設定の移行である。

利用者の確認を必要とすることは、タッチテスト、ADBプロセス終了、終了時ADB停止、管理ADBのダウンロードと切替である。

実行しないことは、エミュレーター同梱ADBの上書き、Android Back、keyevent、ゲームの自動起動、無人操作、指定外プロセスの終了である。

## 完了条件

- 1から6までの設定と操作がRuntimeワークスペースに表示される。
- 旧`user-data/suki-settings.json`を失わずに新schemaへ読み替えられる。
- MuMu高速タッチの可否とフォールバック理由が画面に表示される。
- ベンチマークと明示確認付きタッチテストが実行できる。
- 段階回復は有効にした段階だけを上限回数内で実行する。
- 高度な操作は既定OFFで、対象を限定し、確認前には実行されない。
- MAA contract、Nodeテスト、Sukiテスト、Sukiビルドが成功する。
- 公開デバッグ版とは別の検証用成果物が生成される。

## 未確定事項

軽量ADBはMaaFramework側に対応APIが追加された場合だけ有効化を再検討する。
現時点では無効項目として理由を表示する。
