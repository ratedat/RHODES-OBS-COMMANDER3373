# RHODES OBS COMMANDER3373 起動ガイド

この文書は、配信・大会運用・開発デバッグでアプリをどう起動するかをまとめたものです。

## まず使う人向け

配布版がある場合は、次の exe を起動します。

```text
dist\RHODES OBS COMMANDER3373-0.1.0-x64.exe
```

展開済みディレクトリから直接起動する場合は、次の exe でも起動できます。

```text
dist\win-unpacked\RHODES OBS COMMANDER3373.exe
```

初回起動時に、設定とスクリーンショットの保存先を選びます。迷った場合は `実行ファイル側に保存（推奨）` を選んでください。配布exeの隣に `RHODES OBS COMMANDER3373 Data` フォルダを作るため、アンインストールや削除がしやすくなります。

その後、ローカルサーバーのポート番号を聞かれます。通常は既定値の `5173` のままで問題ありません。OBS Browser Source のURLもこのポート番号を使います。

## ソースフォルダから起動する場合

コマンドを打たずに起動したい場合は、リポジトリ直下の次のファイルをダブルクリックします。

```text
start-windows.vbs
```

初回のみ、依存関係が不足していれば自動で `npm install` が走ります。完了後にデスクトップアプリが起動します。

この起動方法では、古い `5173` / `5174` / `5200` のローカルサーバーが残っている場合に停止してから起動します。

## 開発・デバッグ用の起動

PowerShellで作業する場合は、まずリポジトリへ移動します。

```powershell
cd O:\Arknights_Rogue_OBSTool
```

Control v2をElectronアプリで開く標準デバッグ起動です。

```powershell
npm.cmd run app:debug
```

内部的には `ARKNIGHTS_STATE_DIR=data` を使い、`http://127.0.0.1:5173/control-v2` を開きます。人間デバッグはブラウザ単体ではなく、基本的にこのElectronアプリ側で行います。

ポートを明示して起動したい場合は、次の形を使います。

```powershell
npm.cmd run app -- --view control-v2 --port 5173
```

Electronを使わず、ローカルWebサーバーだけ起動したい場合は次です。

```powershell
npm.cmd run dev
```

この場合はブラウザで各URLを開きます。

## よく使う画面URL

既定ポート `5173` の例です。起動時に別ポートを選んだ場合は、URL内の `5173` を置き換えてください。

| 用途 | URL |
| --- | --- |
| Control | `http://127.0.0.1:5173/control-v2` |
| Sidecar | `http://127.0.0.1:5173/sidecar` |
| OBS overlay | `http://127.0.0.1:5173/overlay` |

`/control` は旧ブックマーク互換用で、現在はControl v2と同じ画面を開きます。

Controlは画面を分けて開けます。

```text
http://127.0.0.1:5173/control-v2?screen=common
http://127.0.0.1:5173/control-v2?screen=operators
http://127.0.0.1:5173/control-v2?screen=relics
http://127.0.0.1:5173/control-v2?screen=special
http://127.0.0.1:5173/control-v2?screen=obs
```

## OBS Browser Source用URL

通常のまとめ表示です。

```text
http://127.0.0.1:5173/overlay
```

レイアウト別表示です。

```text
http://127.0.0.1:5173/overlay?layout=vertical&size=small
http://127.0.0.1:5173/overlay?layout=vertical&size=medium
http://127.0.0.1:5173/overlay?layout=vertical&size=large
http://127.0.0.1:5173/overlay?layout=horizontal&size=small
http://127.0.0.1:5173/overlay?layout=horizontal&size=medium
http://127.0.0.1:5173/overlay?layout=horizontal&size=large
http://127.0.0.1:5173/overlay?layout=full
```

OBS上でパーツを自由配置したい場合は、分割パーツURLを個別のBrowser Sourceにします。

```text
http://127.0.0.1:5173/overlay/part/status
http://127.0.0.1:5173/overlay/part/relics
http://127.0.0.1:5173/overlay/part/operators
http://127.0.0.1:5173/overlay/part/effects
http://127.0.0.1:5173/overlay/part/bosses
http://127.0.0.1:5173/overlay/part/special
```

## ADB連携作業時の起動

ADB連携の確認も、基本はElectronアプリを起動して行います。

```powershell
cd O:\Arknights_Rogue_OBSTool
npm.cmd run app:debug
```

MAAFramework取得はブラウザのHTMLだけでは端末操作できません。アプリ内のローカル実行環境がADB接続、スクリーンショット、MAA-OCRを担当します。

対応エミュレーター、Google Play Games開発者エミュレーター、Hyper-V診断、標準serial、トラブルシュートは `docs/adb-setup.md` を参照してください。

ADB実行ファイルが見つからない場合は、Android platform-toolsをPATHに入れるか、Control v2のADB設定でパスを選択します。環境変数で明示する場合は次を使えます。

```powershell
$env:ARKNIGHTS_ADB_PATH = "C:\path\to\adb.exe"
$env:ARKNIGHTS_ADB_SERIAL = "127.0.0.1:16384"
```

現在のプリセットは `自動`、`MuMu Player`、`LDPlayer`、`BlueStacks`、`NoxPlayer`、`逍遥 / MEmu`、`テンセントアプリストア`、`Google Play Games 開発者`、`Android Studio AVD`、`WSA`、`手動` です。通常は `自動` から試し、Google Play Games開発者エミュレーターでは `Google Play Games 開発者` を選びます。

取得対象は `源石錐`、`等級`、`分隊`、`IS毎の特殊値`、`オペレーター`、`秘宝` です。希望、耐久値、シールド、指揮Lvは取得対象外です。

認識の実行はSuki/Avalonia側の認識ワークフローから行います。Control v2は手入力、候補確認、OBS表示設定を担当します。

取得結果は即座にOverlayへ反映せず、候補としてレビュー待ちに入れる設計です。誤認識や誤取得があり得るため、承認導線を通して反映する方針です。

## 検証・ビルド

テストだけ実行します。

```powershell
npm.cmd test
```

Electronの起動確認だけ行います。

```powershell
npm.cmd run app:smoke
```

テストとElectronディレクトリビルドをまとめて行います。

```powershell
npm.cmd run verify:desktop
```

配布用portable exeを作ります。

```powershell
npm.cmd run dist:win
```

## 保存先と状態ファイル

通常起動では、初回に選んだ保存先へ設定・入力状態・ADBスクリーンショットを保存します。

| 保存先 | 用途 | 既定パス |
| --- | --- | --- |
| 実行ファイル側（推奨） | 配布exeと一緒に消せるポータブル運用 | `RHODES OBS COMMANDER3373 Data` |
| ドキュメント | ユーザーのDocuments配下にまとめる運用 | `Documents\RHODES OBS COMMANDER3373` |

保存先は、アプリ上部メニューの `操作` → `保存先設定` から変更できます。変更後は再起動すると反映されます。

開発起動の `npm.cmd run app:debug` / `npm.cmd run app:smoke` は、検証を安定させるため内部的に `ARKNIGHTS_STATE_DIR=data` を使います。この場合、主な状態ファイルは次です。

```text
data/current-state.json
```

状態ファイルとスクリーンショットはGit管理対象外です。リセット操作をすると、現在のラン入力状態が初期化されます。リセット後に表示や選択状態が変に見える場合は、まずアプリの再読み込み、それでも直らなければアプリ再起動を行ってください。

## よくある問題

### OBSに何も出ない

OBS Browser Sourceのポート番号が、アプリ起動時に選んだポートと一致しているか確認します。

### ポートが競合して起動しない

起動時のポート選択で `5174` や `5200` など別ポートを選びます。OBS側URLのポート番号も同じ値へ変更します。

### ADB executable was not found と出る

ADBがPATHにない状態です。Android platform-toolsをインストールするか、`ARKNIGHTS_ADB_PATH` に `adb.exe` の場所を設定します。

### 二重起動した

通常は単一起動ガードにより既存ウィンドウが前面に出ます。もし古いサーバーだけが残っている場合は、`start-windows.vbs` から起動すると既知ポートの古いサーバーを止めてから起動します。
