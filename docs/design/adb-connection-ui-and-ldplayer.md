# ADB接続UI・同梱ガイド・LDPlayer接続強化仕様

## 目的

ADBプロファイルの適用操作と上書き範囲をUI上で明確にし、接続設定を説明するオフラインHTMLを配布物へ同梱する。
MuMu 12に加えて、MaaFrameworkが対応するLDPlayer 9の高速・ロスレス撮影を選択可能にする。

## 前提と判断

- 「プロファイルを適用」は、選択したプロファイルのADB実行ファイル、serial、推奨撮影方式、推奨入力方式を現在の編集値へ反映する操作である。
- 適用だけでは設定ファイルへ保存しない。永続化は既存の「保存」で行う。
- 適用ボタンはプロファイルComboBoxの直右へ配置し、上書き操作であることをラベルとToolTipで明示する。
- HTMLガイドはアプリの `docs/adb-connection-settings.html` として同梱し、OS既定ブラウザーで開く。
- MaaFrameworkの公式Control Methodsでは、LDPlayer 9は `AdbScreencapMethod.EmulatorExtras` に対応する。
- 同じ公式表で `AdbInputMethod.EmulatorExtras` の対応はMuMu 12等に限定され、LDPlayerは含まれない。そのためLD高速タッチは提供しない。
- Android Back/keyeventは禁止を維持する。

## 対応範囲

### プロファイルUI

- プロファイル選択と「選択内容で上書き」ボタンを同じ行へ置く。
- 従来の上部「適用」ボタンは削除する。
- ADB参照、候補使用、再検出は従来どおり独立操作とする。

### 同梱HTML

- プロファイル適用範囲
- ADBパスとserial
- 撮影・入力方式
- MuMu高速撮影、高速タッチ、ネットワークブリッジ
- LDPlayer高速撮影とinstance
- LDPlayerではEmulatorExtras入力を使わない理由
- フォールバック、接続回復、危険操作、トラブルシュート
- 公式MAA資料へのリンク

### LDPlayer接続強化

- LDPlayerプロファイル選択時だけLD用設定を表示する。
- 高速撮影ONでは `EmulatorExtras | configured fallback` を渡す。
- `extras.ld` へ `enable`, `path`, `index` を設定する。
- MaaToolkitが端末を検出できた場合は、同Toolkitが返すVBox PIDを保持する。
- LD用ライブラリーまたは `ldconsole.exe` を事前確認できない場合は高速撮影を有効化せず、選択したロスレス撮影へ戻す。
- 入力方式は上部のMAA接続方式を維持し、LD向けEmulatorExtras入力へ変更しない。

## 設定契約

`SukiAdbConnectionSettings` をschemaVersion 2へ更新し、次を追加する。

- `LdPlayerScreenshotEnhancementEnabled: bool`
- `LdPlayerInstanceIndex: int`（0..127）

既存schemaVersion 1は既定値を補って読み込む。
既存MuMu設定の意味と保存名は変更しない。

## ファイル

- UI: `apps/rhodes-suki/Views/Workspaces/RuntimeWorkspaceView.axaml`
- ViewModel: `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs`
- 設定: `apps/rhodes-suki/Models/SukiAdbConnectionSettings.cs`
- LD能力確認: `apps/rhodes-suki/Services/RhodesLdPlayerCapabilityDetector.cs`
- MAA方式解決: `apps/rhodes-suki/Services/RhodesMaaAdbOptionPolicy.cs`
- 同梱ガイド: `docs/user/adb-connection-settings.html`
- 配布設定: `apps/rhodes-suki/RhodesSuki.csproj`
- 回帰試験: `tests/rhodes-suki/Program.cs`

## テスト戦略

- 設定schema 1から2への移行とLD instance範囲を単体試験する。
- LDルート、ライブラリー、`ldconsole.exe` の能力判定を単体試験する。
- LD高速撮影がEmulatorExtrasとフォールバックを併用し、入力へEmulatorExtrasを追加しないことを単体試験する。
- Toolkit由来の `extras.ld.pid` を上書きしないことを試験する。
- Runtime XAML内で適用ボタンがプロファイルComboBoxと同じGridにあり、旧位置に残らないことをソース契約試験する。
- HTMLの見出し、MuMu/LDの説明、安全方針、公式リンクとcsproj同梱指定をソース契約試験する。

## 境界

- 常に行う: 入力値正規化、ロスレス撮影フォールバック、既存設定移行、キーボード操作可能なButton、オフライン閲覧。
- 今回行わない: MaaFramework依存更新、LDPlayerの起動や実機接続、ゲーム画面操作、EXE/ZIP生成、commit/push。
- 禁止: LDPlayerで未対応のEmulatorExtras入力を有効扱いすること、Android Back/keyevent、既存の未コミット変更を破棄すること。

## 受け入れ条件

- 適用ボタンがプロファイル欄の直右にあり、上書き対象が分かる。
- 同梱HTMLをUIから開け、公開ビルドにもコピーされる。
- LDPlayerプロファイルで高速撮影、instance、撮影フォールバックを設定できる。
- LDPlayerの高速入力は非対応と明示され、MAA入力方式を破壊しない。
- MuMu既存機能とschema 1設定が回帰しない。
- 対象試験、全体試験、Avaloniaビルド、MAA契約、`git diff --check` が成功する。

## 参照

- https://maafw.com/en/docs/2.4-ControlMethods/
- https://maafw.com/en/docs/2.2-IntegratedInterfaceOverview/
