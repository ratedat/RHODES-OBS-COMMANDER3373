# RHODES ドキュメント

[プロジェクトの紹介](../README.md)へ戻る。

## 初めて使う方へ

- [はじめ方とOBS設定](guides/startup-guide.md) — 起動、手動入力、配信画面への追加、更新方法。
- [ADB設定とトラブルシューティング](guides/adb-setup.md) — エミュレーターの接続と画面取得。
- [出力のカスタマイズ](guides/output-css-customization.md) — 色・文字・背景とユーザーCSS。
- [大会遠隔入力](guides/tournament-remote-input.md) — 入力担当者への共有、再接続、担当交代。
- [不具合報告と機能要望](guides/feedback.md) — 報告に必要な情報と添付ファイルの扱い。

## 大会運用・詳しい設定

- [外部中継サーバーの導入](guides/external-relay-server-setup.md)
- [公開デバッグ版の短い操作ガイド](guides/discord-public-debug-guide.md)
- [ADB/OCRの詳しい報告ガイド](guides/debugger-adb-report-guide.md)
- [サルカズの認識確認項目](guides/sarkaz-test-guide.md)
- [GLM-OCRの任意導入](guides/glm-ocr-setup.md)
- [PaddleOCRの旧構成について](guides/paddle-ocr-setup.md)

## 開発に参加する方へ

- [貢献ガイド](../CONTRIBUTING.md)
- [開発環境の準備](development-setup.md)
- [開発検証手順](development-verification.md)
- [システム構成](reference/architecture.md)
- [データの更新手順](guides/data-update.md)
- [データの出典](reference/data-sources.md)
- [統合戦略データの収録範囲](reference/data-summary.md)
- [秘宝・分隊の効果計算](reference/effect-calculation.md)
- [認識処理の設計](reference/recognition-notes.md)
- [MAA-OCRの採用方針](reference/maa-ocr-adoption.md)
- [MAA-OCRの調査資料](reference/maa-ocr-research.md)
- [MAAFramework関連の開発方針](reference/maaframework-family-roadmap.md)
- [認識性能の確認手順](recognition-performance.md)
- [IS#6の銭OCR評価用データ](reference/sui-coin-ocr-corpus.md)

## UI設計資料

- [Suki版のデザイン方針](design/suki-design-philosophy-ja.md)
- [画面と情報の構成](design/suki-product-ui-information-architecture.md)
- [操作画面の設計原則](design/suki-workbench-design-principles.md)
- [旧Web操作画面の設計](design/control-v2-screen-design.md)
- [過去の表示サンプル](previews/)

## ライセンス・設計判断

- [ライセンスとソースコード](legal/licenses.md)
- [第三者ライブラリ・素材の表記](../THIRD_PARTY_NOTICES.md)
- [MAAFrameworkとSukiUIの採用](decisions/0001-adopt-maaframework-and-sukiui.md)
