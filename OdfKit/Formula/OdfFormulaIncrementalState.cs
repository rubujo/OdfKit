using System.Collections.Generic;
using OdfKit.DOM;
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
        Dictionary<OdfCellAddress, object> values,
        Dictionary<OdfCellAddress, OdfNode> nodes,
        List<string> sheetNames,
        long mutationVersion)
    {
        Graph = graph;
        Formulas = formulas;
        Values = values;
        Nodes = nodes;
        SheetNames = sheetNames;
        MutationVersion = mutationVersion;
    }

    internal OdfFormulaDependencyGraph Graph { get; }

    internal Dictionary<OdfCellAddress, string> Formulas { get; }

    internal Dictionary<OdfCellAddress, object> Values { get; }

    internal Dictionary<OdfCellAddress, OdfNode> Nodes { get; }

    internal List<string> SheetNames { get; }

    internal long MutationVersion { get; }

    internal static OdfFormulaIncrementalState CaptureOwned(
        OdfFormulaDependencyGraph graph,
        OdfDomEvaluationContext context,
        long mutationVersion) =>
        new(
            graph,
            context.CellFormulas,
            context.CellValues,
            context.CellNodes,
            context.MutableSheetNames,
            mutationVersion);
}
