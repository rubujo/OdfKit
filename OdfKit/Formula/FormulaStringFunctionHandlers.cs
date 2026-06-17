using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 字串內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaStringFunctionHandlers
{


    internal static object EvaluateConcat(List<AstNode> arguments, IEvaluationContext context)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            foreach (var item in FormulaCoercion.FlattenValues(val))
            {
                sb.Append(item?.ToString() ?? "");
            }
        }
        return sb.ToString();
    }

    internal static object EvaluateLeft(List<AstNode> arguments, IEvaluationContext context)
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
            if (!FormulaCoercion.TryCoerceDouble(countVal, out double d) || d < 0)
                return OdfFormulaError.Value;
            count = (int)d;
        }

        string str = val.ToString() ?? "";
        return count >= str.Length ? str : str.Substring(0, count);
    }

    internal static object EvaluateRight(List<AstNode> arguments, IEvaluationContext context)
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
            if (!FormulaCoercion.TryCoerceDouble(countVal, out double d) || d < 0)
                return OdfFormulaError.Value;
            count = (int)d;
        }

        string str = val.ToString() ?? "";
        return count >= str.Length ? str : str.Substring(str.Length - count, count);
    }

    internal static object EvaluateMid(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        var startVal = arguments[1].Evaluate(context);
        var lenVal = arguments[2].Evaluate(context);

        if (!FormulaCoercion.TryCoerceDouble(startVal, out double startD) || !FormulaCoercion.TryCoerceDouble(lenVal, out double lenD))
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

    internal static object EvaluateSubstitute(List<AstNode> arguments, IEvaluationContext context)
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
            if (!FormulaCoercion.TryCoerceDouble(instVal, out double d) || d <= 0)
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

    internal static object EvaluateFind(List<AstNode> arguments, IEvaluationContext context)
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
            if (!FormulaCoercion.TryCoerceDouble(startVal, out double d) || d <= 0)
                return OdfFormulaError.Value;
            startIdx = (int)d;
        }

        if (startIdx > withinText.Length + 1)
            return OdfFormulaError.Value;
        int idx = withinText.IndexOf(findText, startIdx - 1, StringComparison.Ordinal);
        return idx == -1 ? OdfFormulaError.Value : (double)(idx + 1);
    }

    internal static object EvaluateSearch(List<AstNode> arguments, IEvaluationContext context)
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
            if (!FormulaCoercion.TryCoerceDouble(startVal, out double d) || d <= 0)
                return OdfFormulaError.Value;
            startIdx = (int)d;
        }

        if (startIdx > withinText.Length + 1)
            return OdfFormulaError.Value;
        int idx = withinText.IndexOf(findText, startIdx - 1, StringComparison.OrdinalIgnoreCase);
        return idx == -1 ? OdfFormulaError.Value : (double)(idx + 1);
    }

    internal static object EvaluateRept(List<AstNode> arguments, IEvaluationContext context)
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
        if (!FormulaCoercion.TryCoerceDouble(timesVal, out double d) || d < 0)
            return OdfFormulaError.Value;
        int times = (int)d;

        if (times == 0)
            return "";
        if (text.Length * times > 32767)
            return OdfFormulaError.Value;
        return string.Concat(Enumerable.Repeat(text, times));
    }

    internal static object EvaluateExact(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateCode(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateChar(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        if (!FormulaCoercion.TryCoerceDouble(val, out double d) || d < 1 || d > 255)
            return OdfFormulaError.Value;
        return ((char)(int)d).ToString();
    }

    internal static object EvaluateText(List<AstNode> arguments, IEvaluationContext context)
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



    internal static object EvaluateLen(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        return (double)str.Length;
    }

    internal static object EvaluateLower(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        return str.ToLowerInvariant();
    }

    internal static object EvaluateUpper(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        string str = val.ToString() ?? "";
        return str.ToUpperInvariant();
    }

    internal static object EvaluateTrim(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateReplace(List<AstNode> arguments, IEvaluationContext context)
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
}

