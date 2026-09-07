# 開発時の検証手順

この文書を、RHODES OBS COMMANDER3373の変更を完了と判断するための正本とします。変更したファイルと挙動に応じて必要な検証を選び、実行したコマンド、結果、未確認事項を記録してください。資材のダウンロード、公開、配布物作成は検証に混ぜません。

## コマンドの範囲

`package.json` が定義する主な検証コマンドは次のとおりです。

| コマンド | 確認する範囲 | 含まれないもの |
| --- | --- | --- |
| `npm test` | `tests/*.test.mjs` のNode.jsテスト全体 | C#テスト、C#ビルド、実画面確認 |
| `npm run maa:check` | MAA resource、hash、interfaceを `--check` で照合し、contract検査を実行 | C#ビルド、Node全テスト、実画面確認 |
| `npm run suki:test` | `dotnet run` で実行する `tests/rhodes-suki/` のカスタムconsoleテスト | `dotnet test` のテストプロジェクトではない。実画面確認も行わない |
| `npm run suki:build` | `apps/rhodes-suki/RhodesSuki.csproj` のビルド | Node全テスト、実画面確認 |
| `npm run verify:desktop` | `maa:check`、`suki:test`、`suki:build` | `npm test`、実画面確認 |
| `npm run verify:all` | Node全テスト、MAA検査、C#テスト、デスクトップビルド | 実画面確認、資材取得、公開 |

全体を確認するときは次を実行します。初回の準備は [開発環境の準備](development-setup.md) を参照してください。

```powershell
npm run verify:all -- --no-restore
```

各段階の成否と所要時間、成功したビルドのEXEとアプリ本体DLLのハッシュは `outputs/verification/latest.json` に保存します。途中で失敗した場合、後続段階は実行せず、過去のビルドを今回の成功として記録しません。`verify:desktop` はNode全テストを含みません。

## 変更種別ごとの選択

### 文書と検証ハーネス

文書だけの変更では、リンク先の存在、Markdownの構造、文字コード、差分を確認します。検証ハーネスだけの変更では、対象ファイルの構文とコマンド定義を照合し、必要なら変更したハーネス自身だけを実行します。実行コードに影響しない場合、全ビルドは必須ではありません。

### Node API、OBS画面、データ

狭い変更では該当するテストを直接実行します。

```powershell
node --test tests/<関連するテスト>.test.mjs
```

API契約、複数のdomain、サーバーと画面の境界、共通データなどへ影響が広がる場合は `npm test` を実行します。データ変更では、利用箇所に対応するNodeテストを選び、生成物やC#へ波及する場合だけMAA/C#の確認を追加します。

### MAA resourceとSuki/Avalonia

MAAの定義、生成物、hash、interface、contractを変更した場合は、最初に次を実行します。

```powershell
npm run maa:check
```

このコマンドは生成結果を照合するためのもので、生成ファイルを書き換えるコマンドではありません。Suki側がその変更を読み込む場合は、影響に応じて次も選びます。

```powershell
npm run suki:test
npm run suki:build
```

C#ロジックだけの変更では通常 `npm run suki:test` を使い、XAML、プロジェクト設定、埋め込み資材、起動経路へ触れた場合は `npm run suki:build` も使います。MAA契約へ触れていない純粋なC#変更に、理由なく `maa:check` を強制しません。

## 外部接続を使わない検証

既に依存関係が復元されており、外部接続を発生させない必要がある回では、次のように `--no-restore` を指定します。

```powershell
dotnet build apps/rhodes-suki/RhodesSuki.csproj --no-restore
dotnet run --project tests/rhodes-suki/RhodesSuki.ServiceTests.csproj --no-restore
```

初回の復元が済んでおらず `--no-restore` で実行できない場合は、復元不足のため未確認と記録します。その場で資材取得やrestoreを検証へ追加しません。必要なSDK、実行環境、対象ツールが使えない場合も、別の成功した確認で置き換えず未確認と明記します。

## 実画面の確認

Suki側の画面を確認する場合は、今回の変更後に `npm run suki:build` が成功し、起動対象が現在のcheckoutから生成された `apps/rhodes-suki/bin/Debug/net8.0/RhodesSuki.exe` であることを確認します。外部接続を使わない回では、前節の `--no-restore` 付きビルドを使います。既に同じ変更に対して成功したビルドは繰り返しません。OBS側だけの変更では、対象のNodeサーバーが今回の変更を読み込んでいることを確認します。

`tools/windows/start-app.ps1` は、通常起動時に現在のソースを必ずビルドし、成功した場合だけ同じ作業フォルダーのEXEを起動します。ビルド失敗時は既存EXEを起動しません。`-SmokeTest` はビルドのみを行い、アプリの起動や既存プロセスの停止は行いません。`-NoRestore` も指定できます。SmokeTestはビルド出力を書き込むため、読み取り専用の診断には `npm run doctor` を使います。

変更に関係する項目だけを実画面で確認します。

- 日本語テキストが欠けず、意図した位置に表示される。
- OCR/ADB由来の提案を手動入力で訂正でき、確定状態を黙って上書きしない。
- OBS向け画面を1920x1080で確認する。
- 背景透過に関係する変更では、透過モードを確認する。

実画面を開けない場合は、その項目を未確認として残します。自動テストやビルドの成功だけで実画面確認済みとは記録しません。

## 完了時の記録

最後に、次を簡潔に残します。

1. 実行したコマンドと成否。
2. 実画面で確認した項目。
3. ツールや環境の不足で未確認となった項目。
4. 依頼範囲外の差分が混入していないこと。

失敗が変更に起因する場合は修正して同じ確認を再実行します。変更と無関係な既存失敗は、根拠とともに分けて報告します。
