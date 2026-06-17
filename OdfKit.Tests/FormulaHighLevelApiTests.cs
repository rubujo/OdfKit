using System;
using System.IO;
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
        Assert.Contains("<msup>", formulaDoc.GetMathML());

        // 驗證 SetMathML
        string newMathml = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi><mo>+</mo><mi>y</mi></math>";
        formulaDoc.SetMathML(newMathml);
        Assert.Equal("x+y", formulaDoc.MathText);
        Assert.Contains("<mi>x</mi>", formulaDoc.GetMathML());
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
        Assert.Contains("<mi>F</mi>", loadedDoc.GetMathML());
    }
}
