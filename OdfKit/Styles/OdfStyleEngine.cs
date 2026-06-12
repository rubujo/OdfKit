using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// ODF 樣式引擎，負責樣式繼承、解析與去重管理。
/// </summary>
public class OdfStyleEngine
{
    private readonly OdfNode _contentRoot;
    private readonly OdfNode _stylesRoot;

    /// <summary>
    /// 樣式變更時發生的事件。
    /// </summary>
    public Action<OdfNode, string>? OnStyleChanging;

    // 已載入樣式的登錄表
    private readonly Dictionary<string, OdfNode> _automaticStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OdfNode> _commonStyles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OdfNode> _defaultStyles = new(StringComparer.Ordinal); // 金鑰：家族 (Family)

    // 由記憶體中包裝器建立/修改的臨時局部樣式
    private readonly Dictionary<OdfNode, OdfNode> _localStyles = [];

    /// <summary>
    /// 初始化 <see cref="OdfStyleEngine"/> 類別的新執行個體。
    /// </summary>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="contentRoot"/> 或 <paramref name="stylesRoot"/> 為 null 時拋出</exception>
    public OdfStyleEngine(OdfNode contentRoot, OdfNode stylesRoot)
    {
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        _stylesRoot = stylesRoot ?? throw new ArgumentNullException(nameof(stylesRoot));
        RebuildStyleIndex();
    }

    /// <summary>
    /// 重新建構樣式索引。
    /// </summary>
    public void RebuildStyleIndex()
    {
        _automaticStyles.Clear();
        _commonStyles.Clear();
        _defaultStyles.Clear();

        // 1. 掃描 content.xml 的 automatic-styles
        var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
        if (contentAuto is not null)
        {
            foreach (var child in contentAuto.Children)
            {
                string? name = child.GetAttribute("name", OdfNamespaces.Style);
                if (name is not null) _automaticStyles[name] = child;
            }
        }

        // 2. 掃描 styles.xml 的 automatic-styles
        var stylesAuto = FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office);
        if (stylesAuto is not null)
        {
            foreach (var child in stylesAuto.Children)
            {
                string? name = child.GetAttribute("name", OdfNamespaces.Style);
                if (name is not null) _automaticStyles[name] = child;
            }
        }

        // 3. 掃描 styles.xml 的 styles (常用樣式與預設樣式)
        var commonStyles = FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office);
        if (commonStyles is not null)
        {
            foreach (var child in commonStyles.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.GetAttribute("name", OdfNamespaces.Style) is not null)
                {
                    string name = child.GetAttribute("name", OdfNamespaces.Style)!;
                    _commonStyles[name] = child;
                }
                else if (child.LocalName == "default-style" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    string? family = child.GetAttribute("family", OdfNamespaces.Style);
                    if (family is not null) _defaultStyles[family] = child;
                }
            }
        }
    }

    /// <summary>
    /// 判斷指定的樣式是否存在。
    /// </summary>
    /// <param name="styleName">樣式名稱</param>
    /// <returns>若存在則為 true，否則為 false</returns>
    public bool StyleExists(string styleName)
    {
        return _automaticStyles.ContainsKey(styleName) || _commonStyles.ContainsKey(styleName);
    }

    /// <summary>
    /// 取得指定的樣式屬性，自動支援層級回溯與循環繼承保護。
    /// </summary>
    /// <param name="styleName">樣式名稱</param>
    /// <param name="propertyLocalName">屬性的區域名稱</param>
    /// <param name="propertyNsUri">屬性的命名空間 URI</param>
    /// <param name="styleFamily">樣式家族</param>
    /// <returns>屬性值，若找不到則為 null</returns>
    public string? GetStyleProperty(string styleName, string propertyLocalName, string propertyNsUri, string styleFamily)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        return GetStylePropertyInternal(styleName, propertyLocalName, propertyNsUri, styleFamily, visited);
    }

    private string? GetStylePropertyInternal(string? styleName, string propertyLocalName, string propertyNsUri, string styleFamily, HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return GetDefaultStyleProperty(styleFamily, propertyLocalName, propertyNsUri);
        }

        // 循環繼承檢查
        if (visited.Contains(styleName!))
        {
            OdfKitDiagnostics.Warn($"偵測到樣式 '{styleName}' 的循環繼承。後退至預設樣式。");
            return GetDefaultStyleProperty(styleFamily, propertyLocalName, propertyNsUri);
        }
        visited.Add(styleName!);

        // 1. 檢查自動樣式 (Automatic Style)
        if (_automaticStyles.TryGetValue(styleName!, out var styleNode))
        {
            string? val = FindPropertyInStyleNode(styleNode, propertyLocalName, propertyNsUri);
            if (val is not null) return val;

            string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(parentName))
            {
                val = GetStylePropertyInternal(parentName, propertyLocalName, propertyNsUri, styleFamily, visited);
                if (val is not null) return val;
            }
        }

        // 2. 檢查常用樣式 (Common Style)
        if (_commonStyles.TryGetValue(styleName!, out styleNode))
        {
            string? val = FindPropertyInStyleNode(styleNode, propertyLocalName, propertyNsUri);
            if (val is not null) return val;

            string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(parentName))
            {
                val = GetStylePropertyInternal(parentName, propertyLocalName, propertyNsUri, styleFamily, visited);
                if (val is not null) return val;
            }
        }

        // 3. 後退至預設樣式
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
                if (val is not null) return val;
            }
        }
        return null;
    }

    #region 局部樣式與去重

    /// <summary>
    /// 取得或建立特定 DOM 節點在記憶體中的局部編輯樣式節點。
    /// </summary>
    /// <param name="elementNode">DOM 項目節點</param>
    /// <param name="family">樣式家族</param>
    /// <returns>建立或取得的局部樣式節點</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="elementNode"/> 為 null 時拋出</exception>
    public OdfNode GetOrCreateLocalStyle(OdfNode elementNode, string family)
    {
        if (elementNode is null) throw new ArgumentNullException(nameof(elementNode));

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

    /// <summary>
    /// 設定局部樣式屬性。
    /// </summary>
    /// <param name="elementNode">DOM 項目節點</param>
    /// <param name="family">樣式家族</param>
    /// <param name="propElementLocalName">屬性元素的區域名稱</param>
    /// <param name="propAttrLocalName">屬性屬性的區域名稱</param>
    /// <param name="propAttrNsUri">屬性屬性的命名空間 URI</param>
    /// <param name="value">要設定的值</param>
    /// <param name="propAttrPrefix">屬性屬性的前綴</param>
    public void SetLocalStyleProperty(OdfNode elementNode, string family, string propElementLocalName, string propAttrLocalName, string propAttrNsUri, string value, string? propAttrPrefix = null)
    {
        OnStyleChanging?.Invoke(elementNode, family);
        var styleNode = GetOrCreateLocalStyle(elementNode, family);

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

        if (propsNode is null)
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
            if (!elementNode.IsModified) continue;

            string family = localStyle.GetAttribute("family", OdfNamespaces.Style) ?? "text";

            // 將屬性序列化為字串以作為雜湊金鑰
            string styleKey = SerializeStyleProperties(localStyle);
            if (string.IsNullOrEmpty(styleKey)) continue;

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

            // 更新項目的樣式屬性
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
        // 用於進行雜湊之樣式屬性的簡單確定性序列化
        StringBuilder sb = new();
        sb.Append($"family:{styleNode.GetAttribute("family", OdfNamespaces.Style)}|");
        sb.Append($"parent:{styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style)}|");

        List<OdfNode> propNodes = [..styleNode.Children];
        propNodes.Sort((x, y) => string.Compare(x.LocalName, y.LocalName, StringComparison.Ordinal));

        foreach (var pNode in propNodes)
        {
            sb.Append($"[{pNode.LocalName}:");
            List<OdfAttributeName> attrs = [..pNode.Attributes.Keys];
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
