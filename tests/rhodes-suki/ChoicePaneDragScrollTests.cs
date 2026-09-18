using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using RhodesSuki.Views.Workspaces;

internal static class ChoicePaneDragScrollTests
{
    public static void ScrollBarKeepsItsGesture()
    {
        var thumb = new Thumb();
        var trackSurface = new Border { Child = thumb };
        var scrollBar = new ScrollBar
        {
            Template = new FuncControlTemplate<ScrollBar>((_, _) => trackSurface),
        };
        scrollBar.ApplyTemplate();
        Require(!ChoicesWorkspaceView.CanStartChoicePaneDrag(scrollBar), "The scroll bar must own its drag.");
        Require(!ChoicesWorkspaceView.CanStartChoicePaneDrag(trackSurface), "Track clicks must reach the scroll bar.");
        Require(!ChoicesWorkspaceView.CanStartChoicePaneDrag(thumb), "The thumb must retain pointer capture.");

        var cardText = new TextBlock();
        var card = new Border { Child = cardText };
        Require(ChoicesWorkspaceView.CanStartChoicePaneDrag(card)
            && ChoicesWorkspaceView.CanStartChoicePaneDrag(cardText), "Catalog content must still allow drag scrolling.");
        Require(!ChoicesWorkspaceView.CanStartChoicePaneDrag(null), "A missing input source must not start a drag.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
