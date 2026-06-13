using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.Compliance;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF 節點類型的列舉。
/// </summary>
public enum OdfNodeType
{
    /// <summary>
    /// 元素節點。
    /// </summary>
    Element,

    /// <summary>
    /// 文字節點。
    /// </summary>
    Text,

    /// <summary>
    /// 註解節點。
    /// </summary>
    Comment,

    /// <summary>
    /// XML 處理指令節點。
    /// </summary>
    ProcessingInstruction
}

/// <summary>
/// 表示 ODF 屬性名稱的結構。
/// </summary>
/// <param name="localName">屬性的局部名稱</param>
/// <param name="namespaceUri">屬性的命名空間 URI</param>
public struct OdfAttributeName(string localName, string namespaceUri) : IEquatable<OdfAttributeName>
{
    /// <summary>
    /// 取得屬性的局部名稱。
    /// </summary>
    public string LocalName { get; } = localName;

    /// <summary>
    /// 取得屬性的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; } = namespaceUri;

    /// <summary>
    /// 指示目前物件是否等於另一個相同類型的物件。
    /// </summary>
    /// <param name="other">要與目前物件進行比較的物件</param>
    /// <returns>如果目前物件等於 <paramref name="other"/> 參數，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool Equals(OdfAttributeName other) =>
        string.Equals(LocalName, other.LocalName, StringComparison.Ordinal) &&
        string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal);

    /// <summary>
    /// 指示此執行個體與指定的物件是否相等。
    /// </summary>
    /// <param name="obj">要比較的物件</param>
    /// <returns>如果 <paramref name="obj"/> 與這個執行個體具有相同的類型並表示相同的值，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public override bool Equals(object? obj) => obj is OdfAttributeName name && Equals(name);

    /// <summary>
    /// 傳回此執行個體的雜湊碼。
    /// </summary>
    /// <returns>32 位元有號整數雜湊碼</returns>
    public override int GetHashCode() =>
        (LocalName?.GetHashCode() ?? 0) ^ (NamespaceUri?.GetHashCode() ?? 0);
}

/// <summary>
/// 用於比較 ODF 屬性名稱的比較器。
/// </summary>
internal class OdfAttributeNameComparer : IEqualityComparer<OdfAttributeName>
{
    /// <summary>
    /// 取得比較器的單例執行個體。
    /// </summary>
    public static readonly OdfAttributeNameComparer Instance = new();

    /// <summary>
    /// 判斷兩個屬性名稱是否相等。
    /// </summary>
    public bool Equals(OdfAttributeName x, OdfAttributeName y) => x.Equals(y);

    /// <summary>
    /// 取得屬性名稱的雜湊碼。
    /// </summary>
    public int GetHashCode(OdfAttributeName obj) => obj.GetHashCode();
}

/// <summary>
/// 表示 ODF 文件物件模型 (DOM) 中的節點基底類別。
/// </summary>
public class OdfNode
{
    /// <summary>
    /// 取得節點的類型。
    /// </summary>
    public OdfNodeType NodeType { get; }

    /// <summary>
    /// 取得節點的局部名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// 取得節點的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// 取得或設定節點的命名空間前綴。
    /// </summary>
    public string? Prefix { get; set; }
    
    private string? _value; // 用於文字節點

    /// <summary>
    /// 取得此節點的父節點。
    /// </summary>
    public OdfNode? Parent { get; internal set; }

    /// <summary>
    /// 取得此節點的子節點清單。
    /// </summary>
    public List<OdfNode> Children { get; } = [];

    /// <summary>
    /// 取得此節點的屬性字典。
    /// </summary>
    public Dictionary<OdfAttributeName, string> Attributes { get; } = new(OdfAttributeNameComparer.Instance);

    private readonly Dictionary<OdfAttributeName, string> _attributePrefixes = new(OdfAttributeNameComparer.Instance);
    
    /// <summary>
    /// 取得或設定標記此節點是否被新增或修改 (Dirty Flag)，用於自動樣式去重。
    /// </summary>
    public bool IsModified { get; set; }

    /// <summary>
    /// 初始化 <see cref="OdfNode"/> 類別的新執行個體。
    /// </summary>
    /// <param name="nodeType">節點類型</param>
    /// <param name="localName">局部名稱</param>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="prefix">命名空間前綴</param>
    public OdfNode(OdfNodeType nodeType, string localName, string namespaceUri, string? prefix = null)
    {
        NodeType = nodeType;
        LocalName = localName;
        NamespaceUri = namespaceUri;
        Prefix = prefix;
    }

    /// <summary>
    /// 初始化 <see cref="OdfNode"/> 類別的新執行個體。
    /// </summary>
    /// <param name="nodeType">節點類型</param>
    /// <param name="localName">局部名稱</param>
    /// <param name="namespaceUri">命名空間</param>
    /// <param name="prefix">命名空間前綴</param>
    public OdfNode(OdfNodeType nodeType, string localName, XNamespace namespaceUri, string? prefix = null)
        : this(nodeType, localName, namespaceUri.NamespaceName, prefix)
    {
    }

    /// <summary>
    /// 遞迴重設此節點及其所有子節點的修改標記為 <see langword="false"/>。
    /// </summary>
    public void ResetModifiedState()
    {
        IsModified = false;
        foreach (var child in Children)
        {
            child.ResetModifiedState();
        }
    }

    /// <summary>
    /// 取得或設定節點內含的文字內容。
    /// </summary>
    /// <remarks>
    /// 對於 Text 節點，這代表其直接值；對於 Element 節點，讀取會串接所有子 Text 節點，寫入會清除子節點並取代為單一 Text 節點。
    /// </remarks>
    public virtual string TextContent
    {
        get
        {
            if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
            {
                return _value ?? string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var child in Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Text)
                {
                    if (child.LocalName == "line-break")
                    {
                        sb.Append('\n');
                        continue;
                    }
                    if (child.LocalName == "tab")
                    {
                        sb.Append('\t');
                        continue;
                    }
                    if (child.LocalName == "s")
                    {
                        int count = 1;
                        string? cAttr = child.GetAttribute("c", OdfNamespaces.Text);
                        if (cAttr is not null && int.TryParse(cAttr, out var parsedCount))
                        {
                            count = parsedCount;
                        }
                        sb.Append(' ', count);
                        continue;
                    }
                }
                sb.Append(child.TextContent);
            }
            return sb.ToString();
        }
        set
        {
            IsModified = true;
            if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
            {
                _value = value;
            }
            else
            {
                Children.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
                    {
                        _value = value,
                        IsModified = true
                    };
                    AppendChild(textNode);
                }
            }
        }
    }

    #region DOM Tree Manipulation

    /// <summary>
    /// 將指定的節點新增至此節點的子節點清單末尾。
    /// </summary>
    /// <param name="child">要新增的子節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="child"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點時擲出</exception>
    public void AppendChild(OdfNode child)
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
        }

        IsModified = true;
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        Children.Add(child);
    }

    /// <summary>
    /// 在現有的子節點之前插入新的子節點。
    /// </summary>
    /// <param name="newChild">要插入的新子節點</param>
    /// <param name="refChild">參考的子節點，新子節點將插入在此節點之前</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="newChild"/> 或 <paramref name="refChild"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點，或參考節點不是此節點的子節點時擲出</exception>
    public void InsertBefore(OdfNode newChild, OdfNode refChild)
    {
        if (newChild is null) throw new ArgumentNullException(nameof(newChild));
        if (refChild is null) throw new ArgumentNullException(nameof(refChild));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
        }

        int index = Children.IndexOf(refChild);
        if (index == -1)
        {
            throw new InvalidOperationException("Reference node is not a child of this node.");
        }

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        newChild.Parent = this;
        Children.Insert(index, newChild);
    }

    /// <summary>
    /// 在現有的子節點之後插入新的子節點。
    /// </summary>
    /// <param name="newChild">要插入的新子節點</param>
    /// <param name="refChild">參考的子節點，新子節點將插入在此節點之後</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="newChild"/> 或 <paramref name="refChild"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點，或參考節點不是此節點的子節點時擲出</exception>
    public void InsertAfter(OdfNode newChild, OdfNode refChild)
    {
        if (newChild is null) throw new ArgumentNullException(nameof(newChild));
        if (refChild is null) throw new ArgumentNullException(nameof(refChild));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
        }

        int index = Children.IndexOf(refChild);
        if (index == -1)
        {
            throw new InvalidOperationException("Reference node is not a child of this node.");
        }

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        newChild.Parent = this;
        Children.Insert(index + 1, newChild);
    }

    /// <summary>
    /// 從此節點的子節點清單中移除指定的子節點。
    /// </summary>
    /// <param name="child">要移除的子節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="child"/> 為 <see langword="null"/> 時擲出</exception>
    public void RemoveChild(OdfNode child)
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        if (Children.Remove(child))
        {
            IsModified = true;
            child.Parent = null;
        }
    }

    /// <summary>
    /// 取得此節點的所有後代節點。
    /// </summary>
    /// <returns>後代節點的列舉</returns>
    public IEnumerable<OdfNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    #endregion

    #region Attributes Helper

    /// <summary>
    /// 取得指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttribute(string localName, string namespaceUri)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        return Attributes.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>
    /// 取得指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間</param>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttribute(string localName, XNamespace namespaceUri) => GetAttribute(localName, namespaceUri.NamespaceName);

    /// <summary>
    /// 設定指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    /// <param name="value">要設定的屬性值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    public void SetAttribute(string localName, string namespaceUri, string value, string? prefix = null)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        string? existingPrefix = GetAttributePrefix(key);
        if (!Attributes.TryGetValue(key, out string? existing) || existing != value)
        {
            IsModified = true;
            Attributes[key] = value;
        }

        if (!string.IsNullOrEmpty(prefix) && prefix is string attributePrefix)
        {
            if (!string.Equals(existingPrefix, attributePrefix, StringComparison.Ordinal))
            {
                IsModified = true;
            }

            _attributePrefixes[key] = attributePrefix;
        }
        else
        {
            if (existingPrefix is not null)
            {
                IsModified = true;
            }

            _attributePrefixes.Remove(key);
        }
    }

    /// <summary>
    /// 設定指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間</param>
    /// <param name="value">要設定的屬性值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    public void SetAttribute(string localName, XNamespace namespaceUri, string value, string? prefix = null) => SetAttribute(localName, namespaceUri.NamespaceName, value, prefix);

    /// <summary>
    /// 移除指定屬性名稱與命名空間的屬性。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    public void RemoveAttribute(string localName, string namespaceUri)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        if (Attributes.Remove(key))
        {
            _attributePrefixes.Remove(key);
            IsModified = true;
        }
    }

    /// <summary>
    /// 取得指定屬性的原始命名空間前綴。
    /// </summary>
    /// <param name="attributeName">屬性名稱。</param>
    /// <returns>原始前綴；若未記錄則為 <see langword="null"/>。</returns>
    public string? GetAttributePrefix(OdfAttributeName attributeName)
    {
        return _attributePrefixes.TryGetValue(attributeName, out string? prefix) ? prefix : null;
    }

    /// <summary>
    /// 移除指定屬性名稱與命名空間的屬性。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間</param>
    public void RemoveAttribute(string localName, XNamespace namespaceUri) => RemoveAttribute(localName, namespaceUri.NamespaceName);

    #endregion

    /// <summary>
    /// 透過向上追溯至根元素，取得此節點所屬文件的 ODF 版本。
    /// </summary>
    /// <returns>所屬文件的 ODF 版本</returns>
    public OdfVersion GetDocumentVersion()
    {
        OdfNode current = this;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }
        if (current.NodeType == OdfNodeType.Element)
        {
            string? versionStr = current.GetAttribute("version", OdfNamespaces.Office);
            if (versionStr is null && current.LocalName == "manifest")
            {
                versionStr = current.GetAttribute("version", OdfNamespaces.Manifest);
            }
            if (versionStr is null)
            {
                foreach (var attr in current.Attributes)
                {
                    if (attr.Key.LocalName == "version")
                    {
                        versionStr = attr.Value;
                        break;
                    }
                }
            }
            
            if (versionStr is not null)
            {
                return versionStr switch
                {
                    "1.0" => OdfVersion.Odf10,
                    "1.1" => OdfVersion.Odf11,
                    "1.2" => OdfVersion.Odf12,
                    "1.3" => OdfVersion.Odf13,
                    "1.4" => OdfVersion.Odf14,
                    _ => OdfVersion.Odf14
                };
            }
        }
        return OdfVersion.Odf14; // 針對未連接或獨立節點的預設後備值
    }

    #region Clone & Import Node

    /// <summary>
    /// 複製當前節點。
    /// </summary>
    /// <param name="deep">是否進行深層複製 (遞迴複製子節點)</param>
    /// <returns>複製的新節點</returns>
    public virtual OdfNode CloneNode(bool deep)
    {
        var clone = new OdfNode(NodeType, LocalName, NamespaceUri, Prefix)
        {
            _value = _value
        };

        foreach (var attr in Attributes)
        {
            clone.Attributes[attr.Key] = attr.Value;
        }

        foreach (var attrPrefix in _attributePrefixes)
        {
            clone._attributePrefixes[attrPrefix.Key] = attrPrefix.Value;
        }

        if (deep)
        {
            foreach (var child in Children)
            {
                clone.AppendChild(child.CloneNode(true));
            }
        }

        return clone;
    }

    /// <summary>
    /// 將一個節點從來源 <see cref="OdfPackage"/> 匯入至目的 <see cref="OdfPackage"/>，自動複製並移轉其所屬的媒體檔案與樣式關聯。
    /// </summary>
    /// <param name="sourceNode">要匯入的來源節點</param>
    /// <param name="sourcePackage">來源套件</param>
    /// <param name="destPackage">目的套件</param>
    /// <returns>匯入後的新節點</returns>
    public static OdfNode ImportNode(OdfNode sourceNode, OdfPackage? sourcePackage, OdfPackage? destPackage)
    {
        if (sourceNode is null) throw new ArgumentNullException(nameof(sourceNode));

        // 先深層複製節點結構
        OdfNode importedNode = sourceNode.CloneNode(true);

        // 如果在不同的套件之間遷移，則掃描並重寫媒體或圖片資源
        if (sourcePackage is not null && destPackage is not null && sourcePackage != destPackage)
        {
            MigrateMediaReferences(importedNode, sourcePackage, destPackage);
        }

        return importedNode;
    }

    private static void MigrateMediaReferences(OdfNode node, OdfPackage sourcePackage, OdfPackage destPackage)
    {
        // 檢查節點中的 xlink:href 屬性
        var hrefKey = new OdfAttributeName("href", OdfNamespaces.XLink);
        if (node.Attributes.TryGetValue(hrefKey, out string? href) && href is not null)
        {
            // 媒體參考通常位於 zip 套件內的 "Pictures/" 目錄下
            if (href.StartsWith("Pictures/", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var stream = sourcePackage.GetEntryStream(href);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    byte[] mediaBytes = ms.ToArray();

                    // 在目的套件中註冊媒體
                    var mediaManager = new OdfMediaManager(destPackage);
                    string fileName = System.IO.Path.GetFileName(href);
                    string newHref = mediaManager.AddImage(mediaBytes, fileName);

                    // 更新複製節點中的參考
                    node.Attributes[hrefKey] = newHref;
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to migrate media reference '{href}' during node import: {ex.Message}");
                }
            }
            else
            {
                string normHref = href.TrimStart('.', '/').TrimEnd('/');
                string folderPrefix = normHref + "/";
                var entriesToCopy = sourcePackage.GetEntries()
                                                  .Where(e => e.Path.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                                                  .ToList();
                
                if (entriesToCopy.Count > 0)
                {
                    string originalPrefix = normHref.StartsWith("Object", StringComparison.OrdinalIgnoreCase) ? "Object" : "Formula";
                    string newHref = $"{originalPrefix}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    
                    foreach (var entryInfo in entriesToCopy)
                    {
                        string srcPath = entryInfo.Path;
                        string relativePath = srcPath.Substring(folderPrefix.Length);
                        string destPath = $"{newHref}/{relativePath}";
                        
                        try
                        {
                            byte[] bytes = sourcePackage.ReadEntry(srcPath);
                            string mediaType = sourcePackage.Manifest.TryGetValue(srcPath, out var m) ? m : "text/xml";
                            if (relativePath == "mimetype")
                            {
                                mediaType = Encoding.UTF8.GetString(bytes).Trim();
                            }
                            destPackage.WriteEntry(destPath, bytes, mediaType);
                        }
                        catch (Exception ex)
                        {
                            OdfKitDiagnostics.Warn($"Failed to migrate embedded entry '{srcPath}' during node import: {ex.Message}");
                        }
                    }
                    
                    node.Attributes[hrefKey] = newHref;
                    destPackage.SaveManifestToEntries();
                }
            }
        }

        // 遞迴子節點
        foreach (var child in node.Children)
        {
            MigrateMediaReferences(child, sourcePackage, destPackage);
        }
    }

    #endregion
}

/// <summary>
/// 提供 <see cref="OdfNode"/> 擴充方法的靜態類別。
/// </summary>
public static class OdfNodeExtensions
{
    /// <summary>
    /// 取得此節點的所有後代節點。
    /// </summary>
    /// <param name="node">目前節點</param>
    /// <returns>後代節點的列舉</returns>
    public static IEnumerable<OdfNode> Descendants(this OdfNode node)
    {
        if (node is null) yield break;
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var desc in child.Descendants())
            {
                yield return desc;
            }
        }
    }

    /// <summary>
    /// 尋找具有指定局部名稱與命名空間 URI 的第一個子元素。
    /// </summary>
    /// <param name="node">目前節點</param>
    /// <param name="localName">要尋找的局部名稱</param>
    /// <param name="nsUri">要尋找的命名空間 URI</param>
    /// <returns>符合的第一個子元素；如果找不到，則為 <see langword="null"/></returns>
    public static OdfNode? FindChildElement(this OdfNode node, string localName, string nsUri)
    {
        if (node is null) return null;
        foreach (var child in node.Children)
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
