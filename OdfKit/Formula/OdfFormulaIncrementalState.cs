using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 儲存單一增量公式工作階段的不可公開狀態。
/// </summary>
internal sealed class OdfFormulaIncrementalState
{
    private OdfFormulaIncrementalState(
        OdfFormulaDependencyGraph graph,
        Dictionary<OdfCellAddress, string> formulas,
        Dictionary<OdfCellAddress, object> values)
    {
        Graph = graph;
        Formulas = formulas;
        Values = values;
    }

    internal OdfFormulaDependencyGraph Graph { get; }

    internal Dictionary<OdfCellAddress, string> Formulas { get; }

    internal Dictionary<OdfCellAddress, object> Values { get; }

    internal static OdfFormulaIncrementalState Capture(
        OdfFormulaDependencyGraph graph,
        OdfDomEvaluationContext context) =>
        new(
            graph,
            new Dictionary<OdfCellAddress, string>(context.CellFormulas),
            new Dictionary<OdfCellAddress, object>(context.CellValues));
}
