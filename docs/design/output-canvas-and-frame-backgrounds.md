# OBS出力の全体背景・枠背景分離

## 目的

統合Overlayと個別ウィンドウの双方で、OBS Browser Source全体を塗る背景と、カード・部品枠を塗る背景を独立して設定できるようにする。

代表的な利用例は「OBSキャンバスは透明、情報カードだけ半透明」である。

## 用語と互換性

- **全体背景（canvas background）**: Browser Source全体の背景。
- **枠背景（frame background）**: カード、部品枠、見出し、値セルなどの中立面。
- schemaVersion 2までの `background*` と `appearance.backgroundColor` は、schemaVersion 3以降では枠背景として維持する。
- schemaVersion 2以前を読み込むときは、旧背景の表示、濃度、色を新しい全体背景へも複製し、移行直後の外観を維持する。
- 統合Overlayと個別ウィンドウは、これまでどおり別々の設定を持つ。
- 個別部品の背景設定は枠背景を上書きする。全体背景は個別ウィンドウ設定を使う。

## 設定契約

既存の枠背景設定に加えて、次を追加する。

- `canvasBackgroundEnabled`
- `canvasBackgroundOpacity`
- `individualCanvasBackgroundEnabled`
- `individualCanvasBackgroundOpacity`
- `appearance.canvasBackgroundColor`

出力状態JSONでは既存の `sukiOutputBackground*` を枠背景として維持し、次を追加する。

- `sukiOutputCanvasBackgroundEnabled`
- `sukiOutputCanvasBackgroundOpacity`
- `sukiOutputIndividualCanvasBackgroundEnabled`
- `sukiOutputIndividualCanvasBackgroundOpacity`

## CSS契約

既存の `--overlay-background-rgb` と `--overlay-background-alpha` は枠背景用として維持する。

全体背景には次を追加する。

- `--overlay-canvas-background-rgb`
- `--overlay-canvas-background-alpha`

状態クラスは次のとおりとする。

- `html.overlay-canvas-background-disabled`: 全体背景がOFF。
- `html.overlay-frame-background-disabled`: 枠背景がOFF。
- `html.overlay-background-disabled`: 両方がOFFのときだけ付く互換クラス。

## 受け入れ条件

1. 統合Overlayと個別ウィンドウの各設定欄に、全体背景と枠背景の表示、濃度、色がある。
2. 全体背景をOFF、枠背景をONにすると、Browser Source全体は透明のままカードが表示される。
3. 枠背景をOFF、全体背景をONにすると、キャンバス色は残りカードの中立面と枠線が消える。
4. schemaVersion 2以前のプロファイルはschemaVersion 3へ移行され、旧背景値が両背景へ引き継がれる。
5. JSON export/importと状態API同期で、統合・個別の全設定が欠落なく往復する。
6. Markdown版と同梱HTML版のCSS説明書に、変数表と「透明キャンバス＋半透明枠」の見本がある。
