using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfElement
{
    #region Enum Parsers (B2)

    private static bool TryParsePresentationTransitionStyle(string? value, out OdfPresentationTransitionStyle transitionStyle)
    {
        switch (value)
        {
            case "none":
                transitionStyle = OdfPresentationTransitionStyle.None;
                return true;
            case "fade-from-left":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromLeft;
                return true;
            case "fade-from-top":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromTop;
                return true;
            case "fade-from-right":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromRight;
                return true;
            case "fade-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromBottom;
                return true;
            case "fade-from-upperleft":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromUpperLeft;
                return true;
            case "fade-from-upperright":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromUpperRight;
                return true;
            case "fade-from-lowerleft":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromLowerLeft;
                return true;
            case "fade-from-lowerright":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromLowerRight;
                return true;
            case "move-from-left":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromLeft;
                return true;
            case "move-from-top":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromTop;
                return true;
            case "move-from-right":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromRight;
                return true;
            case "move-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromBottom;
                return true;
            case "move-from-upperleft":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromUpperLeft;
                return true;
            case "move-from-upperright":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromUpperRight;
                return true;
            case "move-from-lowerleft":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromLowerLeft;
                return true;
            case "move-from-lowerright":
                transitionStyle = OdfPresentationTransitionStyle.MoveFromLowerRight;
                return true;
            case "uncover-to-left":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToLeft;
                return true;
            case "uncover-to-top":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToTop;
                return true;
            case "uncover-to-right":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToRight;
                return true;
            case "uncover-to-bottom":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToBottom;
                return true;
            case "uncover-to-upperleft":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToUpperLeft;
                return true;
            case "uncover-to-upperright":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToUpperRight;
                return true;
            case "uncover-to-lowerleft":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToLowerLeft;
                return true;
            case "uncover-to-lowerright":
                transitionStyle = OdfPresentationTransitionStyle.UncoverToLowerRight;
                return true;
            case "fade-to-center":
                transitionStyle = OdfPresentationTransitionStyle.FadeToCenter;
                return true;
            case "fade-from-center":
                transitionStyle = OdfPresentationTransitionStyle.FadeFromCenter;
                return true;
            case "vertical-stripes":
                transitionStyle = OdfPresentationTransitionStyle.VerticalStripes;
                return true;
            case "horizontal-stripes":
                transitionStyle = OdfPresentationTransitionStyle.HorizontalStripes;
                return true;
            case "clockwise":
                transitionStyle = OdfPresentationTransitionStyle.Clockwise;
                return true;
            case "counterclockwise":
                transitionStyle = OdfPresentationTransitionStyle.Counterclockwise;
                return true;
            case "open-vertical":
                transitionStyle = OdfPresentationTransitionStyle.OpenVertical;
                return true;
            case "open-horizontal":
                transitionStyle = OdfPresentationTransitionStyle.OpenHorizontal;
                return true;
            case "close-vertical":
                transitionStyle = OdfPresentationTransitionStyle.CloseVertical;
                return true;
            case "close-horizontal":
                transitionStyle = OdfPresentationTransitionStyle.CloseHorizontal;
                return true;
            case "wavyline-from-left":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromLeft;
                return true;
            case "wavyline-from-top":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromTop;
                return true;
            case "wavyline-from-right":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromRight;
                return true;
            case "wavyline-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.WavylineFromBottom;
                return true;
            case "spiralin-left":
                transitionStyle = OdfPresentationTransitionStyle.SpiralinLeft;
                return true;
            case "spiralin-right":
                transitionStyle = OdfPresentationTransitionStyle.SpiralinRight;
                return true;
            case "spiralout-left":
                transitionStyle = OdfPresentationTransitionStyle.SpiraloutLeft;
                return true;
            case "spiralout-right":
                transitionStyle = OdfPresentationTransitionStyle.SpiraloutRight;
                return true;
            case "roll-from-top":
                transitionStyle = OdfPresentationTransitionStyle.RollFromTop;
                return true;
            case "roll-from-left":
                transitionStyle = OdfPresentationTransitionStyle.RollFromLeft;
                return true;
            case "roll-from-right":
                transitionStyle = OdfPresentationTransitionStyle.RollFromRight;
                return true;
            case "roll-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.RollFromBottom;
                return true;
            case "stretch-from-left":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromLeft;
                return true;
            case "stretch-from-top":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromTop;
                return true;
            case "stretch-from-right":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromRight;
                return true;
            case "stretch-from-bottom":
                transitionStyle = OdfPresentationTransitionStyle.StretchFromBottom;
                return true;
            case "vertical-lines":
                transitionStyle = OdfPresentationTransitionStyle.VerticalLines;
                return true;
            case "horizontal-lines":
                transitionStyle = OdfPresentationTransitionStyle.HorizontalLines;
                return true;
            case "dissolve":
                transitionStyle = OdfPresentationTransitionStyle.Dissolve;
                return true;
            case "random":
                transitionStyle = OdfPresentationTransitionStyle.Random;
                return true;
            case "vertical-checkerboard":
                transitionStyle = OdfPresentationTransitionStyle.VerticalCheckerboard;
                return true;
            case "horizontal-checkerboard":
                transitionStyle = OdfPresentationTransitionStyle.HorizontalCheckerboard;
                return true;
            case "interlocking-horizontal-left":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingHorizontalLeft;
                return true;
            case "interlocking-horizontal-right":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingHorizontalRight;
                return true;
            case "interlocking-vertical-top":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingVerticalTop;
                return true;
            case "interlocking-vertical-bottom":
                transitionStyle = OdfPresentationTransitionStyle.InterlockingVerticalBottom;
                return true;
            case "fly-away":
                transitionStyle = OdfPresentationTransitionStyle.FlyAway;
                return true;
            case "open":
                transitionStyle = OdfPresentationTransitionStyle.Open;
                return true;
            case "close":
                transitionStyle = OdfPresentationTransitionStyle.Close;
                return true;
            case "melt":
                transitionStyle = OdfPresentationTransitionStyle.Melt;
                return true;
            default:
                transitionStyle = default;
                return false;
        }
    }

    private static string FormatPresentationTransitionStyle(OdfPresentationTransitionStyle transitionStyle)
    {
        return transitionStyle switch
        {
            OdfPresentationTransitionStyle.None => "none",
            OdfPresentationTransitionStyle.FadeFromLeft => "fade-from-left",
            OdfPresentationTransitionStyle.FadeFromTop => "fade-from-top",
            OdfPresentationTransitionStyle.FadeFromRight => "fade-from-right",
            OdfPresentationTransitionStyle.FadeFromBottom => "fade-from-bottom",
            OdfPresentationTransitionStyle.FadeFromUpperLeft => "fade-from-upperleft",
            OdfPresentationTransitionStyle.FadeFromUpperRight => "fade-from-upperright",
            OdfPresentationTransitionStyle.FadeFromLowerLeft => "fade-from-lowerleft",
            OdfPresentationTransitionStyle.FadeFromLowerRight => "fade-from-lowerright",
            OdfPresentationTransitionStyle.MoveFromLeft => "move-from-left",
            OdfPresentationTransitionStyle.MoveFromTop => "move-from-top",
            OdfPresentationTransitionStyle.MoveFromRight => "move-from-right",
            OdfPresentationTransitionStyle.MoveFromBottom => "move-from-bottom",
            OdfPresentationTransitionStyle.MoveFromUpperLeft => "move-from-upperleft",
            OdfPresentationTransitionStyle.MoveFromUpperRight => "move-from-upperright",
            OdfPresentationTransitionStyle.MoveFromLowerLeft => "move-from-lowerleft",
            OdfPresentationTransitionStyle.MoveFromLowerRight => "move-from-lowerright",
            OdfPresentationTransitionStyle.UncoverToLeft => "uncover-to-left",
            OdfPresentationTransitionStyle.UncoverToTop => "uncover-to-top",
            OdfPresentationTransitionStyle.UncoverToRight => "uncover-to-right",
            OdfPresentationTransitionStyle.UncoverToBottom => "uncover-to-bottom",
            OdfPresentationTransitionStyle.UncoverToUpperLeft => "uncover-to-upperleft",
            OdfPresentationTransitionStyle.UncoverToUpperRight => "uncover-to-upperright",
            OdfPresentationTransitionStyle.UncoverToLowerLeft => "uncover-to-lowerleft",
            OdfPresentationTransitionStyle.UncoverToLowerRight => "uncover-to-lowerright",
            OdfPresentationTransitionStyle.FadeToCenter => "fade-to-center",
            OdfPresentationTransitionStyle.FadeFromCenter => "fade-from-center",
            OdfPresentationTransitionStyle.VerticalStripes => "vertical-stripes",
            OdfPresentationTransitionStyle.HorizontalStripes => "horizontal-stripes",
            OdfPresentationTransitionStyle.Clockwise => "clockwise",
            OdfPresentationTransitionStyle.Counterclockwise => "counterclockwise",
            OdfPresentationTransitionStyle.OpenVertical => "open-vertical",
            OdfPresentationTransitionStyle.OpenHorizontal => "open-horizontal",
            OdfPresentationTransitionStyle.CloseVertical => "close-vertical",
            OdfPresentationTransitionStyle.CloseHorizontal => "close-horizontal",
            OdfPresentationTransitionStyle.WavylineFromLeft => "wavyline-from-left",
            OdfPresentationTransitionStyle.WavylineFromTop => "wavyline-from-top",
            OdfPresentationTransitionStyle.WavylineFromRight => "wavyline-from-right",
            OdfPresentationTransitionStyle.WavylineFromBottom => "wavyline-from-bottom",
            OdfPresentationTransitionStyle.SpiralinLeft => "spiralin-left",
            OdfPresentationTransitionStyle.SpiralinRight => "spiralin-right",
            OdfPresentationTransitionStyle.SpiraloutLeft => "spiralout-left",
            OdfPresentationTransitionStyle.SpiraloutRight => "spiralout-right",
            OdfPresentationTransitionStyle.RollFromTop => "roll-from-top",
            OdfPresentationTransitionStyle.RollFromLeft => "roll-from-left",
            OdfPresentationTransitionStyle.RollFromRight => "roll-from-right",
            OdfPresentationTransitionStyle.RollFromBottom => "roll-from-bottom",
            OdfPresentationTransitionStyle.StretchFromLeft => "stretch-from-left",
            OdfPresentationTransitionStyle.StretchFromTop => "stretch-from-top",
            OdfPresentationTransitionStyle.StretchFromRight => "stretch-from-right",
            OdfPresentationTransitionStyle.StretchFromBottom => "stretch-from-bottom",
            OdfPresentationTransitionStyle.VerticalLines => "vertical-lines",
            OdfPresentationTransitionStyle.HorizontalLines => "horizontal-lines",
            OdfPresentationTransitionStyle.Dissolve => "dissolve",
            OdfPresentationTransitionStyle.Random => "random",
            OdfPresentationTransitionStyle.VerticalCheckerboard => "vertical-checkerboard",
            OdfPresentationTransitionStyle.HorizontalCheckerboard => "horizontal-checkerboard",
            OdfPresentationTransitionStyle.InterlockingHorizontalLeft => "interlocking-horizontal-left",
            OdfPresentationTransitionStyle.InterlockingHorizontalRight => "interlocking-horizontal-right",
            OdfPresentationTransitionStyle.InterlockingVerticalTop => "interlocking-vertical-top",
            OdfPresentationTransitionStyle.InterlockingVerticalBottom => "interlocking-vertical-bottom",
            OdfPresentationTransitionStyle.FlyAway => "fly-away",
            OdfPresentationTransitionStyle.Open => "open",
            OdfPresentationTransitionStyle.Close => "close",
            OdfPresentationTransitionStyle.Melt => "melt",
            _ => throw new ArgumentOutOfRangeException(nameof(transitionStyle), transitionStyle, "未知的 ODF 簡報轉場樣式。")
        };
    }


    #endregion
}
