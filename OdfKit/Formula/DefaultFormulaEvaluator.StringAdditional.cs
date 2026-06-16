using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Additional String Functions


    private static object EvaluateLen(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        return (double)str.Length;
    }

    private static object EvaluateLower(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        return str.ToLowerInvariant();
    }

    private static object EvaluateUpper(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        return str.ToUpperInvariant();
    }

    private static object EvaluateTrim(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        str = str.Trim();
        var sb = new System.Text.StringBuilder();
        bool lastWasSpace = false;
        foreach (char c in str)
        {
            if (c == ' ')
            {
                if (!lastWasSpace)
                {
                    sb.Append(c);
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    private static object EvaluateReplace(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 4)
            return OdfFormulaError.Value;
        var valText = arguments[0].Evaluate(context);
        var valStart = arguments[1].Evaluate(context);
        var valLen = arguments[2].Evaluate(context);
        var valNewText = arguments[3].Evaluate(context);

        if (valText is OdfFormulaError err1)
            return err1;
        if (valStart is OdfFormulaError err2)
            return err2;
        if (valLen is OdfFormulaError err3)
            return err3;
        if (valNewText is OdfFormulaError err4)
            return err4;

        string oldText = valText.ToString() ?? "";
        if (!FormulaCoercion.TryCoerceDouble(valStart, out double startD) || !FormulaCoercion.TryCoerceDouble(valLen, out double lenD))
            return OdfFormulaError.Value;

        int startIndex = (int)startD - 1;
        int len = (int)lenD;
        string newText = valNewText.ToString() ?? "";

        if (startIndex < 0 || len < 0)
            return OdfFormulaError.Value;
        if (startIndex > oldText.Length)
            startIndex = oldText.Length;
        int end = startIndex + len;
        if (end > oldText.Length)
            end = oldText.Length;

        string part1 = oldText.Substring(0, startIndex);
        string part2 = oldText.Substring(end);
        return part1 + newText + part2;
    }


    #endregion
}
