using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 投影片動畫讀取引擎（內部協作者）。
/// </summary>
internal static class OdfSlideAnimationReadEngine
{
    private const string AnimNs = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";
    private const string SmilNs = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";

    internal static IReadOnlyList<OdfAnimationInfo> GetAnimations(OdfSlide slide)
    {
        List<OdfAnimationInfo> animations = [];
        CollectAnimations(slide.AnimationRoot.Node, animations);
        for (int index = 0; index < animations.Count; index++)
        {
            OdfAnimationInfo current = animations[index];
            animations[index] = new OdfAnimationInfo(
                current.Kind,
                current.TargetElementId,
                current.Effect,
                current.Trigger,
                current.PresetId,
                current.Duration,
                current.Begin,
                index);
        }

        return animations.AsReadOnly();
    }

    private static void CollectAnimations(OdfNode node, List<OdfAnimationInfo> animations)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != AnimNs || child.LocalName != "par")
                continue;

            if (child.GetAttribute("preset-class", OdfNamespaces.Presentation) is not null)
            {
                TryAddAnimation(child, animations);
            }
            else
            {
                CollectAnimations(child, animations);
            }
        }
    }

    private static void TryAddAnimation(OdfNode effectPar, List<OdfAnimationInfo> animations)
    {
        string? presetClass = effectPar.GetAttribute("preset-class", OdfNamespaces.Presentation);
        string? presetId = effectPar.GetAttribute("preset-id", OdfNamespaces.Presentation);
        if (string.IsNullOrEmpty(presetClass))
            return;

        OdfAnimationKind kind = presetClass switch
        {
            "exit" => OdfAnimationKind.Exit,
            "emphasis" => OdfAnimationKind.Emphasis,
            _ => OdfAnimationKind.Entrance,
        };

        string? targetElementId = FindTargetElementId(effectPar);
        if (string.IsNullOrEmpty(targetElementId))
            return;

        animations.Add(new OdfAnimationInfo(
            kind,
            targetElementId!,
            ParseEffect(presetId, kind),
            ResolveEffectTrigger(effectPar),
            presetId,
            effectPar.GetAttribute("dur", SmilNs),
            effectPar.GetAttribute("begin", SmilNs)));
    }

    private static string? FindTargetElementId(OdfNode effectPar)
    {
        foreach (OdfNode child in effectPar.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != AnimNs)
                continue;

            string? target = child.GetAttribute("targetElement", SmilNs);
            if (!string.IsNullOrEmpty(target))
                return target;
        }

        return null;
    }

    private static OdfAnimationTrigger ResolveEffectTrigger(OdfNode effectPar)
    {
        string? begin = effectPar.GetAttribute("begin", SmilNs);
        if (begin?.StartsWith("prev.end", StringComparison.Ordinal) == true)
            return OdfAnimationTrigger.AfterPrevious;

        OdfNode? clickStep = FindAncestorClickStep(effectPar);
        if (clickStep is null)
            return OdfAnimationTrigger.AfterPrevious;

        return IsFirstEffectInClickStep(effectPar, clickStep)
            ? OdfAnimationTrigger.OnClick
            : OdfAnimationTrigger.WithPrevious;
    }

    private static OdfNode? FindAncestorClickStep(OdfNode node)
    {
        for (OdfNode? current = node.Parent; current is not null; current = current.Parent)
        {
            if (current.NodeType is not OdfNodeType.Element ||
                current.NamespaceUri != AnimNs ||
                current.LocalName != "par")
                continue;

            if (current.GetAttribute("begin", SmilNs) == "on-click")
                return current;
        }

        return null;
    }

    private static bool IsFirstEffectInClickStep(OdfNode effectPar, OdfNode clickStep)
    {
        foreach (OdfNode sibling in clickStep.Children)
        {
            if (sibling.NodeType is not OdfNodeType.Element ||
                sibling.NamespaceUri != AnimNs ||
                sibling.LocalName != "par")
                continue;

            if (sibling.GetAttribute("preset-class", OdfNamespaces.Presentation) is null)
                continue;

            return ReferenceEquals(sibling, effectPar);
        }

        return false;
    }

    private static OdfAnimationEffect ParseEffect(string? presetId, OdfAnimationKind kind)
    {
        if (string.IsNullOrEmpty(presetId))
            return OdfAnimationEffect.Appear;

        if (presetId.Contains("fade", StringComparison.OrdinalIgnoreCase))
            return OdfAnimationEffect.Fade;
        if (presetId.Contains("zoom", StringComparison.OrdinalIgnoreCase))
            return OdfAnimationEffect.Zoom;
        if (presetId.Contains("fly", StringComparison.OrdinalIgnoreCase))
            return OdfAnimationEffect.FlyIn;

        return kind == OdfAnimationKind.Emphasis ? OdfAnimationEffect.Appear : OdfAnimationEffect.Appear;
    }
}
