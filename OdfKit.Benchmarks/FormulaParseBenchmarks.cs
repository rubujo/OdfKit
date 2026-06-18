using BenchmarkDotNet.Attributes;
using OdfKit.Formula;

namespace OdfKit.Benchmarks;

/// <summary>
/// 公式剖析器（<see cref="FormulaParser"/> ref struct）效能基準。
/// </summary>
[MemoryDiagnoser]
public class FormulaParseBenchmarks
{
    private const string SimpleFormula = "SUM(A1:A100)";
    private const string ComplexFormula = "IF(AND(A1>0,B1<>\"\"),SUM((A1~B1),C1),IF((A1>0),1,0))";

    [Benchmark(Baseline = true)]
    public void ParseSimpleFormula()
    {
        var parser = new FormulaParser(SimpleFormula);
        _ = parser.Parse();
    }

    [Benchmark]
    public void ParseComplexFormula()
    {
        var parser = new FormulaParser(ComplexFormula);
        _ = parser.Parse();
    }
}
