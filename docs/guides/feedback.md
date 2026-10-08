# 不具合報告と機能要望

RHODESの不具合・使い方の質問・改善の提案は、[GitHub Issues](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/issues)で受け付けています。同じ内容の報告がある場合は、そのIssueへ情報を追加してください。

## 不具合を報告する

[不具合報告フォーム](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/issues/new?template=bug_report.yml)で、分かる範囲の情報を記入してください。

- 使用した配布ZIPの名前、またはソースのコミット番号。
- Windowsのバージョンと、関係する場合はOBS・エミュレーターの名前とバージョン。
- 選択した統合戦略テーマと、問題が起きた画面。
- 再現する操作、期待した結果、実際に起きた結果。
- エラー文や、問題の箇所が分かる画像。

アプリを起動できない場合も、ZIP名と表示されたエラーがあれば報告できます。追加資料を用意できるまで待つ必要はありません。

### バグ報告ZIPを作る

1. アプリの「デバッグ・報告」を開きます。
2. 「バグ報告ZIP」の「作成」を押します。上部の「その他の操作」→「バグ報告ZIPを作成」からも実行できます。
3. 表示された保存先のZIPを確認します。

報告ZIPには、ログ、認識画像、現在のラン状態、アプリ設定が含まれます。保存先は通常 `RHODES OBS COMMANDER3373 Debug Logs/Bug Reports/` です。

**GitHubのIssueと添付ファイルは公開されます。** 添付前に、ゲーム画面の表示名・UID、ローカルのファイルパス、接続先、入力URLやコードなど、公開したくない情報が入っていないか確認してください。必要な部分だけを抜粋したログや画像でも構いません。報告ZIPは必須ではありません。

ADB/OCRの詳しい調査情報は[ADB/OCR報告ガイド](debugger-adb-report-guide.md)を参照してください。

## 機能を提案する

[機能要望フォーム](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/issues/new?template=feature_request.yml)に、次の3点を記入してください。

1. どのような配信・大会・入力作業で困っているか。
2. どのように操作できると助かるか。
3. 今はどのように対応しているか。

具体的な場面や表示例があると、必要な変更を検討しやすくなります。実装方法が決まっていなくても提案できます。

## 先に確認できる資料

- [はじめ方とOBS設定](startup-guide.md)
- [ADB設定とトラブルシューティング](adb-setup.md)
- [大会遠隔入力](tournament-remote-input.md)
- [現在の制限](../../README.md#現在の制限)
