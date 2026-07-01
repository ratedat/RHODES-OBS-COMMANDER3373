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

    private static Bitmap? LoadBitmap(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }
}
