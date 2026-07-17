# ADB設定ガイド

RHODES OBS COMMANDER3373のADB連携は、エミュレーター画面のスクリーンショット取得、タップ、スワイプをSuki/Avaloniaアプリ内のMAAFramework連携から実行します。ブラウザでHTMLを直接開いただけではADB操作はできないため、通常は配布exeまたは `npm run suki:run` から起動してください。

ADB連携は手入力を置き換えるものではなく、OCR候補を作る補助機能です。誤認識があり得るため、候補はレビューしてから反映する運用を前提にしています。

## 基本手順

1. エミュレーターでアークナイツを起動します。
2. Suki/Avaloniaアプリのランタイム/ADB設定画面を開きます。
3. `接続プリセット` を選びます。迷う場合は `自動` を選びます。
4. `ADB検出` または接続確認を実行します。
5. 候補に使用中のエミュレーターが出たら、スキャンボタンで取得します。

`ADBパス` と `serial` を手動指定した場合は、手動指定が優先されます。環境変数で指定する場合は次を使えます。

```powershell
$env:ARKNIGHTS_ADB_PATH = "C:\path\to\adb.exe"
$env:ARKNIGHTS_ADB_SERIAL = "127.0.0.1:16384"
```

## 対応プリセット

| プリセット | 主なADB候補 | 主なserial候補 | 補足 |
| --- | --- | --- | --- |
| 自動 | 保存設定、環境変数、既知インストール先、PATH | 選択プリセットに応じた候補 | まず試す設定です。 |
| MuMu Player | MuMu 5/15系の `nx_main\adb.exe`、旧MuMu 12系の `shell\adb.exe` | `127.0.0.1:16384`, `16416`, `16448`, `16480`, `16512`, `16544`, `16576` | 実行中プロセスとアンインストール情報も参照します。複数起動時はMuMu側のADB/ポート表示を確認してください。 |
| LDPlayer | `LDPlayer\LDPlayer9\adb.exe` | `emulator-5554`, `5556`, `5558`, `5560`, `127.0.0.1:5555`, `5557`, `5559`, `5561` | LDPlayer 9向けです。 |
| BlueStacks | `BlueStacks_nxt\HD-Adb.exe`, `BlueStacks_nxt\Engine\ProgramFiles\HD-Adb.exe` | `127.0.0.1:5555` など、設定ファイルのADBポート | BlueStacks側でAndroid Debug BridgeをONにしてください。Hyper-V版はポートが変わることがあります。 |
| NoxPlayer | `Nox\bin\nox_adb.exe`, `Nox\bin\adb.exe` | `127.0.0.1:62001`, `127.0.0.1:59865` | 新旧ポートを候補にします。 |
| 逍遥 / MEmu | `Microvirt\MEmu\adb.exe` | `127.0.0.1:21503` | MEmu系向けです。 |
| テンセントアプリストア | `Tencent\Androws\Application\adb.exe` | `127.0.0.1:5555` | アプリストア側でADBデバッグを有効化してください。 |
| Google Play Games 開発者 | `Google\Play Games Developer Emulator\current\emulator\adb.exe`、`Google\Play Games\current\emulator\adb.exe`、Android SDK platform-tools、PATH上のadb | `127.0.0.1:6520` | Hyper-VとGoogleログインが必要です。日本ユーザー向けの重要対応です。 |
| Android Studio AVD | `ANDROID_HOME` / `ANDROID_SDK_ROOT` / Android SDK platform-tools | AVDのadb devicesに出るserial | Android 10以降はADB inputを使います。 |
| WSA | 手動/カスタム扱い | `127.0.0.1:58526` | 現在は非推奨です。 |
| 手動 | 入力したADBパス | 入力したserial | 既知候補でうまくいかない場合に使います。 |

既知パスの探索は `Program Files`、`Program Files (x86)`、`LOCALAPPDATA`、検出可能なドライブの `Program Files` 系を見ます。MuMuは `MuMuPlayer`、`MuMuPlayerGlobal-12.0/15.0`、`YXArkNights-12.0` などの新旧配置に加えて、実行中の `MuMuNxDevice` / `MuMuPlayer` とWindowsのアンインストール情報を参照します。Google Play Gamesは同梱ADBの `Google\Play Games Developer Emulator\current\emulator\adb.exe` と `Google\Play Games\current\emulator\adb.exe` を候補にします。Android SDKは `ANDROID_HOME`、`ANDROID_SDK_ROOT`、`%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` を候補にします。

## ADB設定項目

| 項目 | 意味 | 既定 |
| --- | --- | --- |
| 自動検出 | ADB候補パスと接続済み端末を検出します。手動固定したい場合はOFFにします。 | ON |
| 接続プリセット | エミュレーター別のADBパス、serial、接続設定を選びます。 | 自動 |
| ADBパス | 使用する `adb.exe` です。空欄なら環境変数やPATHを使います。 | 空欄 |
| serial | `adb -s` に渡す接続先です。複数端末がある場合は指定してください。 | 空欄 |
| スクリーンショット拡張 | MuMuなどでスクリーンショット取得を安定させるための補助設定です。 | ON |
| 失敗時にADB serverを再起動 | 接続失敗時に `adb kill-server` / `adb start-server` を試します。 | ON |
| 失敗時に再接続 | TCP接続先の場合、ADB server再起動後に `adb connect` を試します。 | ON |
| 終了時にADBを閉じる | アプリ終了時にADBを閉じる運用向けです。通常はOFFのままで構いません。 | OFF |
| 軽量ADB | 将来の互換用設定です。通常はOFFのまま使います。 | OFF |
| 再試行回数 | ADB接続/コマンド失敗時の最大再試行回数です。 | 5 |
| 再試行待機 | 再接続を試すまでの待機時間です。 | 1000ms |

プリセットを変更すると、そのプリセットに必要な既定値が入ります。ただし、手動入力済みのADBパスは上書きしません。Google Play Games開発者プリセットでは `serial=127.0.0.1:6520`、`失敗時に再接続=ON` が入ります。

## Google Play Games 開発者エミュレーター

Google Play Games開発者エミュレーターはADB接続先が通常 `127.0.0.1:6520` です。アプリでは `Google Play Games 開発者` プリセットを選ぶと、このserialとADB再接続設定を既定で使います。

事前に確認すること:

- Google Play Games開発者エミュレーターにGoogleアカウントでログインしている。
- WindowsでHyper-V系機能が利用できる。
- Google Play Games同梱の `adb.exe`、Android SDK platform-toolsの `adb.exe`、またはPATH上の `adb` を実行できる。
- アプリのADB検出で `127.0.0.1:6520` が `device` として表示される。

手動確認例:

```powershell
adb connect 127.0.0.1:6520
adb devices -l
```

接続が不安定な場合、アプリはADB serverの再起動と再接続を最大5回まで試します。それでも失敗する場合は、Google Play Games開発者エミュレーターを再起動してから再検出してください。

## Hyper-V診断

Google Play Games開発者エミュレーター、Android Studio AVD、BlueStacks Hyper-V版などでは、Windowsの仮想化設定が必要になる場合があります。アプリはWindows環境でHyper-V診断を実行し、状態に応じて次を表示します。

| 状態 | 対応 |
| --- | --- |
| Hyper-V/Windows Hypervisorが有効 | Google Play Games開発者エミュレーターを利用できます。 |
| BIOS/UEFIのCPU仮想化支援が無効 | BIOS/UEFIでIntel VT-xまたはAMD-V/SVMを有効にしてください。 |
| CPU仮想化支援は有効だがWindows Hypervisorが起動していない | Windowsの機能で `Hyper-V`、`仮想マシンプラットフォーム`、`Windows Hypervisor Platform` を有効にしてください。 |
| Windows以外 | Hyper-V診断はできません。 |

BIOS/UEFI設定はPCメーカーごとに名前が異なります。`Intel Virtualization Technology`、`VT-x`、`AMD-V`、`SVM Mode` などの項目を有効にします。

## BlueStacksのポート検出

BlueStacksはインスタンスごとにADBポートが変わることがあります。アプリは次の設定ファイルを読み、`adb_port` をserial候補に追加します。

```text
%ProgramData%\BlueStacks_nxt\bluestacks.conf
%ProgramData%\BlueStacks_nxt_cn\bluestacks.conf
```

場所が違う場合は次の環境変数で設定ファイルを明示できます。

```powershell
$env:ARKNIGHTS_BLUESTACKS_CONFIG_PATH = "D:\ProgramData\BlueStacks_nxt\bluestacks.conf"
```

## スクリーンショットと操作

スクリーンショットはまず次で取得します。

```text
adb exec-out screencap -p
```

PNGとして読めない場合は、次の方式へフォールバックします。

```text
adb shell screencap -p
```

ADB操作はAndroid Back keyeventを使わず、ゲーム内ボタンのタップや同一ボタン再タップで閉じます。タップとスワイプは指定矩形内でランダムにずらして実行します。

## デバッグログとスクリーンショット

デバッグ用ビルドでは、ログとADBスクリーンショットを実行ファイルの近くに作ります。報告時は次を添付してください。

```text
RHODES OBS COMMANDER3373 Debug Logs
RHODES OBS COMMANDER3373 Debug Logs\ADB Screenshots
```

通常ビルドでは、初回に選んだ保存先のデータフォルダ内に状態、ADB作業ファイル、認識ログが保存されます。

```text
RHODES OBS COMMANDER3373 Data\state\recognition-logs
RHODES OBS COMMANDER3373 Data\state\adb-work
RHODES OBS COMMANDER3373 Data\state\adb-screenshots
```

## よくある問題

### ADB executable was not found

`adb.exe` が見つかっていません。Android platform-toolsをインストールし、PATHに追加するか、アプリの `ADBパス` または `ARKNIGHTS_ADB_PATH` で明示してください。

### ADB device was not found

エミュレーターが起動していない、ADBデバッグが無効、またはserialが違います。プリセットを選び直し、`adb devices -l` に対象が出るか確認してください。

### ADB device is offline

エミュレーター側のADB接続が壊れています。アプリの再検出、ADB server再起動、エミュレーター再起動の順で試してください。

```powershell
adb kill-server
adb start-server
adb connect 127.0.0.1:6520
```

### Multiple ADB devices were found

複数の端末やエミュレーターが見えています。アプリの `serial` に対象を指定してください。

### Google Play Games開発者エミュレーターにつながらない

`Google Play Games 開発者` プリセットを選び、serialが `127.0.0.1:6520` になっているか確認してください。Hyper-V診断でBIOS/Windows機能の不足が出ている場合は、先に仮想化設定を直してください。

## MAA由来の設定

ADB候補、serial候補、接続リトライの考え方はMaaAssistantArknightsの接続設定を参考にしています。実装上の由来はソース内コメントと `THIRD_PARTY_NOTICES.md` に残しています。
