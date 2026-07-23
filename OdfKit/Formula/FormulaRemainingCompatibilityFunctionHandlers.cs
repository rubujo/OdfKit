using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula compatibility handlers for remaining text, lookup, and mathematical functions.
/// OpenFormula 剩餘文字、查閱及數學相容函式處理常式。
/// </summary>
internal static class FormulaRemainingCompatibilityFunctionHandlers
{
    internal static object EvaluateAddress(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count is < 2 or > 5 ||
            !TryGetInteger(arguments[0], context, out long row, out error) ||
            !TryGetInteger(arguments[1], context, out long column, out error))
        {
            return error;
        }
        if (row < 1 || column < 1 || column > 16384)
            return OdfFormulaError.Value;

        long absolute = 1;
        if (arguments.Count >= 3 &&
            !TryGetInteger(arguments[2], context, out absolute, out error))
        {
            return error;
        }
        bool a1 = true;
        if (arguments.Count >= 4 &&
            !TryGetBoolean(arguments[3], context, out a1, out error))
        {
            return error;
        }
        string sheet = string.Empty;
        if (arguments.Count == 5 &&
            !TryGetText(arguments[4], context, out sheet, out error))
        {
            return error;
        }
        if (absolute is < 1 or > 4)
            return OdfFormulaError.Value;

        string address = a1
            ? CreateA1Address(row, column, absolute)
            : CreateR1C1Address(row, column, absolute);
        return string.IsNullOrEmpty(sheet)
            ? address
            : $"'{sheet.Replace("'", "''")}'!{address}";
    }

    internal static object EvaluateFixed(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count is < 1 or > 3 ||
            !TryGetNumber(arguments[0], context, out double number, out error))
        {
            return error;
        }
        long decimals = 2;
        if (arguments.Count >= 2 &&
            !TryGetInteger(arguments[1], context, out decimals, out error))
        {
            return error;
        }
        bool noThousands = false;
        if (arguments.Count == 3 &&
            !TryGetBoolean(arguments[2], context, out noThousands, out error))
        {
            return error;
        }
        if (decimals is < -15 or > 15)
            return OdfFormulaError.Value;

        double rounded = Math.Round(
            number,
            (int)Math.Max(0, decimals),
            MidpointRounding.AwayFromZero);
        if (decimals < 0)
        {
            double scale = Math.Pow(10, -decimals);
            rounded = Math.Round(number / scale, MidpointRounding.AwayFromZero) * scale;
        }
        string pattern = noThousands ? "0" : "#,##0";
        if (decimals > 0)
            pattern += "." + new string('0', (int)decimals);
        return rounded.ToString(pattern, CultureInfo.InvariantCulture);
    }

    internal static object EvaluateForecast(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count != 3 ||
            !TryGetNumber(arguments[0], context, out double x, out error) ||
            !TryGetNumberArrays(arguments[1], arguments[2], context, out double[] ys, out double[] xs, out error))
        {
            return error;
        }
        if (xs.Length != ys.Length || xs.Length == 0)
            return OdfFormulaError.NA;

        double meanX = Mean(xs);
        double meanY = Mean(ys);
        double numerator = 0;
        double denominator = 0;
        for (int index = 0; index < xs.Length; index++)
        {
            double dx = xs[index] - meanX;
            numerator += dx * (ys[index] - meanY);
            denominator += dx * dx;
        }
        return denominator == 0
            ? OdfFormulaError.Div0
            : meanY + ((numerator / denominator) * (x - meanX));
    }

    internal static object EvaluateMode(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, out double[] values, out object error))
            return error;
        var counts = new Dictionary<double, int>();
        double mode = 0;
        int bestCount = 1;
        foreach (double value in values)
        {
            counts.TryGetValue(value, out int count);
            count++;
            counts[value] = count;
            if (count > bestCount)
            {
                mode = value;
                bestCount = count;
            }
        }
        return bestCount > 1 ? mode : OdfFormulaError.NA;
    }

    internal static object EvaluatePermut(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count != 2 ||
            !TryGetInteger(arguments[0], context, out long number, out error) ||
            !TryGetInteger(arguments[1], context, out long chosen, out error))
        {
            return error;
        }
        if (number < 0 || chosen < 0 || chosen > number)
            return OdfFormulaError.Num;
        double result = 1;
        for (long value = number - chosen + 1; value <= number; value++)
            result *= value;
        return IsFinite(result) ? result : OdfFormulaError.Num;
    }

    internal static object EvaluateRoman(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count is < 1 or > 2 ||
            !TryGetInteger(arguments[0], context, out long number, out error))
        {
            return error;
        }
        long mode = 0;
        if (arguments.Count == 2 &&
            !TryGetInteger(arguments[1], context, out mode, out error))
        {
            return error;
        }
        if (number is < 0 or > 3999 || mode is < 0 or > 4)
            return OdfFormulaError.Value;
        if (number == 0)
            return string.Empty;

        (int Value, string Text)[] tokens =
        [
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        ];
        var result = new StringBuilder();
        foreach ((int value, string text) in tokens)
        {
            while (number >= value)
            {
                result.Append(text);
                number -= value;
            }
        }
        return result.ToString();
    }

    internal static object EvaluateSeriesSum(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count != 4 ||
            !TryGetNumber(arguments[0], context, out double x, out error) ||
            !TryGetNumber(arguments[1], context, out double initialPower, out error) ||
            !TryGetNumber(arguments[2], context, out double step, out error))
        {
            return error;
        }
        object coefficients = arguments[3].Evaluate(context);
        if (coefficients is OdfFormulaError formulaError)
            return formulaError;
        double result = 0;
        int index = 0;
        foreach (object value in FormulaCoercion.FlattenValues(coefficients))
        {
            if (FormulaCoercion.TryCoerceDouble(value, out double coefficient))
                result += coefficient * Math.Pow(x, initialPower + (index++ * step));
        }
        return IsFinite(result) ? result : OdfFormulaError.Num;
    }

    internal static object EvaluateAreas(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        List<OdfKit.Spreadsheet.OdfCellRange> ranges = arguments[0].GetRanges(context);
        return ranges.Count > 0 ? (double)ranges.Count : OdfFormulaError.Value;
    }

    internal static object EvaluateAsc(List<AstNode> arguments, IEvaluationContext context)
        => ConvertWidth(arguments, context, toNarrow: true);

    internal static object EvaluateJis(List<AstNode> arguments, IEvaluationContext context)
        => ConvertWidth(arguments, context, toNarrow: false);

    internal static object EvaluateFindB(List<AstNode> arguments, IEvaluationContext context)
        => FindByByte(arguments, context, ignoreCase: false);

    internal static object EvaluateSearchB(List<AstNode> arguments, IEvaluationContext context)
        => FindByByte(arguments, context, ignoreCase: true);

    internal static object EvaluateLeftB(List<AstNode> arguments, IEvaluationContext context)
        => SliceByByte(arguments, context, SliceKind.Left);

    internal static object EvaluateRightB(List<AstNode> arguments, IEvaluationContext context)
        => SliceByByte(arguments, context, SliceKind.Right);

    internal static object EvaluateMidB(List<AstNode> arguments, IEvaluationContext context)
        => SliceByByte(arguments, context, SliceKind.Middle);

    internal static object EvaluateLenB(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count != 1 ||
            !TryGetText(arguments[0], context, out string text, out error))
        {
            return error;
        }
        return (double)GetByteLength(text);
    }

    internal static object EvaluateReplaceB(List<AstNode> arguments, IEvaluationContext context)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count != 4 ||
            !TryGetText(arguments[0], context, out string text, out error) ||
            !TryGetInteger(arguments[1], context, out long start, out error) ||
            !TryGetInteger(arguments[2], context, out long count, out error) ||
            !TryGetText(arguments[3], context, out string replacement, out error))
        {
            return error;
        }
        if (start < 1 || count < 0)
            return OdfFormulaError.Value;
        int startIndex = GetCharacterIndexAtByte(text, start - 1);
        int endIndex = GetCharacterIndexAtByte(text, start - 1 + count);
        return text.Substring(0, startIndex) + replacement + text.Substring(endIndex);
    }

    private static object ConvertWidth(
        List<AstNode> arguments,
        IEvaluationContext context,
        bool toNarrow)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count != 1 ||
            !TryGetText(arguments[0], context, out string text, out error))
        {
            return error;
        }
        var result = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (toNarrow && character == '\u3000')
                result.Append(' ');
            else if (toNarrow && character is >= '\uFF01' and <= '\uFF5E')
                result.Append((char)(character - 0xFEE0));
            else if (!toNarrow && character == ' ')
                result.Append('\u3000');
            else if (!toNarrow && character is >= '\u0021' and <= '\u007E')
                result.Append((char)(character + 0xFEE0));
            else
                result.Append(character);
        }
        return result.ToString();
    }

    private static object FindByByte(
        List<AstNode> arguments,
        IEvaluationContext context,
        bool ignoreCase)
    {
        object error = OdfFormulaError.Value;
        if (arguments.Count is < 2 or > 3 ||
            !TryGetText(arguments[0], context, out string needle, out error) ||
            !TryGetText(arguments[1], context, out string haystack, out error))
        {
            return error;
        }
        long start = 1;
        if (arguments.Count == 3 &&
            !TryGetInteger(arguments[2], context, out start, out error))
        {
            return error;
        }
        if (start < 1)
            return OdfFormulaError.Value;
        int characterStart = GetCharacterIndexAtByte(haystack, start - 1);
        int found = haystack.IndexOf(
            needle,
            characterStart,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return found < 0
            ? OdfFormulaError.Value
            : (double)(GetByteLength(haystack.Substring(0, found)) + 1);
    }

    private static object SliceByByte(
        List<AstNode> arguments,
        IEvaluationContext context,
        SliceKind kind)
    {
        object error = OdfFormulaError.Value;
        int expected = kind == SliceKind.Middle ? 3 : 2;
        if (arguments.Count is < 1 || arguments.Count > expected ||
            !TryGetText(arguments[0], context, out string text, out error))
        {
            return error;
        }
        long start = kind == SliceKind.Middle ? 1 : 0;
        long count = kind == SliceKind.Middle ? 0 : 1;
        if (kind == SliceKind.Middle &&
            !TryGetInteger(arguments[1], context, out start, out error))
        {
            return error;
        }
        int countIndex = kind == SliceKind.Middle ? 2 : 1;
        if (arguments.Count > countIndex &&
            !TryGetInteger(arguments[countIndex], context, out count, out error))
        {
            return error;
        }
        if ((kind == SliceKind.Middle && start < 1) || count < 0)
            return OdfFormulaError.Value;

        int totalBytes = GetByteLength(text);
        long byteStart = kind switch
        {
            SliceKind.Left => 0,
            SliceKind.Right => Math.Max(0, totalBytes - count),
            _ => start - 1
        };
        int first = GetCharacterIndexAtByte(text, byteStart);
        int last = GetCharacterIndexAtByte(text, byteStart + count);
        return text.Substring(first, last - first);
    }

    private static string CreateA1Address(long row, long column, long absolute)
    {
        string columnName = string.Empty;
        for (long value = column; value > 0; value = (value - 1) / 26)
            columnName = (char)('A' + ((value - 1) % 26)) + columnName;
        bool absoluteRow = absolute is 1 or 2;
        bool absoluteColumn = absolute is 1 or 3;
        return (absoluteColumn ? "$" : string.Empty) +
            columnName +
            (absoluteRow ? "$" : string.Empty) +
            row.ToString(CultureInfo.InvariantCulture);
    }

    private static string CreateR1C1Address(long row, long column, long absolute)
    {
        bool absoluteRow = absolute is 1 or 2;
        bool absoluteColumn = absolute is 1 or 3;
        string rowText = absoluteRow
            ? row.ToString(CultureInfo.InvariantCulture)
            : $"[{row}]";
        string columnText = absoluteColumn
            ? column.ToString(CultureInfo.InvariantCulture)
            : $"[{column}]";
        return $"R{rowText}C{columnText}";
    }

    private static int GetByteLength(string text)
    {
        int length = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) &&
                index + 1 < text.Length &&
                char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }
            length += text[index] <= 0x7F ? 1 : 2;
        }
        return length;
    }

    private static int GetCharacterIndexAtByte(string text, long byteOffset)
    {
        if (byteOffset <= 0)
            return 0;
        int bytes = 0;
        int index = 0;
        while (index < text.Length)
        {
            int scalarLength = char.IsHighSurrogate(text[index]) &&
                index + 1 < text.Length &&
                char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;
            int width = text[index] <= 0x7F ? 1 : 2;
            if (bytes + width > byteOffset)
                break;
            bytes += width;
            index += scalarLength;
        }
        return index;
    }

    private static double Mean(double[] values)
    {
        double sum = 0;
        foreach (double value in values)
            sum += value;
        return sum / values.Length;
    }

    private static bool TryGetNumberArrays(
        AstNode first,
        AstNode second,
        IEvaluationContext context,
        out double[] firstValues,
        out double[] secondValues,
        out object error)
    {
        bool firstOk = TryGetNumbers(first.Evaluate(context), out firstValues, out error);
        bool secondOk = TryGetNumbers(second.Evaluate(context), out secondValues, out error);
        return firstOk && secondOk;
    }

    private static bool TryGetNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out double[] values,
        out object error)
    {
        var result = new List<double>();
        foreach (AstNode argument in arguments)
        {
            object value = argument.Evaluate(context);
            if (value is OdfFormulaError formulaError)
            {
                values = [];
                error = formulaError;
                return false;
            }
            foreach (object item in FormulaCoercion.FlattenValues(value))
            {
                if (FormulaCoercion.TryCoerceDouble(item, out double number))
                    result.Add(number);
            }
        }
        values = result.ToArray();
        error = values.Length == 0 ? OdfFormulaError.Value : 0d;
        return values.Length > 0;
    }

    private static bool TryGetNumbers(object value, out double[] values, out object error)
    {
        if (value is OdfFormulaError formulaError)
        {
            values = [];
            error = formulaError;
            return false;
        }
        var result = new List<double>();
        foreach (object item in FormulaCoercion.FlattenValues(value))
        {
            if (FormulaCoercion.TryCoerceDouble(item, out double number))
                result.Add(number);
        }
        values = result.ToArray();
        error = values.Length == 0 ? OdfFormulaError.Value : 0d;
        return values.Length > 0;
    }

    private static bool TryGetInteger(
        AstNode argument,
        IEvaluationContext context,
        out long value,
        out object error)
    {
        if (!TryGetNumber(argument, context, out double number, out error) ||
            number < long.MinValue ||
            number > long.MaxValue)
        {
            value = 0;
            return false;
        }
        value = (long)Math.Truncate(number);
        return true;
    }

    private static bool TryGetNumber(
        AstNode argument,
        IEvaluationContext context,
        out double value,
        out object error)
    {
        object result = argument.Evaluate(context);
        if (result is OdfFormulaError formulaError)
        {
            value = 0;
            error = formulaError;
            return false;
        }
        if (!FormulaCoercion.TryCoerceDouble(result, out value))
        {
            error = OdfFormulaError.Value;
            return false;
        }
        error = 0d;
        return true;
    }

    private static bool TryGetBoolean(
        AstNode argument,
        IEvaluationContext context,
        out bool value,
        out object error)
    {
        object result = argument.Evaluate(context);
        if (result is OdfFormulaError formulaError)
        {
            value = false;
            error = formulaError;
            return false;
        }
        if (!FormulaCoercion.TryCoerceToBool(result, out value))
        {
            error = OdfFormulaError.Value;
            return false;
        }
        error = false;
        return true;
    }

    private static bool TryGetText(
        AstNode argument,
        IEvaluationContext context,
        out string value,
        out object error)
    {
        object result = argument.Evaluate(context);
        if (result is OdfFormulaError formulaError)
        {
            value = string.Empty;
            error = formulaError;
            return false;
        }
        value = Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty;
        error = string.Empty;
        return true;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private enum SliceKind
    {
        Left,
        Middle,
        Right
    }
}
