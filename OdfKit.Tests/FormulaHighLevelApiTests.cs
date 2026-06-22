using System;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定公式文件高階 API 的整合測試。
/// </summary>
public class FormulaHighLevelApiTests
{
    /// <summary>
    /// 驗證公式 Fluent builder 可建立 token row 並 round-trip。
    /// </summary>
    [Fact]
    public void OdfFormulaBuilderCreatesTokenRowAndRoundTrips()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(
                OdfMathToken.Identifier("E"),
                OdfMathToken.Operator("="),
                OdfMathToken.Identifier("mc"),
                OdfMathToken.Operator("^"),
                OdfMathToken.Number("2"))
            .Build();

        Assert.Equal("E=mc^2", formula.MathText);

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        Assert.Equal(5, loaded.GetMathTokens().Count);
        Assert.Equal("E", loaded.GetMathTokens()[0].Text);
    }

    /// <summary>
    /// 驗證建立公式文件、設定與獲取 MathML。
    /// </summary>
    [Fact]
    public void CreateAndGetMathMLTest()
    {
        string mathml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>E</mi><mo>=</mo><mi>m</mi><msup><mi>c</mi><mn>2</mn></msup></math>";
        using var formulaDoc = FormulaDocument.Create(mathml);

        // 驗證 MathText 與 MathML
        Assert.Equal("E=mc2", formulaDoc.MathText);
        Assert.Contains("<math", formulaDoc.GetMathML());
        Assert.Contains("<math:msup>", formulaDoc.GetMathML());

        // 驗證 SetMathML
        string newMathml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi><mo>+</mo><mi>y</mi></math>";
        formulaDoc.SetMathML(newMathml);
        Assert.Equal("x+y", formulaDoc.MathText);
        Assert.Contains("<math:mi>x</math:mi>", formulaDoc.GetMathML());
    }

    /// <summary>
    /// 驗證 <see cref="OdfFormulaDocument.GetMathTokens"/> 可讀回 SetMathRow 寫入的 token。
    /// </summary>
    [Fact]
    public void GetMathTokens_RoundTripsAfterSetMathRow()
    {
        using var formulaDoc = FormulaDocument.Create("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow/></math>");
        formulaDoc.SetMathRow(
            OdfMathToken.Identifier("a"),
            OdfMathToken.Operator("+"),
            OdfMathToken.Number("1"));

        var tokens = formulaDoc.GetMathTokens().ToList();
        Assert.Equal(3, tokens.Count);
        Assert.Equal(OdfMathTokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("a", tokens[0].Text);
        Assert.Equal(OdfMathTokenKind.Operator, tokens[1].Kind);
        Assert.Equal("+", tokens[1].Text);
        Assert.Equal(OdfMathTokenKind.Number, tokens[2].Kind);
        Assert.Equal("1", tokens[2].Text);
    }

    /// <summary>
    /// 驗證上標 MathML token 可寫入並於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void SuperscriptToken_RoundTripsAfterSaveAndLoad()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(
                OdfMathToken.Identifier("E"),
                OdfMathToken.Operator("="),
                OdfMathToken.Identifier("m"),
                OdfMathToken.Superscript(
                    OdfMathToken.Identifier("c"),
                    OdfMathToken.Number("2")))
            .Build();

        Assert.Contains("msup", formula.GetMathML());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        Assert.Equal(4, loaded.GetMathTokens().Count);
        OdfMathToken superscript = loaded.GetMathTokens()[3];
        Assert.Equal(OdfMathTokenKind.Superscript, superscript.Kind);
        Assert.Equal("c", superscript.Base?.Text);
        Assert.Equal("2", superscript.Script?.Text);
        Assert.Contains("msup", loaded.GetMathML());
    }

    /// <summary>
    /// 驗證下標 MathML token 可寫入並於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void SubscriptToken_RoundTripsAfterSaveAndLoad()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(
                OdfMathToken.Identifier("H"),
                OdfMathToken.Subscript(
                    OdfMathToken.Number("2"),
                    OdfMathToken.Number("2")))
            .Build();

        Assert.Contains("msub", formula.GetMathML());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        Assert.Equal(2, loaded.GetMathTokens().Count);
        OdfMathToken subscript = loaded.GetMathTokens()[1];
        Assert.Equal(OdfMathTokenKind.Subscript, subscript.Kind);
        Assert.Equal("2", subscript.Base?.Text);
        Assert.Equal("2", subscript.Script?.Text);
        Assert.Contains("msub", loaded.GetMathML());
    }

    /// <summary>
    /// 驗證公式文件的 Round-trip 載入與儲存。
    /// </summary>
    [Fact]
    public void RoundTripSaveAndLoadTest()
    {
        string mathml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>F</mi><mo>=</mo><mi>m</mi><mi>a</mi></math>";
        using var formulaDoc = FormulaDocument.Create(mathml);

        using var stream = new MemoryStream();
        formulaDoc.SaveToStream(stream);
        stream.Position = 0;

        using var loadedDoc = FormulaDocument.Load(stream, "formula.odf");
        Assert.Equal("F=ma", loadedDoc.MathText);
        Assert.Contains("<math:mi>F</math:mi>", loadedDoc.GetMathML());
    }
}
