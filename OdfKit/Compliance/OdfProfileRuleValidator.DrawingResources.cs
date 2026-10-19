using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Core;

namespace OdfKit.Compliance;

internal static partial class OdfProfileRuleValidator
{
    private sealed class DrawingResourceReferences
    {
        private sealed class ResourceScope
        {
            internal readonly HashSet<string> Markers = new(StringComparer.Ordinal);
            internal readonly HashSet<string> Dashes = new(StringComparer.Ordinal);
        }

        private readonly Dictionary<string, ResourceScope> _packageScopes = new(StringComparer.Ordinal);
        private readonly List<(HashSet<string> Names, string Attribute, string Target, string? Path, string XPath)> _references = [];

        internal void Collect(XElement root, string? packagePath, OdfVersion version)
        {
            XNamespace office = OdfNamespaces.Office;
            XNamespace draw = OdfNamespaces.Draw;
            XNamespace style = OdfNamespaces.Style;
            string path = (packagePath ?? string.Empty).Replace('\\', '/');
            int separator = path.LastIndexOf('/');
            string packageScope = separator < 0 ? string.Empty : path.Substring(0, separator + 1);
            if (!_packageScopes.TryGetValue(packageScope, out ResourceScope? packageResources))
            {
                packageResources = new ResourceScope();
                _packageScopes.Add(packageScope, packageResources);
            }
            // 內嵌單一 XML 的鍵只存活於本次掃描；待檢查參照不保留整棵 XML。
            Dictionary<XElement, ResourceScope> inlineScopes = [];
            foreach (XElement element in root.DescendantsAndSelf())
            {
                bool marker = element.Name == draw + "marker";
                bool dash = element.Name == draw + "stroke-dash";
                if (!marker && !dash && element.Name != style + "graphic-properties")
                    continue;
                XElement? owner = element.AncestorsAndSelf().FirstOrDefault(node => node.Name == office + "document");
                ResourceScope scope = packageResources;
                OdfVersion effectiveVersion = version;
                if (owner is not null && owner != root)
                {
                    if (!inlineScopes.TryGetValue(owner, out ResourceScope? inline))
                    {
                        inline = new ResourceScope();
                        inlineScopes.Add(owner, inline);
                    }
                    scope = inline;
                    if (OdfVersionInfo.TryParseVersionString((string?)owner.Attribute(office + "version"), out OdfVersion declared))
                        effectiveVersion = declared;
                }
                if (marker || dash)
                {
                    string? name = ((string?)element.Attribute(draw + "name"))?.Trim(' ', '\t', '\r', '\n');
                    if (name is not null && name.Length > 0)
                        (marker ? scope.Markers : scope.Dashes).Add(name);
                    continue;
                }
                if (effectiveVersion is not (OdfVersion.Odf12 or OdfVersion.Odf13 or OdfVersion.Odf14))
                    continue;
                foreach (XAttribute attribute in element.Attributes())
                {
                    if (attribute.Name.Namespace != draw || attribute.Name.LocalName is not ("marker-start" or "marker-end" or "stroke-dash"))
                        continue;
                    string target = attribute.Value.Trim(' ', '\t', '\r', '\n');
                    if (target.Length == 0)
                        continue;
                    HashSet<string> names = attribute.Name.LocalName == "stroke-dash" ? scope.Dashes : scope.Markers;
                    _references.Add((names, "draw:" + attribute.Name.LocalName, target, packagePath,
                        "/" + root.Name.LocalName + "//style:graphic-properties/@draw:" + attribute.Name.LocalName));
                }
            }
        }

        internal void Validate(OdfComplianceProfile profile, OdfPolicyRule rule, List<OdfValidationIssue> issues)
        {
            foreach (var reference in _references)
                if (!reference.Names.Contains(reference.Target))
                    issues.Add(new OdfValidationIssue(
                        rule.DefaultSeverity,
                        "ODF3143",
                        $"The {reference.Attribute} attribute refers to a missing or incompatible drawing resource '{reference.Target}'.",
                        reference.Path,
                        reference.XPath,
                        profileId: profile.Id));
        }
    }
}
