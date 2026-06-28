using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const string OoxmlCompatNs = "urn:odfkit:ooxml:compatibility";

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
                index,
                current.ParagraphStartIndex,
                current.ParagraphEndIndex);
        }

        return animations.AsReadOnly();
    }

    private static void CollectAnimations(OdfNode node, List<OdfAnimationInfo> animations)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != AnimNs)
            {
                continue;
            }

            if (child.LocalName == "par" && child.GetAttribute("preset-class", OdfNamespaces.Presentation) is not null)
            {
                TryAddAnimation(child, animations);
            }
            else if (child.LocalName == "transitionFilter")
            {
                TryAddTransitionFilterAnimation(child, animations);
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
            effectPar.GetAttribute("begin", SmilNs),
            paragraphStartIndex: ReadNullableNonNegativeInt(effectPar, "pptx-paragraph-start"),
            paragraphEndIndex: ReadNullableNonNegativeInt(effectPar, "pptx-paragraph-end")));
    }

    private static void TryAddTransitionFilterAnimation(OdfNode filter, List<OdfAnimationInfo> animations)
    {
        string? targetElementId = filter.GetAttribute("targetElement", SmilNs);
        if (string.IsNullOrEmpty(targetElementId))
            return;

        OdfAnimationKind kind = filter.GetAttribute("mode", SmilNs) == "out"
            ? OdfAnimationKind.Exit
            : OdfAnimationKind.Entrance;

        animations.Add(new OdfAnimationInfo(
            kind,
            targetElementId!,
            ParseFilterEffect(filter.GetAttribute("type", SmilNs)),
            ResolveEffectTrigger(filter),
            filter.GetAttribute("type", SmilNs),
            filter.GetAttribute("dur", SmilNs),
            filter.GetAttribute("begin", SmilNs)));
    }

    private static int? ReadNullableNonNegativeInt(OdfNode node, string localName)
    {
        string? value = node.GetAttribute(localName, OoxmlCompatNs);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0 ? parsed : null;
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

        OdfNode? clickStep = FindAncestorClickStep(effectPar) ?? FindAncestorClickSequence(effectPar);
        if (clickStep is null)
            return OdfAnimationTrigger.AfterPrevious;

        return IsFirstAnimationInClickStep(effectPar, clickStep)
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

            if (current.GetAttribute("node-type", OdfNamespaces.Presentation) == "on-click")
                return current;
        }

        return null;
    }

    private static OdfNode? FindAncestorClickSequence(OdfNode node)
    {
        for (OdfNode? current = node.Parent; current is not null; current = current.Parent)
        {
            if (current.NodeType is not OdfNodeType.Element ||
                current.NamespaceUri != AnimNs ||
                current.LocalName is not ("seq" or "par"))
                continue;

            string? begin = current.GetAttribute("begin", SmilNs);
            if (string.Equals(begin, "click", StringComparison.OrdinalIgnoreCase))
                return current;
        }

        return null;
    }

    private static bool IsFirstAnimationInClickStep(OdfNode effectNode, OdfNode clickStep)
    {
        foreach (OdfNode child in EnumerateAnimationNodes(clickStep))
        {
            return ReferenceEquals(child, effectNode);
        }

        return false;
    }

    private static IEnumerable<OdfNode> EnumerateAnimationNodes(OdfNode node)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != AnimNs)
                continue;

            if ((child.LocalName == "par" && child.GetAttribute("preset-class", OdfNamespaces.Presentation) is not null) ||
                child.LocalName == "transitionFilter")
            {
                yield return child;
                continue;
            }

            foreach (OdfNode descendant in EnumerateAnimationNodes(child))
            {
                yield return descendant;
            }
        }
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

    private static OdfAnimationEffect ParseFilterEffect(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return OdfAnimationEffect.Appear;

        if (type.Contains("fade", StringComparison.OrdinalIgnoreCase))
            return OdfAnimationEffect.Fade;
        if (type.Contains("zoom", StringComparison.OrdinalIgnoreCase))
            return OdfAnimationEffect.Zoom;
        if (type.Contains("fly", StringComparison.OrdinalIgnoreCase))
            return OdfAnimationEffect.FlyIn;

        return OdfAnimationEffect.Appear;
    }
}
