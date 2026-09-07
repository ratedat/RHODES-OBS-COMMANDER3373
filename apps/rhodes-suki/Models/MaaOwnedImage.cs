using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace RhodesSuki.Models;

public sealed class MaaOwnedImage
{
    private readonly object _encodedGate = new();
    private readonly object _bgraGate = new();
    private readonly byte[] _rawPixels;
    private readonly Action<double>? _encodeObserver;
    private byte[]? _encodedImage;
    private byte[]? _bgraPixels;

    private MaaOwnedImage(
        byte[] rawPixels,
        byte[]? encodedImage,
        int width,
        int height,
        int channels,
        int openCvType,
        Action<double>? encodeObserver)
    {
        _rawPixels = rawPixels;
        _encodedImage = encodedImage;
        Width = width;
        Height = height;
        Channels = channels;
        OpenCvType = openCvType;
        _encodeObserver = encodeObserver;
    }

    public int Width { get; }

    public int Height { get; }

    public int Channels { get; }

    public int OpenCvType { get; }

    public bool HasRawPixels => _rawPixels.Length > 0;

    public bool HasEncodedImage => _encodedImage is { Length: > 0 };

    public ReadOnlyMemory<byte> RawPixels => _rawPixels;

    internal byte[] OwnedRawPixels => _rawPixels;

    public int Length => HasRawPixels ? _rawPixels.Length : _encodedImage?.Length ?? 0;

    public byte[] EncodedImage
    {
        get
        {
            if (_encodedImage is not null)
                return _encodedImage;

            lock (_encodedGate)
            {
                if (_encodedImage is not null)
                    return _encodedImage;
                if (!HasRawPixels)
                    return _encodedImage = [];

                var timer = Stopwatch.StartNew();
                using var bitmap = CreateBitmap();
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                _encodedImage = data.ToArray();
                timer.Stop();
                try
                {
                    _encodeObserver?.Invoke(timer.Elapsed.TotalMilliseconds);
                }
                catch
                {
                    // 計測先の失敗で画像生成を失敗扱いにしない。
                }
                return _encodedImage;
            }
        }
    }

    public static MaaOwnedImage FromOwnedRaw(
        byte[] pixels,
        int width,
        int height,
        int channels,
        int openCvType)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (channels is not (1 or 3 or 4))
            throw new ArgumentOutOfRangeException(nameof(channels));
        var expectedOpenCvType = (channels - 1) << 3;
        if (openCvType != expectedOpenCvType)
        {
            throw new ArgumentException(
                $"Only CV_8UC1/3/4 raw images are supported. channels={channels}, type={openCvType}, expected={expectedOpenCvType}.",
                nameof(openCvType));
        }

        var requiredLength = checked(width * height * channels);
        if (pixels.Length != requiredLength)
        {
            throw new ArgumentException(
                $"Raw image length must be {requiredLength:N0} bytes but was {pixels.Length:N0}.",
                nameof(pixels));
        }

        return new MaaOwnedImage(pixels, null, width, height, channels, openCvType, null);
    }

    internal static MaaOwnedImage FromOwnedRaw(
        byte[] pixels,
        int width,
        int height,
        int channels,
        int openCvType,
        Action<double> encodeObserver)
    {
        var image = FromOwnedRaw(pixels, width, height, channels, openCvType);
        return new MaaOwnedImage(
            image._rawPixels,
            null,
            width,
            height,
            channels,
            openCvType,
            encodeObserver);
    }

    public static MaaOwnedImage FromEncodedCopy(ReadOnlySpan<byte> encodedImage)
    {
        var owned = encodedImage.ToArray();
        if (owned.Length == 0)
            return new MaaOwnedImage([], owned, 0, 0, 0, 0, null);

        using var bitmap = SKBitmap.Decode(owned)
            ?? throw new InvalidOperationException("認識Frame画像をデコードできません。");
        return new MaaOwnedImage([], owned, bitmap.Width, bitmap.Height, 0, 0, null);
    }

    internal static MaaOwnedImage FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var pixels = new byte[checked(bitmap.Width * bitmap.Height * 3)];
        if (bitmap.ColorType == SKColorType.Bgra8888
            && bitmap.AlphaType == SKAlphaType.Opaque)
        {
            var sourceBytes = new byte[checked(bitmap.RowBytes * bitmap.Height)];
            Marshal.Copy(bitmap.GetPixels(), sourceBytes, 0, sourceBytes.Length);
            for (var y = 0; y < bitmap.Height; y++)
            {
                var sourceRow = y * bitmap.RowBytes;
                var targetRow = y * bitmap.Width * 3;
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var sourceOffset = sourceRow + x * 4;
                    var targetOffset = targetRow + x * 3;
                    pixels[targetOffset] = sourceBytes[sourceOffset];
                    pixels[targetOffset + 1] = sourceBytes[sourceOffset + 1];
                    pixels[targetOffset + 2] = sourceBytes[sourceOffset + 2];
                }
            }
            return FromOwnedRaw(pixels, bitmap.Width, bitmap.Height, 3, 16);
        }

        var offset = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                pixels[offset++] = color.Blue;
                pixels[offset++] = color.Green;
                pixels[offset++] = color.Red;
            }
        }

        return FromOwnedRaw(pixels, bitmap.Width, bitmap.Height, 3, 16);
    }

    internal SKBitmap CreateBitmap()
    {
        if (!HasRawPixels)
        {
            if (_encodedImage is not { Length: > 0 })
                throw new InvalidOperationException("認識Frame画像が空です。");
            return SKBitmap.Decode(_encodedImage)
                ?? throw new InvalidOperationException("認識Frame画像をデコードできません。");
        }

        var bgra = GetOrCreateBgraPixels();
        var alphaType = Channels == 4 ? SKAlphaType.Unpremul : SKAlphaType.Opaque;
        var bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, alphaType);
        Marshal.Copy(bgra, 0, bitmap.GetPixels(), bgra.Length);
        return bitmap;
    }

    private byte[] GetOrCreateBgraPixels()
    {
        if (Channels == 4)
            return _rawPixels;

        lock (_bgraGate)
        {
            if (_bgraPixels is not null)
                return _bgraPixels;

            var bgra = new byte[checked(Width * Height * 4)];
            var source = _rawPixels.AsSpan();
            for (var pixelIndex = 0; pixelIndex < Width * Height; pixelIndex++)
            {
                var sourceOffset = pixelIndex * Channels;
                var targetOffset = pixelIndex * 4;
                if (Channels == 1)
                {
                    var value = source[sourceOffset];
                    bgra[targetOffset] = value;
                    bgra[targetOffset + 1] = value;
                    bgra[targetOffset + 2] = value;
                }
                else
                {
                    bgra[targetOffset] = source[sourceOffset];
                    bgra[targetOffset + 1] = source[sourceOffset + 1];
                    bgra[targetOffset + 2] = source[sourceOffset + 2];
                }
                bgra[targetOffset + 3] = byte.MaxValue;
            }

            return _bgraPixels = bgra;
        }
    }
}
