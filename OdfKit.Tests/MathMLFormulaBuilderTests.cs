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
    /// 測試 OdfFormulaDocument 高階 FromLatex Facade 入口方法。
    /// </summary>
    [Fact]
    public void TestFacadeFromLatex()
    {
        var latex = @"a + b = c";
        var doc = OdfFormulaDocument.FromLatex(latex);

        Assert.NotNull(doc);
        var xml = doc.MathMlXml;
        Assert.Contains("<mi>a</mi>", xml);
        Assert.Contains("<mo>+</mo>", xml);
        Assert.Contains("<mi>b</mi>", xml);
        Assert.Contains("<mo>=</mo>", xml);
        Assert.Contains("<mi>c</mi>", xml);
    }
}
