using System;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對 ODF 官方規範中各種核心 XML 結構進行嚴格的 RELAX NG 驗證測試。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Corpus)]
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfRelaxNgCorpusTests
{
    private static MemoryStream CreatePackage(
        string mimeType,
        string contentXml,
        string? stylesXml = null)
    {
        var ms = new MemoryStream();
        using (OdfPackage package = OdfPackage.Create(ms, leaveOpen: true))
        {
            package.SetMimeType(mimeType);
            package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
            if (stylesXml != null)
            {
                package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes(stylesXml), "text/xml");
            }
            package.Save();
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// 測試帶有 style-name 屬性的文字段落。
    /// </summary>
    [Fact]
    public void TestTextParagraphWithStyleName()
    {
        string content =
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:body>" +
            "    <office:text>" +
            "      <text:p text:style-name=\"Standard\">Hello World</text:p>" +
            "    </office:text>" +
            "  </office:body>" +
            "</office:document-content>";

        using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
        using OdfPackage package = OdfPackage.Open(ms);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
    }

    /// <summary>
    /// 測試不帶屬性的 span 元素。
    /// </summary>
    [Fact]
    public void TestPlainSpanElement()
    {
        string content =
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:body>" +
            "    <office:text>" +
            "      <text:p>" +
            "        <text:span>Hello</text:span> World" +
            "      </text:p>" +
            "    </office:text>" +
            "  </office:body>" +
            "</office:document-content>";

        using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
        using OdfPackage package = OdfPackage.Open(ms);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
    }

    /// <summary>
    /// 測試帶有 style-name 屬性的 span 元素。
    /// </summary>
    [Fact]
    public void TestSpanElementWithStyleName()
    {
        string content =
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:body>" +
            "    <office:text>" +
            "      <text:p>" +
            "        <text:span text:style-name=\"Bold\">Hello</text:span> World" +
            "      </text:p>" +
            "    </office:text>" +
            "  </office:body>" +
            "</office:document-content>";

        using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content);
        using OdfPackage package = OdfPackage.Open(ms);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
    }

    /// <summary>
    /// 驗證包含表格與試算表結構的 ODS 文件是否能通過 ODF 1.4 Strict Profile 驗證。
    /// </summary>
    [Fact]
    public void ValidateComplexSpreadsheetDocumentRngPatterns()
    {
        string content =
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:body>" +
            "    <office:spreadsheet>" +
            "      <table:table table:name=\"Sheet1\">" +
            "        <table:table-column table:number-columns-repeated=\"3\"/>" +
            "        <table:table-row>" +
            "          <table:table-cell office:value-type=\"string\">" +
            "            <text:p>Cell A1</text:p>" +
            "          </table:table-cell>" +
            "          <table:table-cell office:value-type=\"float\" office:value=\"123.45\">" +
            "            <text:p>123.45</text:p>" +
            "          </table:table-cell>" +
            "          <table:table-cell office:value-type=\"boolean\" office:boolean-value=\"true\">" +
            "            <text:p>TRUE</text:p>" +
            "          </table:table-cell>" +
            "        </table:table-row>" +
            "      </table:table>" +
            "    </office:spreadsheet>" +
            "  </office:body>" +
            "</office:document-content>";

        using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.spreadsheet", content);
        using OdfPackage package = OdfPackage.Open(ms);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
    }

    /// <summary>
    /// 驗證繪圖與形狀元素是否符合 ODF 1.4 Strict Profile 的模式匹配規範。
    /// </summary>
    [Fact]
    public void ValidateDrawingElementsRngPatterns()
    {
        // 加上必填的 draw:master-page-name 屬性，且將子元素 text-box 替換為符合 ODF 規格的 text:p
        string content =
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:body>" +
            "    <office:drawing>" +
            "      <draw:page draw:name=\"Page1\" draw:master-page-name=\"Standard\">" +
            "        <draw:rect draw:style-name=\"MyRect\" svg:x=\"10mm\" svg:y=\"20mm\" svg:width=\"30mm\" svg:height=\"40mm\">" +
            "          <text:p>Hello</text:p>" +
            "        </draw:rect>" +
            "      </draw:page>" +
            "    </office:drawing>" +
            "  </office:body>" +
            "</office:document-content>";

        using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.graphics", content);
        using OdfPackage package = OdfPackage.Open(ms);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
    }

    /// <summary>
    /// 驗證複雜樣式（styles.xml）中的 page layout 模式匹配。
    /// </summary>
    [Fact]
    public void ValidatePageLayoutPropertiesRngPatterns()
    {
        string content =
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:body>" +
            "    <office:text/>" +
            "  </office:body>" +
            "</office:document-content>";

        string styles =
            "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
            "office:version=\"1.4\">" +
            "  <office:master-styles>" +
            "    <style:master-page style:name=\"Standard\" style:page-layout-name=\"Mylayout\"/>" +
            "  </office:master-styles>" +
            "</office:document-styles>";

        using MemoryStream ms = CreatePackage("application/vnd.oasis.opendocument.text", content, styles);
        using OdfPackage package = OdfPackage.Open(ms);
        OdfValidationReport report = OdfPackageValidator.Validate(package, OdfComplianceProfiles.OasisOdf14Strict);

        Assert.True(report.IsValid, string.Join(", ", report.Issues.Select(i => i.Message)));
    }
}
