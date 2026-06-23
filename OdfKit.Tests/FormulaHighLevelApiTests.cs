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
    /// 驗證分數、根號 token 可寫入並於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void FractionAndRadicalToken_RoundTripsAfterSaveAndLoad()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(
                OdfMathToken.Fraction(OdfMathToken.Number("1"), OdfMathToken.Number("2")),
                OdfMathToken.Operator("+"),
                OdfMathToken.Radical(OdfMathToken.Number("9")))
            .Build();

        Assert.Contains("mfrac", formula.GetMathML());
        Assert.Contains("msqrt", formula.GetMathML());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        var tokens = loaded.GetMathTokens().ToList();
        Assert.Equal(3, tokens.Count);

        Assert.Equal(OdfMathTokenKind.Fraction, tokens[0].Kind);
        Assert.Equal("1", tokens[0].Base?.Text);
        Assert.Equal("2", tokens[0].Script?.Text);

        Assert.Equal(OdfMathTokenKind.Radical, tokens[2].Kind);
        Assert.Equal("9", tokens[2].Base?.Text);
        Assert.Null(tokens[2].Script);
    }

    /// <summary>
    /// 驗證 <see cref="OdfMathBuilder"/> 可組合矩陣 token 並於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void OdfMathBuilder_BuildsMatrixAndRoundTrips()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.FromBuilder(builder => builder
            .Matrix(
                row => row.Number("1").Number("2"),
                row => row.Number("3").Number("4")));

        Assert.Contains("mtable", formula.GetMathML());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "matrix.odf");
        var tokens = loaded.GetMathTokens().ToList();
        OdfMathToken matrix = Assert.Single(tokens);
        Assert.Equal(OdfMathTokenKind.Matrix, matrix.Kind);
        Assert.NotNull(matrix.Children);
        Assert.Equal(2, matrix.Children!.Count);
        Assert.Equal(2, matrix.Children[0].Children?.Count);
        Assert.Equal("1", matrix.Children[0].Children![0].Text);
        Assert.Equal("4", matrix.Children[1].Children![1].Text);
    }

    /// <summary>
    /// 驗證 <see cref="OdfMathToken.WithAttribute"/> 設定的 MathML 通用屬性可寫入並於儲存／載入後讀回。
    /// </summary>
    [Fact]
    public void MathTokenAttributes_RoundTripAfterSaveAndLoad()
    {
        OdfMathToken identifier = OdfMathToken.Identifier("x")
            .WithAttribute("mathvariant", "bold")
            .WithAttribute("mathcolor", "#FF0000");

        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(identifier)
            .Build();

        Assert.Contains("mathvariant=\"bold\"", formula.GetMathML());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        OdfMathToken token = Assert.Single(loaded.GetMathTokens());
        Assert.NotNull(token.Attributes);
        Assert.Equal("bold", token.Attributes!["mathvariant"]);
        Assert.Equal("#FF0000", token.Attributes["mathcolor"]);
    }

    /// <summary>
    /// 驗證 <see cref="OdfFormulaDocument.ToLatex"/> 可將分數、根號與上標結構反向轉換為 LaTeX。
    /// </summary>
    [Fact]
    public void ToLatex_ConvertsFractionRadicalAndSuperscript()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.Builder()
            .WithTokens(
                OdfMathToken.Fraction(OdfMathToken.Number("1"), OdfMathToken.Number("2")),
                OdfMathToken.Operator("+"),
                OdfMathToken.Radical(OdfMathToken.Number("9")),
                OdfMathToken.Superscript(OdfMathToken.Identifier("x"), OdfMathToken.Number("2")))
            .Build();

        string latex = formula.ToLatex();

        Assert.Contains("\\frac{1}{2}", latex);
        Assert.Contains("\\sqrt{9}", latex);
        Assert.Contains("{x}^{2}", latex);
    }

    /// <summary>
    /// 驗證 <see cref="OdfFormulaDocument.ToLatex"/> 與 <see cref="OdfFormulaDocument.FromLatex"/>
    /// 對基本算式具備語意往返一致性（轉為 MathML 再轉回 LaTeX 後，結構保持等價）。
    /// </summary>
    [Fact]
    public void ToLatex_RoundTripsThroughFromLatex()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.FromLatex("\\frac{a}{b}");
        string latex = formula.ToLatex();

        using OdfFormulaDocument reconverted = OdfFormulaDocument.FromLatex(latex);
        Assert.Equal(formula.GetMathML(), reconverted.GetMathML());
    }

    /// <summary>
    /// 驗證 <see cref="OdfMathBuilder.Accent"/> 可建立帶有 <c>accent="true"</c> 屬性的 <c>mover</c>
    /// token，並於儲存／載入後保留該屬性（W3C MathML 重音語意，與一般 <c>Over</c> 的極限記號語意有別）。
    /// </summary>
    [Fact]
    public void Accent_SetsAccentAttributeAndRoundTrips()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.FromBuilder(
            b => b.Accent(inner => inner.Identifier("v"), mark => mark.Operator("→")));

        Assert.Contains("accent=\"true\"", formula.GetMathML());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        OdfMathToken token = Assert.Single(loaded.GetMathTokens());
        Assert.Equal(OdfMathTokenKind.Over, token.Kind);
        Assert.Equal("true", token.Attributes?["accent"]);
    }

    /// <summary>
    /// 驗證 <see cref="OdfMathBuilder.Apply"/> 可建立 Content MathML <c>apply</c> token，
    /// 並於儲存／載入後保留運算子與運算元；<see cref="OdfFormulaDocument.ToLatex"/> 可將其
    /// 轉換為對應的 LaTeX 中綴運算式。
    /// </summary>
    [Fact]
    public void Apply_RoundTripsAndConvertsToLatex()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.FromBuilder(
            b => b.Apply("plus", x => x.Identifier("x"), y => y.Identifier("y")));

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        OdfMathToken token = Assert.Single(loaded.GetMathTokens());
        Assert.Equal(OdfMathTokenKind.Apply, token.Kind);
        Assert.Equal("plus", token.Text);
        Assert.Equal(2, token.Children?.Count);

        Assert.Equal("x + y", loaded.ToLatex());
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

    /// <summary>
    /// 驗證 <see cref="OdfFormulaDocument.SetAnnotation"/>／<see cref="OdfFormulaDocument.GetAnnotation"/>
    /// 可附加、讀取並移除 <c>math:semantics</c>／<c>math:annotation</c> 標註，且不影響既有呈現
    /// 內容（presentation MathML）的 token 讀取，並於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void SetAndGetAnnotation_RoundTripsAfterSaveAndLoad_WithoutAffectingTokens()
    {
        using var formula = OdfFormulaDocument.Builder()
            .WithTokens(OdfMathToken.Identifier("x"), OdfMathToken.Operator("+"), OdfMathToken.Identifier("y"))
            .Build();

        Assert.Null(formula.GetAnnotation("application/x-tex"));

        formula.SetAnnotation("application/x-tex", "x + y");
        formula.SetAnnotation("StarMath 5.0", "x + y");
        Assert.Equal("x + y", formula.GetAnnotation("application/x-tex"));
        Assert.Equal("x + y", formula.GetAnnotation("StarMath 5.0"));
        Assert.Equal(3, formula.GetMathTokens().Count);

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        Assert.Equal("x + y", loaded.GetAnnotation("application/x-tex"));
        Assert.Equal("x + y", loaded.GetAnnotation("StarMath 5.0"));
        Assert.Equal(3, loaded.GetMathTokens().Count);
        Assert.Equal("x", loaded.GetMathTokens()[0].Text);

        loaded.SetAnnotation("application/x-tex", null);
        Assert.Null(loaded.GetAnnotation("application/x-tex"));
        Assert.Equal("x + y", loaded.GetAnnotation("StarMath 5.0"));
    }

    /// <summary>
    /// 驗證 <see cref="OdfFormulaDocument.LoadFromLatex"/> 會自動附加原始 LaTeX 來源為
    /// <c>application/x-tex</c> 標註，使 <see cref="OdfFormulaDocument.ToLatex"/> 可精確還原
    /// 原始來源字串，而非僅 best-effort 由 MathML 重建。
    /// </summary>
    [Fact]
    public void ToLatex_AfterLoadFromLatex_ReturnsExactOriginalSource()
    {
        using OdfFormulaDocument formula = OdfFormulaDocument.FromLatex(@"\frac{a}{b} + \sqrt{c}");

        Assert.Equal(@"\frac{a}{b} + \sqrt{c}", formula.ToLatex());

        using var stream = new MemoryStream();
        formula.SaveToStream(stream);
        stream.Position = 0;

        using OdfFormulaDocument loaded = OdfFormulaDocument.Load(stream, "equation.odf");
        Assert.Equal(@"\frac{a}{b} + \sqrt{c}", loaded.ToLatex());
    }
}
