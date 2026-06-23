using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Formula;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 OTF 公式範本與 FDF 扁平 XML 公式的雙向轉換工作流。
/// </summary>
public class FormulaVariantRoundTripTests
{
    /// <summary>
    /// 驗證 <see cref="FormulaTemplateDocument.CreateFromDocument(FormulaDocument)"/> 與
    /// <see cref="FormulaDocument.CreateFromTemplate(FormulaTemplateDocument)"/> 形成的雙向轉換，
    /// MathML 公式內容完整保留。
    /// </summary>
    [Fact]
    public void FormulaDocument_CreateTemplateFromDocument_RoundTripsBackToDocument()
    {
        using var original = FormulaDocument.Create(
            "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>F</mi><mo>=</mo><mi>m</mi><mi>a</mi></math>");

        using var template = FormulaTemplateDocument.CreateFromDocument(original);
        Assert.Equal("application/vnd.oasis.opendocument.formula-template", template.Package.MimeType);
        Assert.Equal(OdfDocumentKind.FormulaTemplate, template.DocumentKind);
        Assert.Equal("F=ma", template.MathText);

        using var restored = FormulaDocument.CreateFromTemplate(template);
        Assert.Equal("application/vnd.oasis.opendocument.formula", restored.Package.MimeType);
        Assert.Equal(OdfDocumentKind.Formula, restored.DocumentKind);
        Assert.Equal("F=ma", restored.MathText);
    }

    /// <summary>
    /// 驗證 <see cref="FlatFormulaDocument.CreateFromDocument(FormulaDocument)"/> 與
    /// <see cref="FormulaDocument.CreateFromFlatDocument(FlatFormulaDocument)"/> 形成的雙向轉換，
    /// MathML 公式內容完整保留，且 Flat 形態確實可由 OdfKit 自身正確儲存與重新載入。
    /// </summary>
    [Fact]
    public void FormulaDocument_CreateFlatDocument_RoundTripsBackToZip()
    {
        using var original = FormulaDocument.Create(
            "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>E</mi><mo>=</mo><mi>m</mi><msup><mi>c</mi><mn>2</mn></msup></math>");

        using var flat = FlatFormulaDocument.CreateFromDocument(original);
        Assert.True(flat.IsFlatXml);
        Assert.Equal(OdfDocumentKind.FlatFormula, flat.DocumentKind);
        Assert.Equal("E=mc2", flat.MathText);

        using var ms = new MemoryStream();
        flat.SaveToStream(ms);
        ms.Position = 0;
        string flatXml = new StreamReader(ms).ReadToEnd();
        Assert.StartsWith("<?xml", flatXml.TrimStart());
        Assert.Contains("<office:document", flatXml, StringComparison.Ordinal);

        using var restored = FormulaDocument.CreateFromFlatDocument(flat);
        Assert.False(restored.IsFlatXml);
        Assert.Equal(OdfDocumentKind.Formula, restored.DocumentKind);
        Assert.Equal("E=mc2", restored.MathText);
    }

    /// <summary>
    /// 驗證 <see cref="FlatFormulaDocument"/> 可直接儲存為 Flat XML 檔案並由 OdfKit 自身重新載入，
    /// 證明 Flat 公式文件本身（而非僅透過轉換 API）的 round-trip 正確性。
    /// </summary>
    [Fact]
    public void FlatFormulaDocument_SavesAndReloadsDirectlyAsFlatXmlFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"odfkit-formula-flat-{Guid.NewGuid():N}.fdf");
        try
        {
            using (var flat = FlatFormulaDocument.Create())
            {
                flat.SetIdentifierEquation("x", "y");
                flat.Save(path);
            }

            using FlatFormulaDocument reloaded = FlatFormulaDocument.Load(path);
            Assert.True(reloaded.IsFlatXml);
            Assert.Equal("x=y", reloaded.MathText);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// 驗證四個 Formula 雙向轉換工作流方法的邊界案例：
    /// 傳入 <see langword="null"/> 來源文件時皆擲出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void FormulaVariantConversions_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => FormulaTemplateDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => FormulaDocument.CreateFromTemplate(null!));
        Assert.Throws<ArgumentNullException>(() => FlatFormulaDocument.CreateFromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => FormulaDocument.CreateFromFlatDocument(null!));
    }
}
