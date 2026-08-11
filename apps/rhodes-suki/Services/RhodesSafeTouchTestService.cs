using System.Security.Cryptography;
using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesSafeTouchTestService
{
    public const int CanvasWidth = 1280;
    public const int CanvasHeight = 720;

    public static SukiTouchTestConfirmation CreateConfirmation(
        SukiTouchRectangle rectangle,
        DateTimeOffset? now = null,
        Func<int, int, int>? nextInt = null)
    {
        now ??= DateTimeOffset.UtcNow;
        var validation = Validate(rectangle);
        if (validation.Length > 0)
        {
            return new SukiTouchTestConfirmation(
                Guid.Empty,
                rectangle,
                new SukiTouchPoint(0, 0),
                now.Value,
                false,
                validation);
        }

        nextInt ??= RandomNumberGenerator.GetInt32;
        var x = nextInt(rectangle.X, checked(rectangle.X + rectangle.Width));
        var y = nextInt(rectangle.Y, checked(rectangle.Y + rectangle.Height));
        return new SukiTouchTestConfirmation(
            Guid.NewGuid(),
            rectangle,
            new SukiTouchPoint(x, y),
            now.Value.AddSeconds(30),
            true,
            $"1280x720内の矩形 ({rectangle.X},{rectangle.Y},{rectangle.Width},{rectangle.Height}) から ({x},{y}) を選びました。確認後1回だけタップします。");
    }

    public static async Task<SukiTouchTestResult> ExecuteAsync(
        SukiTouchTestConfirmation confirmation,
        bool confirmed,
        Func<int, int, CancellationToken, Task<MaaJobStatus>> tapAsync,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tapAsync);
        now ??= DateTimeOffset.UtcNow;
        if (!confirmation.IsValid || confirmation.Token == Guid.Empty)
            return new SukiTouchTestResult(false, confirmation.Detail, MaaJobStatus.Invalid);
        if (!confirmed)
            return new SukiTouchTestResult(false, "タッチテストは確認されていないため実行しませんでした。", MaaJobStatus.Invalid);
        if (now.Value > confirmation.ExpiresAt)
            return new SukiTouchTestResult(false, "タッチテストの確認期限が切れました。再度準備してください。", MaaJobStatus.Invalid);
        var rectangleValidation = Validate(confirmation.Rectangle);
        if (rectangleValidation.Length > 0)
            return new SukiTouchTestResult(false, rectangleValidation, MaaJobStatus.Invalid);
        if (!Contains(confirmation.Rectangle, confirmation.Point))
            return new SukiTouchTestResult(false, "タップ点が確認済み矩形の外にあるため拒否しました。", MaaJobStatus.Invalid);

        var status = await tapAsync(confirmation.Point.X, confirmation.Point.Y, cancellationToken);
        return new SukiTouchTestResult(
            status == MaaJobStatus.Succeeded,
            status == MaaJobStatus.Succeeded
                ? $"確認済み点 ({confirmation.Point.X},{confirmation.Point.Y}) を1回タップしました。"
                : $"確認済みタップに失敗しました: {status}",
            status);
    }

    public static string Validate(SukiTouchRectangle rectangle)
    {
        if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.Width <= 0 || rectangle.Height <= 0)
            return "タッチ矩形はX/Yが0以上、幅/高さが1以上である必要があります。";
        var right = (long)rectangle.X + rectangle.Width;
        var bottom = (long)rectangle.Y + rectangle.Height;
        return right > CanvasWidth || bottom > CanvasHeight
            ? "タッチ矩形は1280x720の範囲内に収めてください。"
            : "";
    }

    private static bool Contains(SukiTouchRectangle rectangle, SukiTouchPoint point)
    {
        return point.X >= rectangle.X
            && (long)point.X < (long)rectangle.X + rectangle.Width
            && point.Y >= rectangle.Y
            && (long)point.Y < (long)rectangle.Y + rectangle.Height;
    }
}
