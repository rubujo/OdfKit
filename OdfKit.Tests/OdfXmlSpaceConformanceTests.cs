using System.Text;
using System.Security;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests XML whitespace context across parsing and deferred subtree materialization.
/// 測試 XML 空白脈絡在剖析及延遲子樹具現化時的保存。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfXmlSpaceConformanceTests
{
    private const string Foreign = "urn:test:xml-space";

    /// <summary>
    /// Preserving whitespace cannot bypass the XML character budget on eager or deferred paths.
    /// 保留空白不能繞過一般或延遲剖析路徑的 XML 字元預算。
    /// </summary>
    [Theory]
    [InlineData(false, "root")]
    [InlineData(true, "root")]
    [InlineData(false, "styles")]
    [InlineData(true, "styles")]
    [InlineData(false, "table")]
    [InlineData(true, "table")]
    public void PreservedWhitespaceStillConsumesTheCharacterBudget(bool stream, string name)
    {
        string ns = name == "styles" ? OdfNamespaces.Office : name == "table" ? OdfNamespaces.Table : Foreign;
        byte[] bytes = Encoding.UTF8.GetBytes($"<x:{name} xmlns:x='{ns}' xml:space='preserve'>{new string(' ', 9000)}</x:{name}>");
        OdfLoadOptions options = new() { MaxXmlCharactersInDocument = 2048 };
        using MemoryStream input = new(bytes);
        if (stream)
            Assert.Throws<SecurityException>(() => OdfXmlReader.Parse(input, options));
        else
            Assert.Throws<SecurityException>(() => OdfXmlReader.Parse(bytes, options));
    }

    /// <summary>
    /// Preserve is inherited, default overrides it, and a later sibling retains the parent's context.
    /// preserve 會繼承，default 可覆寫它，後續同層節點仍使用父層脈絡。
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void XmlSpaceInheritanceUsesTheNearestDeclaration(bool stream, bool lazy)
    {
        string xml = $"<x:root xmlns:x='{Foreign}' xml:space='preserve'><x:keep> \t\n </x:keep><x:branch xml:space='default'><x:drop>  </x:drop><x:again xml:space='preserve'>  </x:again></x:branch><x:after>  </x:after></x:root>";
        OdfNode root = Read(xml, stream, lazy);
        Assert.Equal(" \t\n ", Find(root, "keep").TextContent);
        Assert.Equal(string.Empty, Find(root, "drop").TextContent);
        Assert.Equal("  ", Find(root, "again").TextContent);
        Assert.Equal("  ", Find(root, "after").TextContent);
        using MemoryStream output = new();
        OdfXmlWriter.Write(root, output);
        OdfNode saved = Read(Encoding.UTF8.GetString(output.ToArray()), stream, lazy);
        Assert.Equal(" \t\n ", Find(saved, "keep").TextContent);
        Assert.Equal(string.Empty, Find(saved, "drop").TextContent);
        Assert.Equal("  ", Find(saved, "again").TextContent);
        Assert.Equal("  ", Find(saved, "after").TextContent);
    }

    /// <summary>
    /// Small special subtrees retain inherited whitespace intent when reparsed through a wrapper.
    /// 小型特殊子樹透過外殼重新剖析時保留繼承的空白意圖。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallSubtreesKeepTheirInheritedWhitespaceContext(bool stream)
    {
        string xml = $"<x:root xmlns:x='{Foreign}' xmlns:o='{OdfNamespaces.Office}' xml:space='preserve'><o:styles><x:keep> \t </x:keep><x:drop xml:space='default'> </x:drop></o:styles></x:root>";
        OdfNode root = Read(xml, stream, true);
        Assert.Equal(" \t ", Find(root, "keep").TextContent);
        Assert.Equal(string.Empty, Find(root, "drop").TextContent);
    }

    /// <summary>
    /// Lazy clones preserve the original parse context even when detached or subsequently edited.
    /// 延遲載入的複本即使脫離父層或之後修改屬性，仍保留原始剖析脈絡。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LazyClonesRetainTheOriginalWhitespaceContext(bool stream)
    {
        string xml = $"<x:root xmlns:x='{Foreign}' xmlns:o='{OdfNamespaces.Office}' xml:space='preserve'><o:styles><x:padding>{new string('a', 9000)}</x:padding><x:keep>  </x:keep><x:drop xml:space='default'> </x:drop></o:styles></x:root>";
        OdfNode root = Read(xml, stream, true);
        OdfNode styles = Assert.Single(root.Children);
        Assert.True(styles._isLazy);
        OdfNode clone = styles.CloneNode(true);
        Assert.True(clone._isLazy);
        Assert.Null(clone.Parent);
        root.SetAttribute("space", OdfNamespaces.Xml, "default", "xml");
        Assert.Equal("  ", Find(clone, "keep").TextContent);
        Assert.Equal(string.Empty, Find(clone, "drop").TextContent);
        Assert.Equal("  ", Find(styles, "keep").TextContent);
        Assert.Equal(string.Empty, Find(styles, "drop").TextContent);
    }

    /// <summary>
    /// A lazy table retains inherited whitespace without treating all tables as mixed content.
    /// 延遲載入表格保留繼承的空白，不會將所有表格視為混合內容。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LazyTablesDistinguishPreserveFromDefault(bool stream)
    {
        foreach (string space in new[] { "default", "preserve" })
        {
            string xml = $"<x:root xmlns:x='{Foreign}' xmlns:t='{OdfNamespaces.Table}' xml:space='{space}'><t:table><x:padding>{new string('a', 9000)}</x:padding><x:value>  </x:value></t:table></x:root>";
            OdfNode root = Read(xml, stream, true);
            OdfNode table = Assert.Single(root.Children);
            Assert.True(table._isLazy);
            Assert.Equal(space == "preserve" ? "  " : string.Empty, Find(table, "value").TextContent);
        }
    }

    /// <summary>
    /// Native mixed content uses its data model while foreign names and attributes do not imitate native semantics.
    /// 原生混合內容使用其資料模型，外來名稱與屬性則不冒充原生語意。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeContentAndForeignWhitespaceHintsRemainDistinct(bool stream)
    {
        string xml = $"<x:root xmlns:x='{Foreign}' xmlns:t='{OdfNamespaces.Text}'><t:meta xml:id='M1'> </t:meta><x:p> </x:p><x:value x:space='preserve'> </x:value></x:root>";
        OdfNode root = Read(xml, stream, true);
        Assert.Equal(" ", root.Children.Single(node => node.NamespaceUri == OdfNamespaces.Text && node.LocalName == "meta").TextContent);
        Assert.Equal(string.Empty, Find(root, "p").TextContent);
        Assert.Equal(string.Empty, Find(root, "value").TextContent);
    }

    private static OdfNode Find(OdfNode root, string name) => root.Descendants().Single(node => node.NamespaceUri == Foreign && node.LocalName == name);

    private static OdfNode Read(string xml, bool stream, bool lazy)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        OdfLoadOptions options = new() { AllowLazyLoading = lazy };
        using MemoryStream input = new(bytes);
        return stream ? OdfXmlReader.Parse(input, options) : OdfXmlReader.Parse(bytes, options);
    }
}
