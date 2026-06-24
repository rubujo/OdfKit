using System;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// ODF 元素 schema 枚舉 token 靜態註冊表（部分檔案）。
/// </summary>
internal static partial class OdfElementSchemaRegistry
{
    #region Schema Registry - Presentation Effect & Transition

    internal static bool TryParsePresentationEffect(string? value, out OdfPresentationEffect effect)
    {
        switch (value)
        {
            case "none":
                effect = OdfPresentationEffect.None;
                return true;
            case "fade":
                effect = OdfPresentationEffect.Fade;
                return true;
            case "move":
                effect = OdfPresentationEffect.Move;
                return true;
            case "stripes":
                effect = OdfPresentationEffect.Stripes;
                return true;
            case "open":
                effect = OdfPresentationEffect.Open;
                return true;
            case "close":
                effect = OdfPresentationEffect.Close;
                return true;
            case "dissolve":
                effect = OdfPresentationEffect.Dissolve;
                return true;
            case "wavyline":
                effect = OdfPresentationEffect.Wavyline;
                return true;
            case "random":
                effect = OdfPresentationEffect.Random;
                return true;
            case "lines":
                effect = OdfPresentationEffect.Lines;
                return true;
            case "laser":
                effect = OdfPresentationEffect.Laser;
                return true;
            case "appear":
                effect = OdfPresentationEffect.Appear;
                return true;
            case "hide":
                effect = OdfPresentationEffect.Hide;
                return true;
            case "move-short":
                effect = OdfPresentationEffect.MoveShort;
                return true;
            case "checkerboard":
                effect = OdfPresentationEffect.Checkerboard;
                return true;
            case "rotate":
                effect = OdfPresentationEffect.Rotate;
                return true;
            case "stretch":
                effect = OdfPresentationEffect.Stretch;
                return true;
            default:
                effect = default;
                return false;
        }
    }

    internal static string FormatPresentationEffect(OdfPresentationEffect effect)
    {
        return effect switch
        {
            OdfPresentationEffect.None => "none",
            OdfPresentationEffect.Fade => "fade",
            OdfPresentationEffect.Move => "move",
            OdfPresentationEffect.Stripes => "stripes",
            OdfPresentationEffect.Open => "open",
            OdfPresentationEffect.Close => "close",
            OdfPresentationEffect.Dissolve => "dissolve",
            OdfPresentationEffect.Wavyline => "wavyline",
            OdfPresentationEffect.Random => "random",
            OdfPresentationEffect.Lines => "lines",
            OdfPresentationEffect.Laser => "laser",
            OdfPresentationEffect.Appear => "appear",
            OdfPresentationEffect.Hide => "hide",
            OdfPresentationEffect.MoveShort => "move-short",
            OdfPresentationEffect.Checkerboard => "checkerboard",
            OdfPresentationEffect.Rotate => "rotate",
            OdfPresentationEffect.Stretch => "stretch",
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfBriefingEffect"))
        };
    }

    internal static bool TryParsePresentationSpeed(string? value, out OdfPresentationSpeed speed)
    {
        switch (value)
        {
            case "slow":
                speed = OdfPresentationSpeed.Slow;
                return true;
            case "medium":
                speed = OdfPresentationSpeed.Medium;
                return true;
            case "fast":
                speed = OdfPresentationSpeed.Fast;
                return true;
            default:
                speed = default;
                return false;
        }
    }

    internal static string FormatPresentationSpeed(OdfPresentationSpeed speed)
    {
        return speed switch
        {
            OdfPresentationSpeed.Slow => "slow",
            OdfPresentationSpeed.Medium => "medium",
            OdfPresentationSpeed.Fast => "fast",
            _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfBriefingSpeed"))
        };
    }

    internal static bool TryParsePresentationAction(string? value, out OdfPresentationAction action)
    {
        switch (value)
        {
            case "none":
                action = OdfPresentationAction.None;
                return true;
            case "previous-page":
                action = OdfPresentationAction.PreviousPage;
                return true;
            case "next-page":
                action = OdfPresentationAction.NextPage;
                return true;
            case "first-page":
                action = OdfPresentationAction.FirstPage;
                return true;
            case "last-page":
                action = OdfPresentationAction.LastPage;
                return true;
            case "hide":
                action = OdfPresentationAction.Hide;
                return true;
            case "stop":
                action = OdfPresentationAction.Stop;
                return true;
            case "execute":
                action = OdfPresentationAction.Execute;
                return true;
            case "show":
                action = OdfPresentationAction.Show;
                return true;
            case "verb":
                action = OdfPresentationAction.Verb;
                return true;
            case "fade-out":
                action = OdfPresentationAction.FadeOut;
                return true;
            case "sound":
                action = OdfPresentationAction.Sound;
                return true;
            case "last-visited-page":
                action = OdfPresentationAction.LastVisitedPage;
                return true;
            default:
                action = default;
                return false;
        }
    }

    internal static string FormatPresentationAction(OdfPresentationAction action)
    {
        return action switch
        {
            OdfPresentationAction.None => "none",
            OdfPresentationAction.PreviousPage => "previous-page",
            OdfPresentationAction.NextPage => "next-page",
            OdfPresentationAction.FirstPage => "first-page",
            OdfPresentationAction.LastPage => "last-page",
            OdfPresentationAction.Hide => "hide",
            OdfPresentationAction.Stop => "stop",
            OdfPresentationAction.Execute => "execute",
            OdfPresentationAction.Show => "show",
            OdfPresentationAction.Verb => "verb",
            OdfPresentationAction.FadeOut => "fade-out",
            OdfPresentationAction.Sound => "sound",
            OdfPresentationAction.LastVisitedPage => "last-visited-page",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, OdfLocalizer.GetMessage("Err_OdfElementSchemaRegistry_UnknownOdfBriefingAction"))
        };
    }

    internal static bool TryParsePresentationTransitionType(string? value, out OdfPresentationTransitionType transitionType)
    {
        switch (value)
        {
            case "manual":
                transitionType = OdfPresentationTransitionType.Manual;
                return true;
            case "automatic":
                transitionType = OdfPresentationTransitionType.Automatic;
                return true;
            case "semi-automatic":
                transitionType = OdfPresentationTransitionType.SemiAutomatic;
                return true;
            default:
                transitionType = default;
                return false;
        }
    }

    internal static string FormatPresentationTransitionType(OdfPresentationTransitionType transitionType)
    {
        if (transitionType == OdfPresentationTransitionType.Manual)
        {
            return "manual";
        }

        if (transitionType == OdfPresentationTransitionType.Automatic)
        {
            return "automatic";
        }

        if (transitionType == OdfPresentationTransitionType.SemiAutomatic)
        {
            return "semi-automatic";
        }

        return transitionType.Value;
    }

    #endregion
}
