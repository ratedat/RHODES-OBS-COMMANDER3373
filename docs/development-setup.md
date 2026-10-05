# 開発環境の準備

対象はWindows上のSuki/AvaloniaアプリとNode APIです。初回準備と、以後の検証を分けます。

## 必要な環境

- Git。
- Node.js `.node-version` の版（24.21.0）、npm `package.json` の `packageManager` の版（11.19.0）。
- .NET SDK `global.json` の版（9.0.318）。自動で別のSDKへ切り替えません。
- .NET 8ランタイム。アプリとC#テストの対象は `net8.0` です。
- PowerShell。ソース起動スクリプトを実行する場合に使います。

これらは検証環境の固定値です。更新時は固定ファイルと依存ロックを一緒に見直し、全検証を実行してください。MAAはC#連携の `Maa.Framework 5.13.0-preview.1` と実行エンジンの `Maa.Framework.Runtimes 5.14.2` を使用します。両者は別々に版管理されるため、プロジェクト指定と依存ロックを正本にします。通常の起動・C#ビルドにはPythonは不要です。Node全テストに含まれるOCR画像生成テストにはPython 3とPillowが必要です。PATH上の `python` を使うか、`PYTHON` 環境変数に実行ファイルを指定してください。GLM-OCRはオプションです。

2026-10-05に[Avalonia 12.1.3](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.3)、[MAAFramework 5.14.2](https://github.com/MaaXYZ/MaaFramework/releases/tag/v5.14.2)、[Node.js 24.21.0 LTS](https://nodejs.org/en/blog/release/v24.21.0)、[cloudflared 2026.9.3](https://github.com/cloudflare/cloudflared/releases/tag/2026.9.3)へ更新しました。SukiUI 7.0.1とMAAのC#連携は公開版を照合して維持しています。Avaloniaのコード生成に合わせてSDKも9.0.318へ更新しています。通常ビルド、C#テスト、Windows x64配布用の各ロックを合わせて更新します。

.NETの対象は引き続き `net8.0` です。今回のローカル検証では.NET 8.0.31を使用します。.NET 10への移行は含みません。

## プロジェクト専用の開発環境

システム全体へインストールせず、公式配布物を `outputs/development-tools/dotnet-9.0.318/` と `outputs/development-tools/node-v24.21.0-win-x64/` に展開した場合は、PowerShellで次を実行します。.NET 8ランタイムも同じdotnetフォルダーに配置してください。

```powershell
. .\tools\windows\use-development-tools.ps1
```

固定版のフォルダーが存在する場合だけ、現在のPowerShellと子プロセスのPATH・DOTNET_ROOTを切り替えます。永続設定は変更しません。`start-app.ps1` はこの選択を自動で行います。フォルダーがない環境では、通常どおりインストール済みの開発環境を使います。

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
