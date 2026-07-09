using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace RhodesSuki.Services;

public sealed class RhodesBitmapPathConverter : IValueConverter
{
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return _cache.GetOrAdd(path, LoadBitmap);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }

    // 一覧カードの表示サイズは42px程度。フル解像度でデコードすると
    // 数百枚で数百MB級のメモリと大きなデコード時間を食うため、サムネイル幅で読む。
    private const int ThumbnailDecodeWidth = 96;

    private static Bitmap? LoadBitmap(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, ThumbnailDecodeWidth);
        }
        catch
        {
            return null;
        }
    }
}
