namespace RhodesSuki.Services;

public static class RhodesChoicePaneDragScroll
{
    public const double StartThreshold = 6;

    public static double OffsetForDrag(
        double startOffset,
        double pointerDelta,
        double extent,
        double viewport)
    {
        var maximum = Math.Max(0, extent - viewport);
        return Math.Clamp(startOffset - pointerDelta, 0, maximum);
    }
}
