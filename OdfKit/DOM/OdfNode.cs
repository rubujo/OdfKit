using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF 文件物件模型 (DOM) 中的節點基底類別。
/// </summary>
public partial class OdfNode
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

    /// <summary>
    /// 向上追溯至根節點，取得此節點所屬文件的 ODF 版本。
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

        return OdfVersion.Odf14;
    }

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
        if (node is null)
            yield break;
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
        if (node is null)
            return null;
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
