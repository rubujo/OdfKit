using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

public partial class OdfStyleEngine
{
    #region Local Styles & Deduplication

    /// <summary>
    /// 取得或建立特定 DOM 節點在記憶體中的局部編輯樣式節點。
    /// </summary>
    /// <param name="elementNode">DOM 專案節點</param>
    /// <param name="family">樣式家族</param>
    /// <returns>建立或取得的局部樣式節點</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="elementNode"/> 為 null 時拋出</exception>
    public OdfNode GetOrCreateLocalStyle(OdfNode elementNode, string family)
    {
        if (elementNode is null)
            throw new ArgumentNullException(nameof(elementNode));

        if (_localStyles.TryGetValue(elementNode, out var styleNode))
        {
            return styleNode;
        }

        // 在記憶體中建立新的樣式節點
        styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        styleNode.SetAttribute("family", OdfNamespaces.Style, family);

        // 如果已存在樣式，則繼承之
        string styleAttr = "style-name";
        string styleNs = family switch
        {
            "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
            "graphic" or "drawing-page" => OdfNamespaces.Draw,
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

    /// <summary>
    /// 設定局部樣式屬性。
    /// </summary>
    /// <param name="elementNode">DOM 專案節點</param>
    /// <param name="family">樣式家族</param>
    /// <param name="propElementLocalName">屬性元素的區域名稱</param>
    /// <param name="propAttrLocalName">屬性屬性的區域名稱</param>
    /// <param name="propAttrNsUri">屬性屬性的命名空間 URI</param>
    /// <param name="value">要設定的值</param>
    /// <param name="propAttrPrefix">屬性屬性的前綴</param>
    /// <param name="deferSave">是否延遲執行自動樣式去重與存檔</param>
    public void SetLocalStyleProperty(OdfNode elementNode, string family, string propElementLocalName, string propAttrLocalName, string propAttrNsUri, string? value, string? propAttrPrefix = null, bool deferSave = false)
    {
        OnStyleChanging?.Invoke(elementNode, family);
        var styleNode = GetOrCreateLocalStyle(elementNode, family);
        if (styleNode is null)
            return;

        // 尋找或建立屬性元素（例如 style:text-properties）
        OdfNode? propsNode = null;
        foreach (var child in styleNode.Children)
        {
            if (child.NodeType == OdfNodeType.Element && child.LocalName == propElementLocalName && child.NamespaceUri == OdfNamespaces.Style)
            {
                propsNode = child;
                break;
            }
        }

        if (value == null)
        {
            // 扁平化繼承鏈：如果存在 parent-style-name，將其屬性複製過來並打破繼承，以落實完全清除屬性
            string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(parentName))
            {
                OdfNode? parentNode = null;
                if (_automaticStyles.TryGetValue(parentName!, out var autoStyle))
                    parentNode = autoStyle;
                else if (_commonStyles.TryGetValue(parentName!, out var commonStyle))
                    parentNode = commonStyle;

                if (parentNode is not null)
                {
                    // 1. 複製父樣式的所有屬性節點，但排除或移除要清除的屬性
                    foreach (var child in parentNode.Children)
                    {
                        if (child.NodeType == OdfNodeType.Element)
                        {
                            var clonedChild = child.CloneNode(true);
                            if (clonedChild.LocalName == propElementLocalName && clonedChild.NamespaceUri == OdfNamespaces.Style)
                            {
                                clonedChild.RemoveAttribute(propAttrLocalName, propAttrNsUri);
                            }

                            // 只有在子屬性節點還有屬性或子節點時才保留
                            if (clonedChild.Attributes.Count > 0 || clonedChild.Children.Count > 0)
                            {
                                // 檢查 styleNode 是否已有同名子節點
                                OdfNode? existingChild = null;
                                foreach (var sc in styleNode.Children)
                                {
                                    if (sc.LocalName == clonedChild.LocalName && sc.NamespaceUri == clonedChild.NamespaceUri)
                                    {
                                        existingChild = sc;
                                        break;
                                    }
                                }

                                if (existingChild is not null)
                                {
                                    // 合併屬性且以局部編輯屬性優先
                                    foreach (var attr in clonedChild.Attributes)
                                    {
                                        if (!existingChild.Attributes.ContainsKey(attr.Key))
                                        {
                                            existingChild.SetAttribute(attr.Key.LocalName, attr.Key.NamespaceUri, attr.Value);
                                        }
                                    }
                                }
                                else
                                {
                                    styleNode.AppendChild(clonedChild);
                                }
                            }
                        }
                    }

                    // 2. 繼承父樣式的 parent-style-name
                    string? grandParentName = parentNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
                    if (!string.IsNullOrEmpty(grandParentName))
                    {
                        styleNode.SetAttribute("parent-style-name", OdfNamespaces.Style, grandParentName!);
                    }
                    else
                    {
                        styleNode.RemoveAttribute("parent-style-name", OdfNamespaces.Style);
                    }
                }
            }

            // 3. 確保在當前的 styleNode 中移除該屬性（無論是複製過來還是原本就存在）
            propsNode = null;
            foreach (var child in styleNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.LocalName == propElementLocalName && child.NamespaceUri == OdfNamespaces.Style)
                {
                    propsNode = child;
                    break;
                }
            }

            if (propsNode is not null)
            {
                propsNode.RemoveAttribute(propAttrLocalName, propAttrNsUri);
                if (propsNode.Attributes.Count == 0 && propsNode.Children.Count == 0)
                {
                    styleNode.RemoveChild(propsNode);
                }
            }
        }
        else
        {
            if (propsNode is null)
            {
                propsNode = new OdfNode(OdfNodeType.Element, propElementLocalName, OdfNamespaces.Style, "style");
                styleNode.AppendChild(propsNode);
            }

            propsNode.SetAttribute(propAttrLocalName, propAttrNsUri, value, propAttrPrefix);
        }

        elementNode.IsModified = true;
        if (!deferSave)
        {
            DeduplicateAndSaveStyles();
        }
    }

    /// <summary>
    /// 執行自動樣式去重，將記憶體中的臨時局部樣式合併寫入 XML 並更新 DOM 節點參照。
    /// </summary>
    public void DeduplicateAndSaveStyles()
    {
        if (_localStyles.Count == 0)
            return;

        // 尋找或在 content.xml 中建立 <office:automatic-styles>
        var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
        if (contentAuto is null)
        {
            contentAuto = new OdfNode(OdfNodeType.Element, "automatic-styles", OdfNamespaces.Office, "office");
            // 在 document-content 的開頭插入 automatic-styles
            if (_contentRoot.Children.Count > 0)
            {
                _contentRoot.InsertBefore(contentAuto, _contentRoot.Children[0]);
            }
            else
            {
                _contentRoot.AppendChild(contentAuto);
            }
        }

        // 對應序列化樣式屬性至其去重樣式名稱的字典
        Dictionary<string, string> uniqueStyles = new(StringComparer.Ordinal);

        // 用於產生自動樣式名稱的計數器（例如 P1, P2, T1, T2）
        Dictionary<string, int> familyCounters = new(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in _localStyles)
        {
            OdfNode elementNode = kvp.Key;
            OdfNode localStyle = kvp.Value;

            // 僅在節點被修改時寫入（Dirty 旗標檢查）
            if (!elementNode.IsModified)
                continue;

            string family = localStyle.GetAttribute("family", OdfNamespaces.Style) ?? "text";

            // 將屬性序列化為字串以作為雜湊金鑰
            string styleKey = SerializeStyleProperties(localStyle);
            if (string.IsNullOrEmpty(styleKey))
                continue;

            if (!uniqueStyles.TryGetValue(styleKey, out string? styleName))
            {
                // 產生不與現有樣式衝突的新名稱
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

                // 新增至 automatic-styles XML DOM
                var savedStyleNode = localStyle.CloneNode(true);
                savedStyleNode.SetAttribute("name", OdfNamespaces.Style, styleName);
                contentAuto.AppendChild(savedStyleNode);

                uniqueStyles[styleKey] = styleName;
                _automaticStyles[styleName] = savedStyleNode;
            }

            // 更新專案的樣式屬性
            string styleAttr = "style-name";
            string styleNs = family switch
            {
                "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
                "graphic" or "drawing-page" => OdfNamespaces.Draw,
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
        // 用於進行雜湊之樣式屬性的簡單確定性序列化
        StringBuilder sb = new();
        sb.Append($"family:{styleNode.GetAttribute("family", OdfNamespaces.Style)}|");
        sb.Append($"parent:{styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style)}|");

        List<OdfNode> propNodes = [.. styleNode.Children];
        propNodes.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));

        foreach (var pNode in propNodes)
        {
            sb.Append($"[{pNode.LocalName}:");
            List<OdfAttributeName> attrs = [.. pNode.Attributes.Keys];
            attrs.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));

            foreach (var attr in attrs)
            {
                sb.Append($"{attr.NamespaceUri}:{attr.LocalName}={pNode.Attributes[attr]};");
            }
            sb.Append(']');
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
}
