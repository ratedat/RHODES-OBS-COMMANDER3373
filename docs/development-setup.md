# 開発環境の準備

対象はWindows上のSuki/AvaloniaアプリとNode APIです。初回準備と、以後の検証を分けます。

## 必要な環境

- Git。
- Node.js `.node-version` の版（24.19.0）、npm `package.json` の `packageManager` の版（11.17.0）。
- .NET SDK `global.json` の版（9.0.202）。自動で別のSDKへ切り替えません。
- .NET 8ランタイム。アプリとC#テストの対象は `net8.0` です。
- PowerShell。ソース起動スクリプトを実行する場合に使います。

これらは検証環境の固定値です。更新時は固定ファイルと依存ロックを一緒に見直し、全検証を実行してください。Maa.Frameworkのpreview版とRuntimesのbeta版も、現在のプロジェクト指定とロックに従います。PythonとGLM-OCRはオプションの作業用で、通常ビルドの前提ではありません。

## 初回の準備

作業フォルダーで次を順番に実行します。

```powershell
npm ci --ignore-scripts
npm run hooks:install
npm run assets:fetch
npm run deps:restore
npm run doctor
npm run verify:all -- --no-restore
```

`assets:fetch` はビルドに必要なOCRモデル2件の不足分だけを取得します。`data/recognition/maa-build-assets.lock.json` の固定Git blob URL、サイズ、SHA-256を照合し、合致したファイルだけを保存します。既存ファイルが不一致なら上書きせず失敗するため、内容を退避・確認してから置き換えてください。資材の出典・ライセンスは `THIRD_PARTY_NOTICES.md` を参照してください。

`deps:restore` はアプリとC#テストの `packages.lock.json` を固定モードで復元します。依存更新時だけロックの更新を明示的に行い、差分を確認してください。既に必要なパッケージがローカルにある場合は、そのパッケージソースを指定して復元できます。

`suki:publish:portable` は、Windows x64の自己完結型・単一ファイル配布に必要な依存関係を `apps/rhodes-suki/packages.portable.lock.json` から固定モードで復元します。通常ビルド用のロックは書き換えません。配布の依存関係を更新する場合は、この配布用ロックも明示的に更新して差分を確認してください。

## 読み取りだけの診断

```powershell
npm run doctor
npm run doctor -- --json
npm run assets:check
npm run hooks:check
```

診断はSDKとランタイム、依存ロック、固定資材、初期状態テンプレート、Gitフックを確認します。不足は `MISSING` と補修手順で表示し、インストール、モデル取得、アプリ起動は行いません。NuGetの復元済み状態やアプリ画面の動作は、診断の成功だけでは保証しません。

## Gitフック

`hooks:install` は現在のNode実行ファイルとリポジトリ位置を使い、pre-commitとpre-pushへ公開前の検査を設定します。検査スクリプトのハッシュとフックの内容をGit共通ディレクトリの `info/publication-hooks.json` に記録します。スクリプト更新後は再度インストールしてください。

既存のローカル方針ファイルはそのまま保持します。必須方針が欠けている場合や、未管理のフック・`core.hooksPath` がある場合は停止します。新規リポジトリには一般的な初期方針を作ります。個別の除外情報をソース管理へ移さず、必要な方針を別途管理してください。フック、パス・本文・秘密情報の検査は、公開内容の人による確認を置き換えません。

## 状態と検証結果

ビルドは追跡対象の `data/overlay-state.example.json` を使います。ローカルの `data/current-state.json` をビルド入力にしません。配布物を作る入口ではテンプレートから初期状態を生成し、開発中の状態を変更しません。

通常の全検証は `npm run verify:all -- --no-restore` です。ビルドと自動テストの詳細、起動と画面確認の範囲は [開発検証手順](development-verification.md) を参照してください。
