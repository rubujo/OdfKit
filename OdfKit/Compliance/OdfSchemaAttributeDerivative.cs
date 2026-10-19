using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 在選擇、可選與重複模式中保留屬性與內容的關聯，避免兩種投影採用不同分支。
/// </summary>
internal sealed class OdfSchemaAttributeDerivative(OdfSchemaSet schema)
{
    // 屬性衍生及起始標籤結束規則：https://relaxng.org/jclark/derivative.html
    private readonly Dictionary<OdfSchemaPatternNode, Features> _features = new();
    private readonly Dictionary<string, OdfSchemaPatternNode> _references = new(StringComparer.Ordinal);
    private Dictionary<OdfSchemaPatternNode, OdfSchemaPatternNode>? _constructed;
    private static readonly OdfSchemaPatternNode Empty = Make(OdfSchemaPatternNodeKind.Empty, []);
    private static readonly OdfSchemaPatternNode NotAllowed = Make(OdfSchemaPatternNodeKind.NotAllowed, []);
    private readonly record struct Features(bool Attribute, bool Content, bool Coupled);

    private sealed class StructuralComparer : IEqualityComparer<OdfSchemaPatternNode>
    {
        bool IEqualityComparer<OdfSchemaPatternNode>.Equals(OdfSchemaPatternNode? left, OdfSchemaPatternNode? right) =>
            ReferenceEquals(left, right) || left is not null && right is not null &&
            left.Kind == right.Kind && left.Children.SequenceEqual(right.Children);

        int IEqualityComparer<OdfSchemaPatternNode>.GetHashCode(OdfSchemaPatternNode node)
        {
            int hash = (int)node.Kind;
            foreach (OdfSchemaPatternNode child in node.Children)
                hash = unchecked(hash * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(child));
            return hash;
        }
    }

    internal bool RequiresResolution(IReadOnlyList<OdfSchemaPatternNode> nodes) =>
        nodes.Any(node => Analyze(node, new HashSet<string>(StringComparer.Ordinal)).Coupled);

    private Features Analyze(OdfSchemaPatternNode node, HashSet<string> active)
    {
        if (_features.TryGetValue(node, out Features found))
            return found;
        if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
            return new(true, false, false);
        if (node.Kind is OdfSchemaPatternNodeKind.Element or OdfSchemaPatternNodeKind.AnyName or
            OdfSchemaPatternNodeKind.NamespaceName or OdfSchemaPatternNodeKind.Name or
            OdfSchemaPatternNodeKind.Text or OdfSchemaPatternNodeKind.Data or
            OdfSchemaPatternNodeKind.Value or OdfSchemaPatternNodeKind.List)
            return new(false, true, false);
        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            if (!active.Add(node.ReferenceName))
                return default;
            Features referenced = Analyze(Reference(node.ReferenceName), active);
            active.Remove(node.ReferenceName);
            return referenced;
        }
        bool attribute = false, content = node.Kind == OdfSchemaPatternNodeKind.Mixed, coupled = false;
        foreach (OdfSchemaPatternNode child in node.Children)
        {
            Features feature = Analyze(child, active);
            attribute |= feature.Attribute;
            content |= feature.Content;
            coupled |= feature.Coupled;
        }
        coupled |= attribute && content && node.Kind is (OdfSchemaPatternNodeKind.Choice or
            OdfSchemaPatternNodeKind.Optional or OdfSchemaPatternNodeKind.ZeroOrMore or OdfSchemaPatternNodeKind.OneOrMore);
        Features result = new(attribute, content, coupled);
        _features[node] = result;
        return result;
    }

    internal OdfSchemaPatternNode Resolve(
        IReadOnlyList<OdfSchemaPatternNode> nodes, XElement element, OdfSchemaPatternMatchContext context)
    {
        // 節點合併只在目前元素的屬性階段保留；同構分支共用節點，避免選擇展開時重複配置。
        _constructed = new(new StructuralComparer());
        try
        {
            OdfSchemaPatternNode residual = Combine(OdfSchemaPatternNodeKind.Group, nodes);
            foreach (XAttribute attribute in element.Attributes())
            {
                if (!attribute.IsNamespaceDeclaration)
                    residual = Derive(residual, attribute, context, new(), new(StringComparer.Ordinal));
            }
            return Close(residual, new(), new(StringComparer.Ordinal));
        }
        finally
        {
            _constructed = null;
        }
    }

    private OdfSchemaPatternNode Derive(
        OdfSchemaPatternNode node, XAttribute attribute, OdfSchemaPatternMatchContext context,
        Dictionary<OdfSchemaPatternNode, OdfSchemaPatternNode> memo, HashSet<string> active)
    {
        if (memo.TryGetValue(node, out OdfSchemaPatternNode? cached))
            return cached;
        OdfSchemaPatternNode result;
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
                result = OdfSchemaPatternAttributeMatcher.AttributePatternAllowsAttribute(node, attribute, context) ? Empty : NotAllowed;
                break;
            case OdfSchemaPatternNodeKind.Ref:
                if (!active.Add(node.ReferenceName))
                    return NotAllowed;
                result = Derive(Reference(node.ReferenceName), attribute, context, memo, active);
                active.Remove(node.ReferenceName);
                break;
            case OdfSchemaPatternNodeKind.Choice:
                result = Combine(node.Kind, node.Children.Select(child => Derive(child, attribute, context, memo, active)));
                break;
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Other:
            case OdfSchemaPatternNodeKind.Mixed:
                var alternatives = new List<OdfSchemaPatternNode>();
                for (int index = 0; index < node.Children.Count; index++)
                {
                    OdfSchemaPatternNode replacement = Derive(node.Children[index], attribute, context, memo, active);
                    if (replacement.Kind == OdfSchemaPatternNodeKind.NotAllowed)
                        continue;
                    OdfSchemaPatternNode[] children = node.Children.ToArray();
                    children[index] = replacement;
                    alternatives.Add(Combine(node.Kind, children));
                }
                result = Combine(OdfSchemaPatternNodeKind.Choice, alternatives);
                break;
            case OdfSchemaPatternNodeKind.Optional:
                result = Derive(Combine(OdfSchemaPatternNodeKind.Group, node.Children), attribute, context, memo, active);
                break;
            case OdfSchemaPatternNodeKind.ZeroOrMore:
            case OdfSchemaPatternNodeKind.OneOrMore:
                OdfSchemaPatternNode body = Combine(OdfSchemaPatternNodeKind.Group, node.Children);
                result = Combine(OdfSchemaPatternNodeKind.Group,
                    [Derive(body, attribute, context, memo, active), Intern(Make(OdfSchemaPatternNodeKind.ZeroOrMore, [body]))]);
                break;
            default:
                result = NotAllowed;
                break;
        }
        memo[node] = result;
        return result;
    }

    private OdfSchemaPatternNode Close(OdfSchemaPatternNode node,
        Dictionary<OdfSchemaPatternNode, OdfSchemaPatternNode> memo, HashSet<string> active)
    {
        if (memo.TryGetValue(node, out OdfSchemaPatternNode? cached))
            return cached;
        OdfSchemaPatternNode result;
        if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
            result = NotAllowed;
        else if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            if (!active.Add(node.ReferenceName))
                return NotAllowed;
            result = Close(Reference(node.ReferenceName), memo, active);
            active.Remove(node.ReferenceName);
        }
        else if (node.Kind is OdfSchemaPatternNodeKind.Group or OdfSchemaPatternNodeKind.Interleave or
            OdfSchemaPatternNodeKind.Choice or OdfSchemaPatternNodeKind.Optional or OdfSchemaPatternNodeKind.ZeroOrMore or
            OdfSchemaPatternNodeKind.OneOrMore or OdfSchemaPatternNodeKind.Mixed or OdfSchemaPatternNodeKind.Other)
            result = Combine(node.Kind, node.Children.Select(child => Close(child, memo, active)));
        else
            result = node;
        memo[node] = result;
        return result;
    }

    private OdfSchemaPatternNode Reference(string name)
    {
        if (!_references.TryGetValue(name, out OdfSchemaPatternNode? root))
        {
            OdfSchemaPatternDefinition? pattern = schema.FindPattern(name);
            // 多個根是 combine="choice" 的替代項；sequence/interleave 已包成單一根。
            root = pattern is null ? NotAllowed : Combine(OdfSchemaPatternNodeKind.Choice, pattern.Roots);
            _references[name] = root;
        }
        return root;
    }

    private OdfSchemaPatternNode Combine(OdfSchemaPatternNodeKind kind, IEnumerable<OdfSchemaPatternNode> nodes)
    {
        var children = new List<OdfSchemaPatternNode>();
        var seen = new HashSet<OdfSchemaPatternNode>();
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (kind == OdfSchemaPatternNodeKind.Choice)
            {
                if (node.Kind == OdfSchemaPatternNodeKind.NotAllowed)
                    continue;
                foreach (OdfSchemaPatternNode alternative in node.Kind == kind ? node.Children : new[] { node })
                {
                    if (seen.Add(alternative))
                        children.Add(alternative);
                }
            }
            else
            {
                if (node.Kind == OdfSchemaPatternNodeKind.NotAllowed)
                    return kind is OdfSchemaPatternNodeKind.Optional or OdfSchemaPatternNodeKind.ZeroOrMore ? Empty : NotAllowed;
                if (node.Kind != OdfSchemaPatternNodeKind.Empty)
                    children.Add(node);
            }
        }
        if (children.Count == 0 && kind == OdfSchemaPatternNodeKind.Mixed)
            return Intern(Make(kind, [Empty]));
        if (children.Count == 0)
            return kind == OdfSchemaPatternNodeKind.Choice ? NotAllowed : Empty;
        if (kind == OdfSchemaPatternNodeKind.Optional)
            return Combine(OdfSchemaPatternNodeKind.Choice, [Empty, Combine(OdfSchemaPatternNodeKind.Group, children)]);
        if (children.Count == 1 && kind is OdfSchemaPatternNodeKind.Choice or OdfSchemaPatternNodeKind.Group or
            OdfSchemaPatternNodeKind.Interleave or OdfSchemaPatternNodeKind.Other)
            return children[0];
        return Intern(Make(kind, children));
    }

    private OdfSchemaPatternNode Intern(OdfSchemaPatternNode node)
    {
        if (_constructed is null)
            return node;
        if (_constructed.TryGetValue(node, out OdfSchemaPatternNode? existing))
            return existing;
        _constructed.Add(node, node);
        return node;
    }

    private static OdfSchemaPatternNode Make(OdfSchemaPatternNodeKind kind, IEnumerable<OdfSchemaPatternNode> children) =>
        new(kind, "exactlyOne", "", "", "", "", "", null, children);
}
