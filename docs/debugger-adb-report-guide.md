# デバッグ版 ADB/OCR 報告ガイド

この文書は、デバッグ版ZIPでADB/OCR検証を行う報告者向けの短い手順です。詳しいADB設定は `docs/adb-setup.md` を参照してください。

## 1. 起動するファイル

ZIP内の次のファイルを起動します。

```text
RHODES OBS COMMANDER3373-0.1.0-x64-debugger.exe
```

初回起動で保存先を聞かれた場合は、迷ったら `実行ファイル側に保存` を選んでください。ログとスクリーンショットを実行ファイルの近くにまとめやすくなります。

## 2. ADB接続を確認する

Control v2の支援/ADB設定画面で、使用しているエミュレーターに合わせてプリセットを選びます。

| 環境 | 推奨プリセット | 代表的なserial |
| --- | --- | --- |
| MuMu Player | MuMu Player | `127.0.0.1:16384` |
| BlueStacks | BlueStacks | 設定ファイルのADBポート、または `127.0.0.1:5555` |
| LDPlayer | LDPlayer | `emulator-5554` など |
| NoxPlayer | NoxPlayer | `127.0.0.1:62001` |
| MEmu / 逍遥 | 逍遥 / MEmu | `127.0.0.1:21503` |
| Google Play Games開発者 | Google Play Games 開発者 | `127.0.0.1:6520` |
| Android Studio AVD | Android Studio AVD | `adb devices -l` に出るserial |

`ADB検出` を実行し、対象が `device` として表示されることを確認してからスキャンしてください。複数端末が出る場合は、対象のserialを明示してください。

## 3. Google Play Games開発者エミュレーターの場合

Google Play Games開発者エミュレーターは、通常 `127.0.0.1:6520` へ接続します。

確認すること:

- Google Play Games開発者エミュレーターにGoogleアカウントでログインしている。
- WindowsのHyper-V系機能が有効。
- Android SDK platform-toolsの `adb.exe` が使える。
- アプリのHyper-V診断でBIOS/UEFI仮想化やWindows機能の不足が出ていない。

手動確認例:

```powershell
adb connect 127.0.0.1:6520
adb devices -l
```

## 4. スキャン時の注意

- ゲーム画面は16:9の横画面で表示してください。
- ADB操作ではAndroid Back keyeventを使いません。アプリはゲーム内ボタンのタップで開閉します。
- タップ/スワイプ座標は固定点ではなく、指定範囲内でランダムにずらして実行します。
- OCR候補は誤認識することがあります。候補が変な場合は、そのままログとスクリーンショットを送ってください。

## 5. 報告時に添付してほしいもの

デバッグ版では、実行ファイルの近くに次のフォルダが作られます。

```text
RHODES OBS COMMANDER3373 Debug Logs
RHODES OBS COMMANDER3373 Debug Logs\ADB Screenshots
```

報告時は、できれば次を添付してください。

| ファイル/フォルダ | 内容 |
| --- | --- |
| `RHODES OBS COMMANDER3373 Debug Logs` | アプリのデバッグログ |
| `RHODES OBS COMMANDER3373 Debug Logs\ADB Screenshots` | ADBで取得したスクリーンショット |
| `recognition-*.json` | 取得結果、候補、OCRログ |
| 問題が見えるゲーム画面スクリーンショット | 目視確認用 |

通常ビルドや保存先を実行ファイル側にした場合は、次にも認識ログが残ることがあります。

```text
RHODES OBS COMMANDER3373 Data\state\recognition-logs
RHODES OBS COMMANDER3373 Data\state\adb-screenshots
```

## 6. よくあるエラー

### ADB executable was not found

`adb.exe` が見つかっていません。Android platform-toolsを入れるか、ADB設定で `adb.exe` のパスを選んでください。

### ADB device was not found

エミュレーターが起動していない、ADBデバッグが無効、またはserialが違います。プリセットとserialを確認してください。

### ADB device is offline

ADB接続が壊れています。ADB検出を再実行し、直らなければエミュレーターを再起動してください。

### Multiple ADB devices were found

複数端末が見えています。ADB設定のserialに対象を入力してください。

## 7. 送る前の短いメモ

報告本文には、分かる範囲で次を書いてください。

- 使用エミュレーター名
- 画面解像度またはウィンドウサイズ
- 選んだADBプリセット
- 問題が出たスキャン種別: 基本情報、オペレーター、秘宝、思案、時代など
- 期待した結果と実際に入った結果
