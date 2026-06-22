using System.Text;
using System.IO;
using System;
using Xunit;
using OdfKit.Formula;

namespace OdfKit.Tests;

/// <summary>
/// 測試 LaTeX 轉 MathML 的正確性，涵蓋分數、上下標、根號、矩陣、希臘字母與常用微積分符號。
/// </summary>
public class MathMLFormulaBuilderTests
{
    /// <summary>
    /// 測試分數的 LaTeX 轉換結果。
    /// </summary>
    [Fact]
    public void TestFraction()
    {
        var latex = @"\frac{a}{b}";
        var xml = OdfFormulaLatexConverter.Convert(latex);

        Assert.Contains("<mfrac>", xml);
        Assert.Contains("<mi>a</mi>", xml);
        Assert.Contains("<mi>b</mi>", xml);
    }

    /// <summary>
    /// 測試上標、下標以及雙重上下標的 LaTeX 轉換結果。
    /// </summary>
    [Fact]
    public void TestScripts()
    {
        // 僅上標
        var xmlSup = OdfFormulaLatexConverter.Convert("x^2");
        Assert.Contains("<msup>", xmlSup);
        Assert.Contains("<mi>x</mi>", xmlSup);
        Assert.Contains("<mn>2</mn>", xmlSup);

        // 僅下標
        var xmlSub = OdfFormulaLatexConverter.Convert("x_i");
        Assert.Contains("<msub>", xmlSub);
        Assert.Contains("<mi>x</mi>", xmlSub);
        Assert.Contains("<mi>i</mi>", xmlSub);

        // 同時上下標
        var xmlBoth = OdfFormulaLatexConverter.Convert("x_i^2");
        Assert.Contains("<msubsup>", xmlBoth);
        Assert.Contains("<mi>x</mi>", xmlBoth);
        Assert.Contains("<mi>i</mi>", xmlBoth);
        Assert.Contains("<mn>2</mn>", xmlBoth);
    }

    /// <summary>
    /// 測試平方根與任意次方根的 LaTeX 轉換結果。
    /// </summary>
    [Fact]
    public void TestRadicals()
    {
        // 平方根
        var xmlSqrt = OdfFormulaLatexConverter.Convert(@"\sqrt{x}");
        Assert.Contains("<msqrt>", xmlSqrt);
        Assert.Contains("<mi>x</mi>", xmlSqrt);

        // 三次方根
        var xmlRoot = OdfFormulaLatexConverter.Convert(@"\sqrt[3]{x}");
        Assert.Contains("<mroot>", xmlRoot);
        Assert.Contains("<mi>x</mi>", xmlRoot);
        Assert.Contains("<mn>3</mn>", xmlRoot);
    }

    /// <summary>
    /// 測試矩陣環境的 LaTeX 轉換結果。
    /// </summary>
    [Fact]
    public void TestMatrix()
    {
        var latex = @"\begin{matrix} a & b \\ c & d \end{matrix}";
        var xml = OdfFormulaLatexConverter.Convert(latex);

        Assert.Contains("<mtable>", xml);
        Assert.Contains("<mtr>", xml);
        Assert.Contains("<mtd>", xml);
        Assert.Contains("<mi>a</mi>", xml);
        Assert.Contains("<mi>b</mi>", xml);
        Assert.Contains("<mi>c</mi>", xml);
        Assert.Contains("<mi>d</mi>", xml);
    }

    /// <summary>
    /// 測試希臘字母的 LaTeX 轉換結果。
    /// </summary>
    [Fact]
    public void TestGreekLetters()
    {
        var latex = @"\alpha + \beta = \theta";
        var xml = OdfFormulaLatexConverter.Convert(latex);

        Assert.Contains("<mi>α</mi>", xml);
        Assert.Contains("<mi>β</mi>", xml);
        Assert.Contains("<mi>θ</mi>", xml);
    }

    /// <summary>
    /// 測試極限、加總與積分等常用微積分符號的 LaTeX 轉換結果。
    /// </summary>
    [Fact]
    public void TestCalculusSymbols()
    {
        // 總和符號 (munderover)
        var xmlSum = OdfFormulaLatexConverter.Convert(@"\sum_{i=1}^n x_i");
        Assert.Contains("<munderover>", xmlSum);
        Assert.Contains("<mo>∑</mo>", xmlSum);
        Assert.Contains("<mi>x</mi>", xmlSum);

        // 積分符號 (msubsup)
        var xmlInt = OdfFormulaLatexConverter.Convert(@"\int_a^b x dx");
        Assert.Contains("<msubsup>", xmlInt);
        Assert.Contains("<mo>∫</mo>", xmlInt);

        // 極限符號 (munder)
        var xmlLim = OdfFormulaLatexConverter.Convert(@"\lim_{x \to 0} f(x)");
        Assert.Contains("<munder>", xmlLim);
        Assert.Contains("<mo>lim</mo>", xmlLim);
    }

    /// <summary>
    /// 驗證 WithMathML 即使輸入的 MathML XML 使用無前綴的預設命名空間宣告，
    /// 寫回時仍會正規化為固定的 <c>math:</c> 前綴；並驗證 <c>content.xml</c> 實際寫入封裝時，
    /// 根節點為裸 <c>math:math</c>（ODF 公式文件專屬封裝慣例，未以 office:document-content 包裹），
    /// 此為真實 LibreOffice <c>math8</c> 匯入篩選器能成功開啟檔案的必要結構。
    /// </summary>
    [Fact]
    public void WithMathMLNormalizesPrefixAndPersistsBareMathRoot()
    {
        using var formula = OdfFormulaDocument.Builder()
            .WithMathML("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi><mo>+</mo><mi>y</mi></math>")
            .Build();
        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;
        using var pkg = OdfKit.Core.OdfPackage.Open(stream, leaveOpen: true);
        using var contentStream = pkg.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string content = reader.ReadToEnd();

        Assert.Contains("xmlns:math=\"http://www.w3.org/1998/Math/MathML\"", content);
        Assert.Contains("<math:math", content);
        Assert.Contains("<math:mi>x</math:mi>", content);
        Assert.DoesNotContain("office:document-content", content);
        Assert.DoesNotContain("office:formula", content);
    }

    /// <summary>
    /// 驗證 OdfFormulaDocument 可正確載入真實 LibreOffice 產生的 ODF 公式文件
    /// （<c>content.xml</c> 根節點為裸 <c>math:math</c>，未以 office:document-content 包裹）。
    /// 此結構複製自真實 LibreOffice 26.2 透過 private:factory/smath 轉存出的 content.xml，
    /// 修正前 OdfKit 會將裸 math 根節點誤判為一般包裹結構，導致讀回的 MathText 永遠是空字串。
    /// </summary>
    [Fact]
    public void LoadsRealLibreOfficeBareMathRootFormula()
    {
        string contentXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<math xmlns=\"http://www.w3.org/1998/Math/MathML\" display=\"block\"><mi>x</mi><mo>+</mo><mi>y</mi></math>";

        using var ms = new MemoryStream();
        using (var pkg = OdfKit.Core.OdfPackage.Create(ms, leaveOpen: true))
        {
            pkg.SetMimeType("application/vnd.oasis.opendocument.formula");
            pkg.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
            pkg.Save();
        }
        ms.Position = 0;

        using var formula = OdfFormulaDocument.Load(ms, "real-lo.odf");
        Assert.Equal("x+y", formula.MathText);

        // 重新儲存後，仍須維持真機相容的裸 math 根節點形狀（而非退化回包裹結構）。
        using var resaved = new MemoryStream();
        formula.SaveToStream(resaved);
        resaved.Position = 0;
        using var resavedPkg = OdfKit.Core.OdfPackage.Open(resaved, leaveOpen: true);
        using var resavedContentStream = resavedPkg.GetEntryStream("content.xml");
        using var resavedReader = new StreamReader(resavedContentStream);
        string resavedContent = resavedReader.ReadToEnd();
        Assert.DoesNotContain("office:document-content", resavedContent);
    }

    /// <summary>
    /// 測試 OdfFormulaDocument 高階 FromLatex Facade 入口方法。
    /// </summary>
    [Fact]
    public void TestFacadeFromLatex()
    {
        var latex = @"a + b = c";
        var doc = OdfFormulaDocument.FromLatex(latex);

        Assert.NotNull(doc);
        var xml = doc.MathMlXml;
        Assert.Contains("<math:mi>a</math:mi>", xml);
        Assert.Contains("<math:mo>+</math:mo>", xml);
        Assert.Contains("<math:mi>b</math:mi>", xml);
        Assert.Contains("<math:mo>=</math:mo>", xml);
        Assert.Contains("<math:mi>c</math:mi>", xml);
    }
}
