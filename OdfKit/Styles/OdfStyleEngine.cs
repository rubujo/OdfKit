using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles
{
    public class OdfStyleEngine
    {
        private readonly OdfNode _contentRoot;
        private readonly OdfNode _stylesRoot;
        public Action<OdfNode, string>? OnStyleChanging;

        // Registry of loaded styles
        private readonly Dictionary<string, OdfNode> _automaticStyles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, OdfNode> _commonStyles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, OdfNode> _defaultStyles = new(StringComparer.Ordinal); // Key: family

        // Temporary local styles created/modified by wrappers in memory
        private readonly Dictionary<OdfNode, OdfNode> _localStyles = new();

        public OdfStyleEngine(OdfNode contentRoot, OdfNode stylesRoot)
        {
            _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
            _stylesRoot = stylesRoot ?? throw new ArgumentNullException(nameof(stylesRoot));
            RebuildStyleIndex();
        }

        public void RebuildStyleIndex()
        {
            _automaticStyles.Clear();
            _commonStyles.Clear();
            _defaultStyles.Clear();

            // 1. Scan content.xml automatic-styles
            var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
            if (contentAuto != null)
            {
                foreach (var child in contentAuto.Children)
                {
                    string? name = child.GetAttribute("name", OdfNamespaces.Style);
                    if (name != null) _automaticStyles[name] = child;
                }
            }

            // 2. Scan styles.xml automatic-styles
            var stylesAuto = FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office);
            if (stylesAuto != null)
            {
                foreach (var child in stylesAuto.Children)
                {
                    string? name = child.GetAttribute("name", OdfNamespaces.Style);
                    if (name != null) _automaticStyles[name] = child;
                }
            }

            // 3. Scan styles.xml styles (common styles & default styles)
            var commonStyles = FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office);
            if (commonStyles != null)
            {
                foreach (var child in commonStyles.Children)
                {
                    if (child.NodeType == OdfNodeType.Element && child.GetAttribute("name", OdfNamespaces.Style) != null)
                    {
                        string name = child.GetAttribute("name", OdfNamespaces.Style)!;
                        _commonStyles[name] = child;
                    }
                    else if (child.LocalName == "default-style" && child.NamespaceUri == OdfNamespaces.Style)
                    {
                        string? family = child.GetAttribute("family", OdfNamespaces.Style);
                        if (family != null) _defaultStyles[family] = child;
                    }
                }
            }
        }

        public bool StyleExists(string styleName)
        {
            return _automaticStyles.ContainsKey(styleName) || _commonStyles.ContainsKey(styleName);
        }

        /// <summary>
        /// 取得指定的樣式屬性，自動支援層級回溯與循環繼承保護。
        /// </summary>
        public string? GetStyleProperty(string styleName, string propertyLocalName, string propertyNsUri, string styleFamily)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            return GetStylePropertyInternal(styleName, propertyLocalName, propertyNsUri, styleFamily, visited);
        }

        private string? GetStylePropertyInternal(string? styleName, string propertyLocalName, string propertyNsUri, string styleFamily, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(styleName))
            {
                return GetDefaultStyleProperty(styleFamily, propertyLocalName, propertyNsUri);
            }

            // Cyclic inheritance check
            if (visited.Contains(styleName!))
            {
                OdfKitDiagnostics.Warn($"Cyclic style inheritance detected for style '{styleName}'. Fallback to default style.");
                return GetDefaultStyleProperty(styleFamily, propertyLocalName, propertyNsUri);
            }
            visited.Add(styleName!);

            // 1. Check Automatic Style
            if (_automaticStyles.TryGetValue(styleName!, out var styleNode))
            {
                string? val = FindPropertyInStyleNode(styleNode, propertyLocalName, propertyNsUri);
                if (val != null) return val;

                string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
                if (!string.IsNullOrEmpty(parentName))
                {
                    val = GetStylePropertyInternal(parentName, propertyLocalName, propertyNsUri, styleFamily, visited);
                    if (val != null) return val;
                }
            }

            // 2. Check Common Style
            if (_commonStyles.TryGetValue(styleName!, out styleNode))
            {
                string? val = FindPropertyInStyleNode(styleNode, propertyLocalName, propertyNsUri);
                if (val != null) return val;

                string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
                if (!string.IsNullOrEmpty(parentName))
                {
                    val = GetStylePropertyInternal(parentName, propertyLocalName, propertyNsUri, styleFamily, visited);
                    if (val != null) return val;
                }
            }

            // 3. Fallback to default style
            return GetDefaultStyleProperty(styleFamily, propertyLocalName, propertyNsUri);
        }

        private string? GetDefaultStyleProperty(string family, string propertyLocalName, string propertyNsUri)
        {
            if (_defaultStyles.TryGetValue(family, out var defaultStyleNode))
            {
                return FindPropertyInStyleNode(defaultStyleNode, propertyLocalName, propertyNsUri);
            }
            return null;
        }

        private string? FindPropertyInStyleNode(OdfNode styleNode, string propertyLocalName, string propertyNsUri)
        {
            foreach (var child in styleNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && 
                    child.LocalName.EndsWith("-properties", StringComparison.Ordinal) && 
                    child.NamespaceUri == OdfNamespaces.Style)
                {
                    string? val = child.GetAttribute(propertyLocalName, propertyNsUri);
                    if (val != null) return val;
                }
            }
            return null;
        }

        #region Local Styles & Deduplication

        /// <summary>
        /// 取得或建立特定 DOM 節點在記憶體中的局部編輯樣式節點。
        /// </summary>
        public OdfNode GetOrCreateLocalStyle(OdfNode elementNode, string family)
        {
            if (elementNode == null) throw new ArgumentNullException(nameof(elementNode));

            if (_localStyles.TryGetValue(elementNode, out var styleNode))
            {
                return styleNode;
            }

            // Create a new style node in memory
            styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
            styleNode.SetAttribute("family", OdfNamespaces.Style, family);

            // Inherit from existing style if it has one
            string styleAttr = "style-name";
            string styleNs = family switch
            {
                "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
                "graphic" => OdfNamespaces.Draw,
                _ => OdfNamespaces.Text
            };
            
            string? existingStyleName = elementNode.GetAttribute(styleAttr, styleNs);
            if (!string.IsNullOrEmpty(existingStyleName))
            {
                styleNode.SetAttribute("parent-style-name", OdfNamespaces.Style, existingStyleName!);
            }

            _localStyles[elementNode] = styleNode;
            elementNode.IsModified = true;
            return styleNode;
        }

        public void SetLocalStyleProperty(OdfNode elementNode, string family, string propElementLocalName, string propAttrLocalName, string propAttrNsUri, string value, string? propAttrPrefix = null)
        {
            OnStyleChanging?.Invoke(elementNode, family);
            var styleNode = GetOrCreateLocalStyle(elementNode, family);
            
            // Find or create the properties element (e.g. style:text-properties)
            OdfNode? propsNode = null;
            foreach (var child in styleNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.LocalName == propElementLocalName && child.NamespaceUri == OdfNamespaces.Style)
                {
                    propsNode = child;
                    break;
                }
            }

            if (propsNode == null)
            {
                propsNode = new OdfNode(OdfNodeType.Element, propElementLocalName, OdfNamespaces.Style, "style");
                styleNode.AppendChild(propsNode);
            }

            propsNode.SetAttribute(propAttrLocalName, propAttrNsUri, value, propAttrPrefix);
            elementNode.IsModified = true;

            DeduplicateAndSaveStyles();
        }

        /// <summary>
        /// 執行自動樣式去重，將記憶體中的臨時局部樣式合併寫入 XML 並更新 DOM 節點參照。
        /// </summary>
        public void DeduplicateAndSaveStyles()
        {
            if (_localStyles.Count == 0) return;

            // Find or create <office:automatic-styles> in content.xml
            var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
            if (contentAuto == null)
            {
                contentAuto = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
                // Insert automatic-styles at the beginning of document-content
                if (_contentRoot.Children.Count > 0)
                {
                    _contentRoot.InsertBefore(contentAuto, _contentRoot.Children[0]);
                }
                else
                {
                    _contentRoot.AppendChild(contentAuto);
                }
            }

            // Dictionary mapping serialized style properties to their deduplicated style name
            var uniqueStyles = new Dictionary<string, string>(StringComparer.Ordinal);
            
            // Counter for generating automatic style names (e.g. P1, P2, T1, T2)
            var familyCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _localStyles)
            {
                OdfNode elementNode = kvp.Key;
                OdfNode localStyle = kvp.Value;

                // Only write if the node is modified (Dirty flag check)
                if (!elementNode.IsModified) continue;

                string family = localStyle.GetAttribute("family", OdfNamespaces.Style) ?? "text";
                
                // Serialize properties to a string to use as a hash key
                string styleKey = SerializeStyleProperties(localStyle);
                if (string.IsNullOrEmpty(styleKey)) continue;

                if (!uniqueStyles.TryGetValue(styleKey, out string? styleName))
                {
                    // Generate new name that doesn't collide with existing styles
                    string prefix = GetFamilyPrefix(family);
                    int count = familyCounters.TryGetValue(family, out int c) ? c + 1 : 1;
                    string generatedName;
                    do
                    {
                        generatedName = $"{prefix}{count}";
                        count++;
                    } while (_automaticStyles.ContainsKey(generatedName) || _commonStyles.ContainsKey(generatedName));

                    familyCounters[family] = count - 1;
                    styleName = generatedName;

                    // Add to automatic-styles XML DOM
                    var savedStyleNode = localStyle.CloneNode(true);
                    savedStyleNode.SetAttribute("name", OdfNamespaces.Style, styleName);
                    contentAuto.AppendChild(savedStyleNode);

                    uniqueStyles[styleKey] = styleName;
                    _automaticStyles[styleName] = savedStyleNode;
                }

                // Update element's style attribute
                string styleAttr = "style-name";
                string styleNs = family switch
                {
                    "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
                    "graphic" => OdfNamespaces.Draw,
                    _ => OdfNamespaces.Text
                };
                string stylePrefix = OdfNamespaces.GetPrefix(styleNs);

                elementNode.SetAttribute(styleAttr, styleNs, styleName, stylePrefix);
            }

            _localStyles.Clear();
            RebuildStyleIndex();
        }

        private string SerializeStyleProperties(OdfNode styleNode)
        {
            // Simple deterministic serialization of style properties for hashing
            var sb = new StringBuilder();
            sb.Append($"family:{styleNode.GetAttribute("family", OdfNamespaces.Style)}|");
            sb.Append($"parent:{styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style)}|");

            var propNodes = new List<OdfNode>(styleNode.Children);
            propNodes.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));

            foreach (var pNode in propNodes)
            {
                sb.Append($"[{pNode.LocalName}:");
                var attrs = new List<OdfAttributeName>(pNode.Attributes.Keys);
                attrs.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));
                
                foreach (var attr in attrs)
                {
                    sb.Append($"{attr.NamespaceUri}:{attr.LocalName}={pNode.Attributes[attr]};");
                }
                sb.Append("]");
            }

            return sb.ToString();
        }

        private string GetFamilyPrefix(string family)
        {
            return family.ToLowerInvariant() switch
            {
                "paragraph" => "P",
                "text" => "T",
                "table-cell" => "ce",
                "table-row" => "ro",
                "table-column" => "co",
                "graphic" => "gr",
                "section" => "S",
                _ => "a"
            };
        }

        #endregion

        private OdfNode? FindChildElement(OdfNode parent, string localName, string nsUri)
        {
            foreach (var child in parent.Children)
            {
                if (child.NodeType == OdfNodeType.Element && 
                    string.Equals(child.LocalName, localName, StringComparison.Ordinal) && 
                    string.Equals(child.NamespaceUri, nsUri, StringComparison.Ordinal))
                {
                    return child;
                }
            }
            return null;
        }
    }
}
