using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region String Functions


    private static object EvaluateConcat(List<AstNode> arguments, IEvaluationContext context)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            foreach (var item in FlattenValues(val))
            {
                sb.Append(item?.ToString() ?? "");
            }
        }
        return sb.ToString();
    }

    private static object EvaluateLeft(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        int count = 1;
        if (arguments.Count == 2)
        {
            var countVal = arguments[1].Evaluate(context);
            if (!TryCoerceDouble(countVal, out double d) || d < 0)
                return OdfFormulaError.Value;
            count = (int)d;
        }

        string str = val.ToString() ?? "";
        return count >= str.Length ? str : str.Substring(0, count);
    }

    private static object EvaluateRight(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        int count = 1;
        if (arguments.Count == 2)
        {
            var countVal = arguments[1].Evaluate(context);
            if (!TryCoerceDouble(countVal, out double d) || d < 0)
                return OdfFormulaError.Value;
            count = (int)d;
        }

        string str = val.ToString() ?? "";
        return count >= str.Length ? str : str.Substring(str.Length - count, count);
    }

    private static object EvaluateMid(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        var startVal = arguments[1].Evaluate(context);
        var lenVal = arguments[2].Evaluate(context);

        if (!TryCoerceDouble(startVal, out double startD) || !TryCoerceDouble(lenVal, out double lenD))
            return OdfFormulaError.Value;

        int start = (int)startD;
        int len = (int)lenD;

        if (start < 1 || len < 0)
            return OdfFormulaError.Value;

        string str = val.ToString() ?? "";
        int zeroIndexStart = start - 1;

        if (zeroIndexStart >= str.Length)
            return string.Empty;
        return zeroIndexStart + len >= str.Length ? str.Substring(zeroIndexStart) : str.Substring(zeroIndexStart, len);
    }

    private static object EvaluateSubstitute(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 4)
            return OdfFormulaError.Value;
        var textVal = arguments[0].Evaluate(context);
        var oldVal = arguments[1].Evaluate(context);
        var newVal = arguments[2].Evaluate(context);

        if (textVal is OdfFormulaError err1)
            return err1;
        if (oldVal is OdfFormulaError err2)
            return err2;
        if (newVal is OdfFormulaError err3)
            return err3;

        string text = textVal?.ToString() ?? "";
        string oldText = oldVal?.ToString() ?? "";
        string newText = newVal?.ToString() ?? "";

        if (arguments.Count == 4)
        {
            var instVal = arguments[3].Evaluate(context);
            if (!TryCoerceDouble(instVal, out double d) || d <= 0)
                return OdfFormulaError.Value;
            int instance = (int)d;

            int pos = -1;
            for (int i = 0; i < instance; i++)
            {
                pos = text.IndexOf(oldText, pos + 1, StringComparison.Ordinal);
                if (pos == -1)
                    break;
            }
            if (pos == -1)
                return text;
            return text.Substring(0, pos) + newText + text.Substring(pos + oldText.Length);
        }
        return text.Replace(oldText, newText);
    }

    private static object EvaluateFind(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var findVal = arguments[0].Evaluate(context);
        var withinVal = arguments[1].Evaluate(context);

        if (findVal is OdfFormulaError err1)
            return err1;
        if (withinVal is OdfFormulaError err2)
            return err2;

        string findText = findVal?.ToString() ?? "";
        string withinText = withinVal?.ToString() ?? "";

        int startIdx = 1;
        if (arguments.Count == 3)
        {
            var startVal = arguments[2].Evaluate(context);
            if (!TryCoerceDouble(startVal, out double d) || d <= 0)
                return OdfFormulaError.Value;
            startIdx = (int)d;
        }

        if (startIdx > withinText.Length + 1)
            return OdfFormulaError.Value;
        int idx = withinText.IndexOf(findText, startIdx - 1, StringComparison.Ordinal);
        return idx == -1 ? OdfFormulaError.Value : (double)(idx + 1);
    }

    private static object EvaluateSearch(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var findVal = arguments[0].Evaluate(context);
        var withinVal = arguments[1].Evaluate(context);

        if (findVal is OdfFormulaError err1)
            return err1;
        if (withinVal is OdfFormulaError err2)
            return err2;

        string findText = findVal?.ToString() ?? "";
        string withinText = withinVal?.ToString() ?? "";

        int startIdx = 1;
        if (arguments.Count == 3)
        {
            var startVal = arguments[2].Evaluate(context);
            if (!TryCoerceDouble(startVal, out double d) || d <= 0)
                return OdfFormulaError.Value;
            startIdx = (int)d;
        }

        if (startIdx > withinText.Length + 1)
            return OdfFormulaError.Value;
        int idx = withinText.IndexOf(findText, startIdx - 1, StringComparison.OrdinalIgnoreCase);
        return idx == -1 ? OdfFormulaError.Value : (double)(idx + 1);
    }

    private static object EvaluateRept(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var textVal = arguments[0].Evaluate(context);
        var timesVal = arguments[1].Evaluate(context);

        if (textVal is OdfFormulaError err1)
            return err1;
        if (timesVal is OdfFormulaError err2)
            return err2;

        string text = textVal?.ToString() ?? "";
        if (!TryCoerceDouble(timesVal, out double d) || d < 0)
            return OdfFormulaError.Value;
        int times = (int)d;

        if (times == 0)
            return "";
        if (text.Length * times > 32767)
            return OdfFormulaError.Value;
        return string.Concat(Enumerable.Repeat(text, times));
    }

    private static object EvaluateExact(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var t1 = arguments[0].Evaluate(context);
        var t2 = arguments[1].Evaluate(context);

        if (t1 is OdfFormulaError err1)
            return err1;
        if (t2 is OdfFormulaError err2)
            return err2;

        string s1 = t1?.ToString() ?? "";
        string s2 = t2?.ToString() ?? "";
        return string.Equals(s1, s2, StringComparison.Ordinal);
    }

    private static object EvaluateCode(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        string str = val?.ToString() ?? "";
        if (string.IsNullOrEmpty(str))
            return OdfFormulaError.Value;
        return (double)str[0];
    }

    private static object EvaluateChar(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        if (!TryCoerceDouble(val, out double d) || d < 1 || d > 255)
            return OdfFormulaError.Value;
        return ((char)(int)d).ToString();
    }

    private static object EvaluateText(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        var formatVal = arguments[1].Evaluate(context);

        if (val is OdfFormulaError err1)
            return err1;
        if (formatVal is OdfFormulaError err2)
            return err2;

        string format = formatVal?.ToString() ?? "";
        if (val is double d)
        {
            try
            {
                return d.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        return val?.ToString() ?? "";
    }


    #endregion
}
