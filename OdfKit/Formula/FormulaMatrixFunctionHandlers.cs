using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 矩陣內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaMatrixFunctionHandlers
{


    internal static object EvaluateTranspose(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        object val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        if (val is object[,] arr)
        {
            int rows = arr.GetLength(0);
            int cols = arr.GetLength(1);
            var result = new object[cols, rows];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                    result[c, r] = arr[r, c];
            }

            return result;
        }

        var scalar = new object[1, 1];
        scalar[0, 0] = val;
        return scalar;
    }
}

