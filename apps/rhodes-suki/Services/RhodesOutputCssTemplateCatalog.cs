namespace RhodesSuki.Services;

public sealed record SukiOutputCssTemplate(
    string Id,
    string DisplayName,
    string Description,
    string Css)
{
    public override string ToString() => DisplayName;
}

public static class RhodesOutputCssTemplateCatalog
{
    public static IReadOnlyList<SukiOutputCssTemplate> DefaultTemplates { get; } =
    [
        new(
            "transparent-canvas-frame",
            "透明キャンバス＋半透明枠",
            "OBS Browser Source全体を透明にし、情報カードと部品枠だけを半透明で残します。",
            """
            /* ゲーム画面へ情報カードだけを重ねる */
            :root {
              --overlay-canvas-background-rgb: 0 0 0;
              --overlay-canvas-background-alpha: 0;
              --overlay-background-rgb: 8 11 12;
              --overlay-background-alpha: 0.86;
            }
            """),
        new(
            "title-icon-line",
            "タイトルに放送アイコン",
            "統合Overlayと個別ウィンドウの見出し左へ、単色のラインアイコンを追加します。",
            """
            /* タイトル左に放送アイコン（統合Overlay / 個別ウィンドウ共通） */
            .overlay-card-header,
            .overlay-part-head {
              display: flex;
              align-items: center;
              gap: 0.55em;
            }

            .overlay-card-header::before,
            .overlay-part-head::before {
              content: "";
              flex: 0 0 1em;
              width: 1em;
              height: 1em;
              background-color: currentColor;
              -webkit-mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='black' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Crect x='3' y='5' width='18' height='14' rx='2'/%3E%3Cpath d='m10 9 5 3-5 3z'/%3E%3C/svg%3E");
              mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='black' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Crect x='3' y='5' width='18' height='14' rx='2'/%3E%3Cpath d='m10 9 5 3-5 3z'/%3E%3C/svg%3E");
              -webkit-mask-position: center;
              mask-position: center;
              -webkit-mask-repeat: no-repeat;
              mask-repeat: no-repeat;
              -webkit-mask-size: contain;
              mask-size: contain;
            }
            """),
        new(
            "title-accent-rule",
            "タイトルにアクセントライン",
            "見出しの左端へ短い強調線を付け、アイコンを使わず区切りを明確にします。",
            """
            /* タイトル左のアクセントライン */
            .overlay-card-header,
            .overlay-part-head {
              padding-left: 0.7em;
              border-left: 0.24em solid var(--overlay-accent-color);
            }
            """),
        new(
            "part-title-badge",
            "個別タイトルをラベル化",
            "個別ウィンドウのタイトルを、小さな角丸ラベルとして表示します。",
            """
            /* 個別ウィンドウのタイトルラベル */
            .overlay-part-head {
              width: fit-content;
              padding: 0.3em 0.65em;
              border: 1px solid var(--overlay-border-color);
              border-radius: 0.35em;
              background: rgb(var(--overlay-background-rgb) / 92%);
              color: var(--overlay-accent-color);
              letter-spacing: 0.02em;
            }
            """),
        new(
            "high-contrast-edges",
            "輪郭を明確にする",
            "カードと個別ウィンドウの枠・区切りを強め、明るいゲーム画面上でも読みやすくします。",
            """
            /* 明るいゲーム画面向けの明確な輪郭 */
            .overlay-card,
            .overlay-part-shell {
              border: 2px solid var(--overlay-border-color);
              box-shadow: 0 2px 8px rgb(0 0 0 / 48%);
            }

            .overlay-card-header,
            .overlay-part-head {
              border-bottom-color: var(--overlay-border-color);
            }
            """),
    ];
}
