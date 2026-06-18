using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// ODF 樣式引擎，負責樣式繼承、解析與去重管理。
/// </summary>
public partial class OdfStyleEngine
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

    // 樣式節點直接屬性展平快取（PERF-4d）
    private readonly Dictionary<OdfNode, Dictionary<OdfAttributeName, string?>> _directStyleProperties = new();

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
        _directStyleProperties.Clear();

        // 1. 掃描 content.xml 的 automatic-styles
        var contentAuto = FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office);
        if (contentAuto is not null)
        {
            foreach (var child in contentAuto.Children)
            {
                string? name = child.GetAttribute("name", OdfNamespaces.Style);
                if (name is not null)
                {
                    _automaticStyles[name] = child;
                    CacheDirectStyleProperties(child);
                }
            }
        }

        // 2. 掃描 styles.xml 的 automatic-styles
        var stylesAuto = FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office);
        if (stylesAuto is not null)
        {
            foreach (var child in stylesAuto.Children)
            {
                string? name = child.GetAttribute("name", OdfNamespaces.Style);
                if (name is not null)
                {
                    _automaticStyles[name] = child;
                    CacheDirectStyleProperties(child);
                }
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
                    CacheDirectStyleProperties(child);
                }
                else if (child.LocalName == "default-style" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    string? family = child.GetAttribute("family", OdfNamespaces.Style);
                    if (family is not null)
                    {
                        _defaultStyles[family] = child;
                        CacheDirectStyleProperties(child);
                    }
                }
            }
        }
    }

    private void CacheDirectStyleProperties(OdfNode styleNode)
    {
        var map = new Dictionary<OdfAttributeName, string?>(OdfAttributeNameComparer.Instance);
        foreach (var child in styleNode.Children)
        {
            if (child.NodeType != OdfNodeType.Element ||
                !child.LocalName.EndsWith("-properties", StringComparison.Ordinal) ||
                child.NamespaceUri != OdfNamespaces.Style)
            {
                continue;
            }

            foreach (var attr in child.Attributes)
            {
                map[attr.Key] = attr.Value;
            }
        }

        _directStyleProperties[styleNode] = map;
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
            if (val is not null)
                return val;

            string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(parentName))
            {
                val = GetStylePropertyInternal(parentName, propertyLocalName, propertyNsUri, styleFamily, visited);
                if (val is not null)
                    return val;
            }
        }

        // 2. 檢查常用樣式 (Common Style)
        if (_commonStyles.TryGetValue(styleName!, out styleNode))
        {
            string? val = FindPropertyInStyleNode(styleNode, propertyLocalName, propertyNsUri);
            if (val is not null)
                return val;

            string? parentName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(parentName))
            {
                val = GetStylePropertyInternal(parentName, propertyLocalName, propertyNsUri, styleFamily, visited);
                if (val is not null)
                    return val;
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
        if (_directStyleProperties.TryGetValue(styleNode, out Dictionary<OdfAttributeName, string?>? map))
        {
            var key = new OdfAttributeName(propertyLocalName, propertyNsUri);
            if (map.TryGetValue(key, out string? cached))
            {
                return cached;
            }
        }

        foreach (var child in styleNode.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName.EndsWith("-properties", StringComparison.Ordinal) &&
                child.NamespaceUri == OdfNamespaces.Style)
            {
                string? val = child.GetAttribute(propertyLocalName, propertyNsUri);
                if (val is not null)
                    return val;
            }
        }

        return null;
    }
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
