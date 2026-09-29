using System;
using System.Globalization;
using System.Xml;

using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 限制元素巢狀深度的 <see cref="XmlReader"/> 包裝（內部協作者）。
/// <see cref="System.Xml.Linq.XDocument.Load(XmlReader)"/> 不限制深度，而後續對 XElement 的複製、
/// 走訪與清理多以遞迴實作，數萬層巢狀的輸入會造成無法攔截的堆疊溢位而使整個處理程序崩潰。
/// 超過限制時擲出 <see cref="XmlException"/>，讓呼叫端沿用既有的「XML 格式錯誤」處理。
/// </summary>
internal sealed class OdfDepthLimitedXmlReader(XmlReader inner, int maxDepth) : XmlReader
{
    private readonly XmlReader _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// 預設的最大元素巢狀深度，與 <see cref="OdfKit.DOM.OdfXmlReader.MaxElementDepth"/> 一致。
    /// </summary>
    internal const int DefaultMaxDepth = OdfKit.DOM.OdfXmlReader.MaxElementDepth;

    internal OdfDepthLimitedXmlReader(XmlReader inner) : this(inner, DefaultMaxDepth)
    {
    }

    public override bool Read()
    {
        bool read = _inner.Read();
        if (read && _inner.NodeType == XmlNodeType.Element && _inner.Depth + 1 > maxDepth)
        {
            throw new XmlException(
                OdfLocalizer.GetMessage("Err_OdfXmlReader_XmlElementNestingDepth", CultureInfo.InvariantCulture, _inner.Depth + 1, maxDepth));
        }

        return read;
    }

    public override XmlNodeType NodeType => _inner.NodeType;

    public override string LocalName => _inner.LocalName;

    public override string NamespaceURI => _inner.NamespaceURI;

    public override string Prefix => _inner.Prefix;

    public override bool HasValue => _inner.HasValue;

    public override string Value => _inner.Value;

    public override int Depth => _inner.Depth;

    public override string BaseURI => _inner.BaseURI;

    public override bool IsEmptyElement => _inner.IsEmptyElement;

    public override int AttributeCount => _inner.AttributeCount;

    public override bool EOF => _inner.EOF;

    public override ReadState ReadState => _inner.ReadState;

    public override XmlNameTable NameTable => _inner.NameTable;

    public override XmlReaderSettings? Settings => _inner.Settings;

    public override string XmlLang => _inner.XmlLang;

    public override XmlSpace XmlSpace => _inner.XmlSpace;

    public override bool IsDefault => _inner.IsDefault;

    public override string? GetAttribute(string name) => _inner.GetAttribute(name);

    public override string? GetAttribute(string name, string? namespaceURI) => _inner.GetAttribute(name, namespaceURI);

    public override string GetAttribute(int i) => _inner.GetAttribute(i);

    public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);

    public override bool MoveToAttribute(string name, string? ns) => _inner.MoveToAttribute(name, ns);

    public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();

    public override bool MoveToNextAttribute() => _inner.MoveToNextAttribute();

    public override bool MoveToElement() => _inner.MoveToElement();

    public override bool ReadAttributeValue() => _inner.ReadAttributeValue();

    public override string? LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);

    public override void ResolveEntity() => _inner.ResolveEntity();

    public override void Close() => _inner.Close();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
