using System.Text;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests XML attribute normalization before ODF datatype validation.
/// 測試 ODF 資料型別驗證前的 XML 屬性正規化。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfXmlAttributeNormalizationConformanceTests
{
    /// <summary>
    /// Literal XML whitespace becomes spaces while character references and non-XML spaces retain their values.
    /// 字面的 XML 空白轉為空格，字元參照及非 XML 空白則保留原值。
    /// </summary>
    /// <param name="source">The XML attribute source. / XML 屬性來源。</param>
    /// <param name="expected">The normalized attribute value. / 正規化後的屬性值。</param>
    [Theory]
    [InlineData(" A\tB\nC\rD\r\nE ", " A B C D E ")]
    [InlineData("  A\t \nB  ", "  A   B  ")]
    [InlineData("A&#x9;B&#xA;C&#xD;D", "A\tB\nC\rD")]
    [InlineData(" A\t&#x9;\r\n&#xD;B ", " A \t \rB ")]
    [InlineData(" A\u0085\u00a0B ", " A\u0085\u00a0B ")]
    [InlineData("&lt;&amp;&quot;&apos;", "<&\"'")]
    public void AttributeValuesFollowXmlInformationSetRules(string source, string expected)
    {
        foreach (bool streamReader in new[] { false, true })
        {
            string xml = $"<q:header-decl xmlns:q='{OdfNamespaces.Presentation}' xmlns:n='{OdfNamespaces.Presentation}' n:name='{source}'>text</q:header-decl>";
            using MemoryStream input = new(Encoding.UTF8.GetBytes(xml));
            OdfNode node = streamReader ? OdfXmlReader.Parse(input) : OdfXmlReader.Parse(Encoding.UTF8.GetBytes(xml));
            Assert.Equal(expected, node.GetAttribute("name", OdfNamespaces.Presentation));
            using MemoryStream output = new();
            OdfXmlWriter.Write(node, output);
            XElement saved = XElement.Parse(Encoding.UTF8.GetString(output.ToArray()), LoadOptions.PreserveWhitespace);
            Assert.Equal(expected, (string?)saved.Attribute(XName.Get("name", OdfNamespaces.Presentation)));
        }
    }
}
