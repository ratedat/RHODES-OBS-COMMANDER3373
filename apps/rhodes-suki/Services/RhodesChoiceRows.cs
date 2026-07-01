using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesChoiceRows
{
    public static IReadOnlyList<SukiChoiceRow> Build(IEnumerable<SukiChoiceItem> items, int columns)
    {
        var columnCount = Math.Clamp(columns, 1, 4);
        var rows = new List<SukiChoiceRow>();
        var buffer = new List<SukiChoiceItem>(columnCount);

        foreach (var item in items)
        {
            buffer.Add(item);
            if (buffer.Count < columnCount)
                continue;

            rows.Add(new SukiChoiceRow(columnCount, buffer.ToArray()));
            buffer.Clear();
        }

        if (buffer.Count > 0)
            rows.Add(new SukiChoiceRow(columnCount, buffer.ToArray()));

        return rows;
    }
}
