using System.Diagnostics;
using System.Text.Json.Nodes;
using RhodesSuki.Models;
using RhodesSuki.Services;
using SkiaSharp;

namespace RhodesSuki.Tests;

public static class MaaOwnedImageTests
{
    public static void Run()
    {
        RawImageOwnsPixelsAndDefersPngEncoding();
        RawFingerprintMatchesEncodedCompatibilityInput();
        RawOcrPreprocessingPreservesRoiAndScale();
        EncodedCompatibilityInputIsCopied();
        RejectsMismatchedOpenCvType();
        FourChannelAlphaMatchesEncodedRoiProcessing();
        ReplacedFramesReleaseTheirCaches();
        WriteOptInRawEncodedMicrobenchmark();
    }

    private static void ReplacedFramesReleaseTheirCaches()
    {
        var frames = Enumerable.Range(0, 24).Select(_ => CreateRetiredFrame()).ToArray();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Equal(0, frames.Count(frame => frame.IsAlive), "retired raw frames and their PNG/BGRA caches are collectible");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateRetiredFrame()
    {
        var frame = MaaOwnedImage.FromOwnedRaw(new byte[1280 * 720 * 3], 1280, 720, 3, 16);
        _ = RhodesRecognitionFrameFingerprint.Compute(frame, new RhodesRecognitionSwipeArea(0, 0, 1280, 720));
        _ = frame.EncodedImage;
        return new WeakReference(frame);
    }

    private static void RawImageOwnsPixelsAndDefersPngEncoding()
    {
        var image = MaaOwnedImage.FromOwnedRaw(
            [0, 0, 255, 0, 255, 0],
            width: 2,
            height: 1,
            channels: 3,
            openCvType: 16);

        Equal(true, image.HasRawPixels, "raw image has pixels");
        Equal(false, image.HasEncodedImage, "PNG is not produced during raw capture");
        Equal(6, image.Length, "raw payload length");

        using var decoded = SKBitmap.Decode(image.EncodedImage);
        Equal(true, image.HasEncodedImage, "PNG is cached after first request");
        Equal(SKColors.Red, decoded.GetPixel(0, 0), "OpenCV BGR red pixel is preserved");
        Equal(SKColors.Lime, decoded.GetPixel(1, 0), "OpenCV BGR green pixel is preserved");
    }

    private static void RawFingerprintMatchesEncodedCompatibilityInput()
    {
        var pixels = new byte[8 * 8 * 3];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var offset = (y * 8 + x) * 3;
                pixels[offset] = 255;
                pixels[offset + 1] = 255;
                pixels[offset + 2] = 255;
            }
        }
        var raw = MaaOwnedImage.FromOwnedRaw(pixels, 8, 8, 3, 16);
        var encoded = MaaOwnedImage.FromEncodedCopy(raw.EncodedImage);
        var area = new RhodesRecognitionSwipeArea(0, 0, 1280, 720);

        Equal(
            RhodesRecognitionFrameFingerprint.Compute(encoded, area),
            RhodesRecognitionFrameFingerprint.Compute(raw, area),
            "raw and encoded fingerprints match");
    }

    private static void RawOcrPreprocessingPreservesRoiAndScale()
    {
        var pixels = new byte[4 * 4 * 3];
        var whiteOffset = (1 * 4 + 1) * 3;
        pixels[whiteOffset] = 255;
        pixels[whiteOffset + 1] = 255;
        pixels[whiteOffset + 2] = 255;
        var raw = MaaOwnedImage.FromOwnedRaw(pixels, 4, 4, 3, 16);

        var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
            raw,
            "OCR",
            """{"roi":[1,1,2,2],"only_rec":true,"threshold":0.3}""",
            3);

        Equal(true, prepared.Image.HasRawPixels, "prepared OCR ROI remains raw");
        Equal(false, prepared.Image.HasEncodedImage, "prepared OCR ROI does not encode eagerly");
        Equal(6, prepared.Image.Width, "scaled crop width");
        Equal(6, prepared.Image.Height, "scaled crop height");
        var parameters = JsonNode.Parse(prepared.ParametersJson)!.AsObject();
        Equal(0, parameters["roi"]![0]!.GetValue<int>(), "prepared roi x");
        Equal(0, parameters["roi"]![1]!.GetValue<int>(), "prepared roi y");
        Equal(6, parameters["roi"]![2]!.GetValue<int>(), "prepared roi width");
        Equal(6, parameters["roi"]![3]!.GetValue<int>(), "prepared roi height");
        Equal(0.3, parameters["threshold"]!.GetValue<double>(), "OCR threshold is preserved");
    }

    private static void EncodedCompatibilityInputIsCopied()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Blue);
        using var skImage = SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        var source = data.ToArray();
        var image = MaaOwnedImage.FromEncodedCopy(source);

        source[0] = 0;

        Equal(2, image.Width, "encoded compatibility width");
        Equal(2, image.Height, "encoded compatibility height");
        using var decoded = SKBitmap.Decode(image.EncodedImage);
        Equal(SKColors.Blue, decoded.GetPixel(0, 0), "encoded compatibility input is owned independently");
    }

    private static void RejectsMismatchedOpenCvType()
    {
        try
        {
            MaaOwnedImage.FromOwnedRaw(new byte[3], 1, 1, 3, openCvType: 24);
            throw new InvalidOperationException("mismatched OpenCV type should throw");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void FourChannelAlphaMatchesEncodedRoiProcessing()
    {
        var raw = MaaOwnedImage.FromOwnedRaw(
            [
                0, 0, 255, 128,
                255, 0, 0, 64,
                0, 255, 0, 255,
                255, 255, 255, 0,
            ],
            width: 2,
            height: 2,
            channels: 4,
            openCvType: 24);

        using (var decoded = SKBitmap.Decode(raw.EncodedImage))
        {
            Equal(new SKColor(255, 0, 0, 128), decoded.GetPixel(0, 0), "BGRA semi-transparent red survives PNG");
            Equal(new SKColor(0, 0, 255, 64), decoded.GetPixel(1, 0), "BGRA translucent blue survives PNG");
            Equal((byte)0, decoded.GetPixel(1, 1).Alpha, "transparent BGRA pixel remains transparent");
        }

        var encoded = MaaOwnedImage.FromEncodedCopy(raw.EncodedImage);
        var rawPrepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
            raw,
            "OCR",
            """{"roi":[0,0,2,2],"only_rec":true}""",
            2);
        var encodedPrepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
            encoded,
            "OCR",
            """{"roi":[0,0,2,2],"only_rec":true}""",
            2);
        using var rawBitmap = SKBitmap.Decode(rawPrepared.EncodedImage);
        using var encodedBitmap = SKBitmap.Decode(encodedPrepared.EncodedImage);
        Equal(rawBitmap.Width, encodedBitmap.Width, "raw/encoded ROI width");
        Equal(rawBitmap.Height, encodedBitmap.Height, "raw/encoded ROI height");
        Equal(true, PixelsEquivalent(rawBitmap.Pixels, encodedBitmap.Pixels), "raw/encoded ROI pixels remain equivalent");
    }

    private static void WriteOptInRawEncodedMicrobenchmark()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RHODES_SUKI_IMAGE_BENCHMARK"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        const int width = 1280;
        const int height = 720;
        const int iterations = 10;
        var pixels = new byte[width * height * 3];
        for (var index = 0; index < pixels.Length; index++)
            pixels[index] = (byte)(index % 251);
        var raw = MaaOwnedImage.FromOwnedRaw(pixels, width, height, 3, 16);
        var encoded = MaaOwnedImage.FromEncodedCopy(raw.EncodedImage);
        var area = new RhodesRecognitionSwipeArea(100, 80, 900, 520);
        var areas = new[]
        {
            new RhodesRecognitionSwipeArea(100, 80, 400, 260),
            new RhodesRecognitionSwipeArea(500, 80, 400, 260),
            new RhodesRecognitionSwipeArea(100, 340, 400, 260),
            new RhodesRecognitionSwipeArea(500, 340, 400, 260),
        };
        const string parameters = """{"roi":[100,80,900,520],"only_rec":true,"threshold":0.3}""";

        var rawFingerprint = Measure(iterations, () => RhodesRecognitionFrameFingerprint.Compute(raw, area));
        var encodedFingerprint = Measure(iterations, () => RhodesRecognitionFrameFingerprint.Compute(encoded, area));
        var rawPreprocess = Measure(iterations, () => RhodesMaaRecognitionImagePreprocessor.Prepare(raw, "OCR", parameters, 2));
        var encodedPreprocess = Measure(iterations, () => RhodesMaaRecognitionImagePreprocessor.Prepare(encoded, "OCR", parameters, 2));
        Equal(
            FingerprintAreas(encoded, areas),
            FingerprintAreas(raw, areas),
            "same-frame multi-ROI raw and encoded fingerprints match before timing");
        var rawMultiRoi = Measure(iterations, () => FingerprintAreas(raw, areas));
        var encodedMultiRoi = Measure(iterations, () => FingerprintAreas(encoded, areas));

        ulong OldEncodedFrameFingerprint()
        {
            var frame = MaaOwnedImage.FromOwnedRaw((byte[])pixels.Clone(), width, height, 3, 16);
            return RhodesRecognitionFrameFingerprint.Compute(frame.EncodedImage, area);
        }

        ulong NewRawFrameFingerprint()
        {
            var frame = MaaOwnedImage.FromOwnedRaw((byte[])pixels.Clone(), width, height, 3, 16);
            return RhodesRecognitionFrameFingerprint.Compute(frame, area);
        }

        Equal(
            OldEncodedFrameFingerprint(),
            NewRawFrameFingerprint(),
            "new-frame raw and encoded fingerprints match before timing");
        var oldNewFrame = Measure(iterations, OldEncodedFrameFingerprint);
        var rawNewFrame = Measure(iterations, NewRawFrameFingerprint);
        Console.WriteLine(
            $"image-benchmark diagnostic-only iterations={iterations} "
            + $"same-frame.fingerprint.raw={rawFingerprint:0.###}ms same-frame.fingerprint.encoded={encodedFingerprint:0.###}ms "
            + $"same-frame.preprocess.raw={rawPreprocess:0.###}ms same-frame.preprocess.encoded={encodedPreprocess:0.###}ms "
            + $"same-frame.multi-roi.raw={rawMultiRoi:0.###}ms same-frame.multi-roi.encoded={encodedMultiRoi:0.###}ms "
            + $"new-frame.raw={rawNewFrame:0.###}ms new-frame.encode-and-fingerprint={oldNewFrame:0.###}ms");
    }

    private static double Measure<T>(int iterations, Func<T> action)
    {
        action();
        var timer = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
            action();
        timer.Stop();
        return timer.Elapsed.TotalMilliseconds / iterations;
    }

    private static ulong FingerprintAreas(
        MaaOwnedImage image,
        IEnumerable<RhodesRecognitionSwipeArea> areas)
    {
        ulong combined = 0;
        foreach (var area in areas)
            combined ^= RhodesRecognitionFrameFingerprint.Compute(image, area);
        return combined;
    }

    private static bool PixelsEquivalent(IReadOnlyList<SKColor> left, IReadOnlyList<SKColor> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (Math.Abs(left[index].Red - right[index].Red) > 1
                || Math.Abs(left[index].Green - right[index].Green) > 1
                || Math.Abs(left[index].Blue - right[index].Blue) > 1
                || Math.Abs(left[index].Alpha - right[index].Alpha) > 1)
            {
                return false;
            }
        }
        return true;
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
    }
}
