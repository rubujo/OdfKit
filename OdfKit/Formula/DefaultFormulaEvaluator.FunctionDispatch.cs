using System.Collections.Generic;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Function Dispatch

    /// <summary>
    /// 評估所有支援之公式函式的中央分派方法。
    /// </summary>
    internal static object EvaluateFunction(string name, List<AstNode> arguments, IEvaluationContext context)
        => FormulaBuiltinFunctionRegistry.Evaluate(name, arguments, context);

    #endregion
}
