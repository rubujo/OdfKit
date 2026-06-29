using System.Text;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// Base class for nodes in the ODF Document Object Model (DOM).
/// 表示 ODF 文件物件模型 (DOM) 中的節點基底類別。
/// </summary>
public partial class OdfNode
{
    /// <summary>
    /// Gets the type of the node.
    /// 取得節點的類型。
    /// </summary>
    public OdfNodeType NodeType { get; }

    /// <summary>
    /// Gets the local name of the node.
    /// 取得節點的局部名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// Gets the namespace URI of the node.
    /// 取得節點的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// Gets or sets the namespace prefix of the node.
    /// 取得或設定節點的命名空間前綴。
    /// </summary>
    public string? Prefix { get; set; }

    private string? _value; // 用於文字節點

    /// <summary>
    /// Gets the parent node of this node.
    /// 取得此節點的父節點。
    /// </summary>
    public OdfNode? Parent { get; internal set; }

    private OdfDocument? _document;

    /// <summary>
    /// Gets the ODF document this node belongs to.
    /// 取得此節點所屬的 ODF 文件。
    /// </summary>
    public OdfDocument? Document
    {
        get
        {
            if (_document is not null)
                return _document;
            return Parent?.Document;
        }
        internal set
        {
            _document = value;
        }
    }

    /// <summary>
    /// Gets the child node collection of this node (a doubly linked list with O(1) insert/remove).
    /// 取得此節點的子節點集合（雙向鏈結串列；插入／移除為 O(1)）。
    /// </summary>
    public OdfNodeChildList Children { get; }

    /// <summary>
    /// Gets the attribute dictionary of this node.
    /// 取得此節點的屬性字典。
    /// </summary>
    public Dictionary<OdfAttributeName, string> Attributes { get; } = new(OdfAttributeNameComparer.Instance);

    private readonly Dictionary<OdfAttributeName, string> _attributePrefixes = new(OdfAttributeNameComparer.Instance);

    /// <summary>
    /// Gets or sets a dirty flag indicating whether this node has been added or modified, used for automatic style deduplication.
    /// 取得或設定標記此節點是否被新增或修改 (Dirty Flag)，用於自動樣式去重。
    /// </summary>
    public bool IsModified { get; set; }

    /// <summary>
    /// 此節點在父節點 <see cref="Children"/> 清單中的快取索引；-1 表示尚未建立或已脫離父節點。
    /// </summary>
    internal int SiblingIndex { get; set; } = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfNode"/> class.
    /// 初始化 <see cref="OdfNode"/> 類別的新執行個體。
    /// </summary>
    /// <param name="nodeType">The node type. / 節點類型。</param>
    /// <param name="localName">The local name. / 局部名稱。</param>
    /// <param name="namespaceUri">The namespace URI. / 命名空間 URI。</param>
    /// <param name="prefix">The namespace prefix. / 命名空間前綴。</param>
    public OdfNode(OdfNodeType nodeType, string localName, string namespaceUri, string? prefix = null)
    {
        NodeType = nodeType;
        LocalName = localName;
        NamespaceUri = namespaceUri;
        Prefix = prefix;
        Children = new OdfNodeChildList(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfNode"/> class.
    /// 初始化 <see cref="OdfNode"/> 類別的新執行個體。
    /// </summary>
    /// <param name="nodeType">The node type. / 節點類型。</param>
    /// <param name="localName">The local name. / 局部名稱。</param>
    /// <param name="namespaceUri">The namespace. / 命名空間。</param>
    /// <param name="prefix">The namespace prefix. / 命名空間前綴。</param>
    public OdfNode(OdfNodeType nodeType, string localName, XNamespace namespaceUri, string? prefix = null)
        : this(nodeType, localName, namespaceUri.NamespaceName, prefix)
    {
    }

    /// <summary>
    /// Recursively resets the modified flag of this node and all of its child nodes to <see langword="false"/>.
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
    /// Gets or sets the text content contained by the node.
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

            if (Children.Count == 0)
            {
                return string.Empty;
            }

            if (Children.Count == 1)
            {
                return Children[0].TextContent;
            }

            var sb = new StringBuilder();
            WriteTextContentTo(new StringBuilderTextSink(sb));
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
    /// Walks up to the root node to get the ODF version of the document this node belongs to.
    /// 向上追溯至根節點，取得此節點所屬文件的 ODF 版本。
    /// </summary>
    /// <returns>The ODF version of the owning document. / 所屬文件的 ODF 版本。</returns>
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

    /// <summary>
    /// Invalidates the style cache of this node. The base class implementation does nothing.
    /// 使此節點的樣式快取失效。基底類別實作不執行任何動作。
    /// </summary>
    public virtual void InvalidateStyle()
    {
    }

    internal ReadOnlyMemory<byte> _lazyXmlMemory;
    internal IntPtr _lazyXmlPtr;
    internal int _lazyXmlLen;
    internal OdfXmlByteRange? _xmlByteRange;
    internal bool _isLazy;

    /// <summary>
    /// Attempts to get the byte range of this node within the source UTF-8 XML buffer.
    /// 嘗試取得此節點在來源 UTF-8 XML 緩衝區中的位元組範圍。
    /// </summary>
    /// <param name="range">The byte range of the full element and content segment, if a source index exists. / 若存在來源索引，則為完整元素與內容區段的位元組範圍。</param>
    /// <returns><see langword="true"/> if this node carries a source XML byte index. / 若此節點帶有來源 XML 位元組索引則為 <see langword="true"/>。</returns>
    public bool TryGetXmlByteRange(out OdfXmlByteRange range)
    {
        if (_xmlByteRange is OdfXmlByteRange existing)
        {
            range = existing;
            return true;
        }

        range = default;
        return false;
    }

    /// <summary>
    /// Ensures that lazily parsed child nodes have been materialized.
    /// 確保延遲解析的子節點已具現化。
    /// </summary>
    public void EnsureMaterialized()
    {
        if (_isLazy)
        {
            _isLazy = false;
            if (_lazyXmlPtr != IntPtr.Zero && _lazyXmlLen > 0)
            {
                unsafe
                {
                    using var manager = new UnmanagedMemoryManager(_lazyXmlPtr, _lazyXmlLen);
                    MaterializeChildren(manager.Memory);
                }
                _lazyXmlPtr = IntPtr.Zero;
                _lazyXmlLen = 0;
            }
            else if (!_lazyXmlMemory.IsEmpty)
            {
                var data = _lazyXmlMemory;
                _lazyXmlMemory = default;
                MaterializeChildren(data);
            }
        }
    }

    internal bool TryWriteLazyXml(System.Xml.XmlWriter writer)
    {
        if (!_isLazy || Children.LoadedCount != 0)
        {
            return false;
        }

        if (_lazyXmlPtr != IntPtr.Zero && _lazyXmlLen > 0)
        {
            unsafe
            {
                ReadOnlySpan<byte> xml = new((byte*)_lazyXmlPtr, _lazyXmlLen);
                writer.WriteRaw(Encoding.UTF8.GetString(
#if NETSTANDARD2_0
                    xml.ToArray()
#else
                    xml
#endif
                ));
            }

            return true;
        }

        if (!_lazyXmlMemory.IsEmpty)
        {
            writer.WriteRaw(Encoding.UTF8.GetString(
#if NETSTANDARD2_0
                _lazyXmlMemory.ToArray()
#else
                _lazyXmlMemory.Span
#endif
            ));
            return true;
        }

        return false;
    }

    private void MaterializeChildren(ReadOnlyMemory<byte> xmlData)
    {
        byte[] prefixBytes = Encoding.UTF8.GetBytes("<wrapper" +
            " xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"" +
            " xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"" +
            " xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\"" +
            " xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\"" +
            " xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\"" +
            " xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\"" +
            " xmlns:xlink=\"http://www.w3.org/1999/xlink\"" +
            ">");
        byte[] suffixBytes = Encoding.UTF8.GetBytes("</wrapper>");

        using var seqStream = new OdfSequenceStream(prefixBytes, xmlData, suffixBytes);
        OdfNode? tempRoot = OdfXmlReader.Parse(seqStream, new OdfLoadOptions { AllowLazyLoading = false });
        if (tempRoot is not null)
        {
            OdfNode? nextChild = tempRoot.FirstChild;
            while (nextChild is not null)
            {
                OdfNode? sibling = nextChild.NextSibling;
                tempRoot.Children.Remove(nextChild);
                this.AppendChild(nextChild);
                nextChild = sibling;
            }
        }
    }

    private sealed class OdfSequenceStream : System.IO.Stream
    {
        private readonly ReadOnlyMemory<byte>[] _buffers;
        private int _currentBufferIndex;
        private int _currentBufferPosition;
        private long _position;

        public OdfSequenceStream(params ReadOnlyMemory<byte>[] buffers)
        {
            _buffers = buffers;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_currentBufferIndex >= _buffers.Length)
                return 0;

            int totalRead = 0;
            while (count > 0 && _currentBufferIndex < _buffers.Length)
            {
                var current = _buffers[_currentBufferIndex];
                int remaining = current.Length - _currentBufferPosition;
                if (remaining <= 0)
                {
                    _currentBufferIndex++;
                    _currentBufferPosition = 0;
                    continue;
                }

                int toCopy = Math.Min(remaining, count);
                current.Span.Slice(_currentBufferPosition, toCopy).CopyTo(buffer.AsSpan(offset, toCopy));
                _currentBufferPosition += toCopy;
                offset += toCopy;
                count -= toCopy;
                totalRead += toCopy;
                _position += toCopy;
            }

            return totalRead;
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// Attempts to write this node's XML using a custom override. If it returns <see langword="true"/>, default serialization is skipped.
    /// 嘗試自訂寫出 XML。若傳回 <see langword="true"/>，則略過預設的序列化行為。
    /// </summary>
    /// <param name="writer">The XML writer. / XML 寫入器。</param>
    /// <param name="nsDict">The namespace declaration dictionary. / 命名空間宣告字典。</param>
    /// <returns><see langword="true"/> if this node has taken over writing; otherwise <see langword="false"/>. / 若已由該節點接管寫出則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public virtual bool TryWriteOverride(System.Xml.XmlWriter writer, Dictionary<string, string> nsDict) => false;
}

/// <summary>
/// Static class providing extension methods for <see cref="OdfNode"/>.
/// 提供 <see cref="OdfNode"/> 擴充方法的靜態類別。
/// </summary>
public static class OdfNodeExtensions
{
    /// <summary>
    /// Gets all descendant nodes of this node.
    /// 取得此節點的所有後代節點。
    /// </summary>
    /// <param name="node">The current node. / 目前節點。</param>
    /// <returns>An enumeration of descendant nodes. / 後代節點的列舉。</returns>
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
    /// Finds the first child element with the specified local name and namespace URI.
    /// 尋找具有指定局部名稱與命名空間 URI 的第一個子元素。
    /// </summary>
    /// <param name="node">The current node. / 目前節點。</param>
    /// <param name="localName">The local name to find. / 要尋找的局部名稱。</param>
    /// <param name="nsUri">The namespace URI to find. / 要尋找的命名空間 URI。</param>
    /// <returns>The first matching child element, or <see langword="null"/> if not found. / 符合的第一個子元素；如果找不到，則為 <see langword="null"/>。</returns>
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

internal sealed unsafe class UnmanagedMemoryManager : System.Buffers.MemoryManager<byte>
{
    private readonly byte* _pointer;
    private readonly int _length;

    public UnmanagedMemoryManager(byte* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

    public UnmanagedMemoryManager(IntPtr pointer, int length)
    {
        _pointer = (byte*)pointer;
        _length = length;
    }

    public override Span<byte> GetSpan() => new Span<byte>(_pointer, _length);

    public override System.Buffers.MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex > _length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        return new System.Buffers.MemoryHandle(_pointer + elementIndex);
    }

    public override void Unpin() { }

    protected override void Dispose(bool disposing) { }
}
