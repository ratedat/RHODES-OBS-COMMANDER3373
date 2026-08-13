# Noto Sans JP

RHODES OBS COMMANDER3373の日本語・ラテン文字UIを同じ字形体系・同じウェイトで表示するため、Noto Sans JPの静的フォントを無改変で同梱します。

Avaloniaは可変フォントのウェイト軸に未対応のため、既定値がThinだった可変フォントは使用しません。本文・強調・太字に対応する静的なRegular / Medium / Boldを埋め込みます。

- Source directory: https://github.com/notofonts/noto-cjk/tree/523d033d6cb47f4a80c58a35753646f5c3608a78/Sans/SubsetOTF/JP
- Source commit: `523d033d6cb47f4a80c58a35753646f5c3608a78`
- `NotoSansJP-Regular.otf`: `DFF723BA59D57D136764A04B9B2D03205544F7CD785A711442D6D2D085AC5073`
- `NotoSansJP-Medium.otf`: `F396A3B57256E4515BE9CB41F7AAC54766D654890082A9F1B5C2451B5C093D8A`
- `NotoSansJP-Bold.otf`: `1B0EDFB500B73A4FA8A4FCAAE1BBBD403994E08E73E3E0DA37E70D3853F42C5F`
- License: SIL Open Font License 1.1 (`OFL-NotoSansJP.txt`)

Webfontの実行時取得は行いません。Avaloniaリソースとしてアプリへ埋め込み、オフライン環境とportable配布でも同じフォントを使用します。韓国語・簡体字・繁体字の地域固有字形は、将来のUI言語追加時に対応するNoto Sans CJK地域版を同じフォントトークンへ追加します。
