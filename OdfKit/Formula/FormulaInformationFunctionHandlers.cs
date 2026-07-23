using System;
using System.Collections.Generic;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 資訊與型別轉換公式函式處理常式。
/// </summary>
internal static class FormulaInformationFunctionHandlers
{
    internal static object EvaluateIsErr(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;

        return arguments[0].Evaluate(context) is OdfFormulaError error &&
            error.ErrorType != OdfFormulaErrorType.NA;
    }

    internal static object EvaluateN(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;

        object value = arguments[0].Evaluate(context);
        return value switch
        {
            OdfFormulaError error => error,
            double number => number,
            bool logical => logical ? 1d : 0d,
            DateTime dateTime => (dateTime - new DateTime(1899, 12, 30)).TotalDays,
            _ => 0d
        };
    }

    internal static object EvaluateT(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;

        object value = arguments[0].Evaluate(context);
        return value switch
        {
            OdfFormulaError error => error,
            string text => text,
            _ => string.Empty
        };
    }

    internal static object EvaluateValue(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;

        object value = arguments[0].Evaluate(context);
        if (value is OdfFormulaError error)
            return error;
        return FormulaCoercion.TryCoerceDouble(value, out double number)
            ? number
            : OdfFormulaError.Value;
    }
}
