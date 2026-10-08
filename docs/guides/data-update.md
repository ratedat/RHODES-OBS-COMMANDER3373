# データの更新手順

Wiki等に由来するデータの更新は、差分を記録する更新ツールを使います。利用環境の準備は[開発環境の準備](../development-setup.md)、出典は[データの出典](../reference/data-sources.md)を参照してください。

## 更新内容を確認する

リポジトリのフォルダーで実行します。

```powershell
npm run data:update:plan
```

計画を確認した後、標準の更新を実行します。

```powershell
npm run data:update
```

一部だけ更新する場合は対象を指定します。

```powershell
npm run data:update -- -Scope Operators
npm run data:update -- -Scope Performances
```

## 差分をレビューする

更新結果は `review/update-runs/<run-id>/` に保存されます。

| ファイル | 内容 |
| --- | --- |
| `summary.md` | 件数と主な変更 |
| `changes.csv` / `changes.json` | 項目ごとの差分 |
| `before/data` / `after/data` | 更新前後のデータ |
| `run.log` | 実行した処理と結果 |

名称・件数・画像の対応と、意図しない削除がないか確認してください。生成された更新レポートはGit管理対象外です。確認後に、必要なデータ・画像の変更だけをコミットします。

## 主な定義ファイル

| 内容 | ファイル |
| --- | --- |
| テーマの取得元 | [`wikiru-campaign-sources.json`](../../data/wikiru-campaign-sources.json) |
| オペレーターの取得元 | [`wikiru-operator-sources.json`](../../data/wikiru-operator-sources.json) |
| オペレーター情報と日本版実装順 | [`operators.json`](../../data/operators.json) / [`operator-implementation-history.json`](../../data/operator-implementation-history.json) |
| 秘宝・画像の同期結果 | [`relics.json`](../../data/relics.json) / [`relic-images.json`](../../data/relic-images.json) |
| オペレーター画像の同期結果 | [`operator-images.json`](../../data/operator-images.json) |
| 演目・イベント効果 | [`performance-sources.json`](../../data/performance-sources.json) / [`performances.json`](../../data/performances.json) |
| 難度ごとの秘宝差分 | [`difficulty-variant-sources.json`](../../data/difficulty-variant-sources.json) / [`relic-effect-variants.json`](../../data/relic-effect-variants.json) |
| 難度段階・等級 | [`difficulty-tiers.json`](../../data/difficulty-tiers.json) / [`difficulty-grades.json`](../../data/difficulty-grades.json) |
| 等級の取得元 | [`difficulty-grade-sources.json`](../../data/difficulty-grade-sources.json) |

画像の出典とローカルパスは各データのメタ情報を参照してください。収録範囲は[統合戦略データの収録範囲](../reference/data-summary.md)にまとめています。
