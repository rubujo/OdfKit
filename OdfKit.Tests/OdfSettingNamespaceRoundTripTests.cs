using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests preservation of value-only namespace bindings and scoped setting identities.
/// 測試僅供屬性值使用的命名空間繫結及設定集合的區域識別。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfSettingNamespaceRoundTripTests
{
    /// <summary>
    /// Local aliases and rebinding retain their meaning across repeated XML writes.
    /// 區域別名及重新繫結在重複寫出 XML 後仍保留原有語意。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValueOnlyBindingsSurviveRoundTrips(bool lazy)
    {
        const string xml = "<root xmlns:c='urn:oasis:names:tc:opendocument:xmlns:config:1.0' xmlns:p='urn:first'>" +
            "<c:config-item-set c:name='p:first'/><c:config-item-set xmlns:p='urn:second' c:name='p:second'/>" +
            "<p:config-item-set xmlns:p='urn:oasis:names:tc:opendocument:xmlns:config:1.0' c:name='p:third'/>" +
            "<child xmlns='urn:default'><plain xmlns='' xmlns:value='urn:value' name='value:only'/></child></root>";
        string current = xml;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            OdfNode node = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(current), new OdfLoadOptions { AllowLazyLoading = lazy });
            current = Write(node);
            XElement output = XElement.Parse(current);
            Assert.Equal(XName.Get("root"), output.Name);
            XElement[] sets = output.Elements(XName.Get("config-item-set", OdfNamespaces.Config)).ToArray();
            Assert.Equal(3, sets.Length);
            Assert.Equal(new[] { "urn:first", "urn:second", OdfNamespaces.Config }, sets.Select(ExpandedSettingNamespace));
            XElement plain = Assert.Single(output.Descendants(XName.Get("plain")));
            Assert.Equal("urn:value", plain.GetNamespaceOfPrefix("value")!.NamespaceName);
            Assert.Equal("value:only", plain.Attribute("name")!.Value);
            Assert.Equal(XName.Get("child", "urn:default"), plain.Parent!.Name);
        }
    }

    /// <summary>
    /// Detached clones keep namespace bindings inherited from their original ancestors.
    /// 分離的複製節點保留從原始祖先繼承的命名空間繫結。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetachedClonesKeepInheritedBindings(bool deep)
    {
        OdfNode root = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(
            "<root xmlns:p='urn:value' xmlns:c='urn:oasis:names:tc:opendocument:xmlns:config:1.0'><c:config-item-set c:name='p:test'/></root>"));
        OdfNode clone = root.Children[0].CloneNode(deep);
        XElement output = XElement.Parse(Write(clone));
        Assert.Equal("urn:value", ExpandedSettingNamespace(output));
    }

    /// <summary>
    /// Built-in settings use namespace identity and preserve unrelated application settings.
    /// 內建設定依命名空間識別，並保留其他應用程式的設定。
    /// </summary>
    [Fact]
    public void OfficeAliasesDoNotBorrowForeignSettings()
    {
        using TextDocument document = TextDocument.Create();
        OdfNode settings = document.SettingsDom.Children.Single(node => node.LocalName == "settings" && node.NamespaceUri == OdfNamespaces.Office);
        settings.Children.Clear();
        const string officeUri = "http://openoffice.org/2004/office";
        OdfNode foreign = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(
            "<c:config-item-set xmlns:c='urn:oasis:names:tc:opendocument:xmlns:config:1.0' xmlns:ooo='urn:foreign' c:name='ooo:view-settings'><c:config-item c:name='ZoomValue' c:type='int'>7</c:config-item></c:config-item-set>"));
        OdfNode own = OdfXmlReader.Parse(Encoding.UTF8.GetBytes(
            "<c:config-item-set xmlns:c='urn:oasis:names:tc:opendocument:xmlns:config:1.0' xmlns:app='" + officeUri + "' c:name='app:view-settings'><c:config-item-map-indexed c:name='Views'><c:config-item-map-entry><c:config-item c:name='ZoomValue' c:type='int'>125</c:config-item></c:config-item-map-entry></c:config-item-map-indexed></c:config-item-set>"));
        settings.AppendChild(foreign);
        settings.AppendChild(own);
        Assert.Equal(125, document.ZoomLevel);
        document.ZoomLevel = 150;
        Assert.Equal(150, document.ZoomLevel);
        Assert.Equal("7", foreign.Children[0].TextContent);
        XElement output = XElement.Parse(Write(document.SettingsDom));
        Assert.Contains(output.Descendants(XName.Get("config-item-set", OdfNamespaces.Config)),
            node => ExpandedSettingNamespace(node) == "urn:foreign");
        Assert.Contains(output.Descendants(XName.Get("config-item-set", OdfNamespaces.Config)),
            node => ExpandedSettingNamespace(node) == officeUri);
    }

    private static string ExpandedSettingNamespace(XElement element)
    {
        string name = element.Attribute(XName.Get("name", OdfNamespaces.Config))!.Value;
        return element.GetNamespaceOfPrefix(name.Substring(0, name.IndexOf(':')))!.NamespaceName;
    }

    private static string Write(OdfNode node)
    {
        using var stream = new MemoryStream();
        OdfXmlWriter.Write(node, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
