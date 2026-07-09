namespace RhodesSuki.Services;

/// <summary>
/// OBSへ部品単位で追加するためのオーバーレイURL一覧。
/// 配信サーバー側の /overlay/part/&lt;id&gt; (app/lib/overlay-config.js の overlayParts) と対応する。
/// 各部品を個別のブラウザソースとして追加すれば、位置・サイズ・重なりはOBS側で自由に調整できる。
/// </summary>
public static class RhodesOverlayPartLinkCatalog
{
    private static readonly (string Id, string Label, string Hint)[] Parts =
    [
        ("status", "ラン状態", "上部バー向け / 目安 1200x120"),
        ("relics", "秘宝", "横帯・下帯向け / 目安 1200x170"),
        ("operators", "招集オペレーター", "右サイド向け / 目安 420x620"),
        ("effects", "発動効果", "左サイド向け / 目安 520x360"),
        ("bosses", "ボスフラグ", "フラグ枠向け / 目安 520x220"),
        ("special", "特殊値 (思案・啓示など)", "目安 520x180"),
    ];

    public static IReadOnlyList<SukiOverlayPartLink> Build(string apiUrl)
    {
        var baseUrl = string.IsNullOrWhiteSpace(apiUrl) ? "http://127.0.0.1:5173" : apiUrl.TrimEnd('/');
        return Parts
            .Select(part => new SukiOverlayPartLink(
                part.Label,
                $"/overlay/part/{part.Id}",
                part.Hint,
                $"{baseUrl}/overlay/part/{part.Id}"))
            .ToArray();
    }
}

public sealed record SukiOverlayPartLink(string Label, string Path, string Hint, string Url);
