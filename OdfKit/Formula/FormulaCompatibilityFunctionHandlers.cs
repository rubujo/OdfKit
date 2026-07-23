using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula compatibility function handlers for text and radix conversion.
/// OpenFormula 文字與基數轉換相容函式處理常式。
/// </summary>
internal static class FormulaCompatibilityFunctionHandlers
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    internal static object EvaluateClean(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetText(arguments, context, out string text, out object error))
            return error;
        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (character >= 32)
                builder.Append(character);
        }

        return builder.ToString();
    }

    internal static object EvaluateUnicode(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetText(arguments, context, out string text, out object error))
            return error;
        if (text.Length == 0)
            return OdfFormulaError.Value;
        return (double)char.ConvertToUtf32(text, 0);
    }

    internal static object EvaluateUniChar(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetOneNumber(arguments, context, out double value, out object error))
            return error;
        int scalar = (int)Math.Truncate(value);
        if (scalar <= 0 ||
            scalar > 0x10FFFF ||
            scalar is >= 0xD800 and <= 0xDFFF)
        {
            return OdfFormulaError.Value;
        }

        return char.ConvertFromUtf32(scalar);
    }

    internal static object EvaluateBase(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 2 or > 3)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[0], context, out double numberValue, out object numberError))
            return numberError;
        if (!TryGetNumber(arguments[1], context, out double radixValue, out object radixError))
            return radixError;

        int radix = (int)Math.Truncate(radixValue);
        if (numberValue < 0 ||
            numberValue > long.MaxValue ||
            radix is < 2 or > 36)
        {
            return OdfFormulaError.Num;
        }

        int minimumLength = 0;
        if (arguments.Count == 3)
        {
            if (!TryGetNumber(arguments[2], context, out double lengthValue, out object lengthError))
                return lengthError;
            if (lengthValue < 0 || lengthValue > int.MaxValue)
                return OdfFormulaError.Num;
            minimumLength = (int)Math.Truncate(lengthValue);
        }

        long number = (long)Math.Truncate(numberValue);
        string converted = ConvertFromDecimal(number, radix);
        return converted.Length >= minimumLength
            ? converted
            : new string('0', minimumLength - converted.Length) + converted;
    }

    internal static object EvaluateDecimal(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        object textValue = arguments[0].Evaluate(context);
        if (textValue is OdfFormulaError textError)
            return textError;
        string text = textValue?.ToString()?.Trim() ?? string.Empty;
        if (!TryGetNumber(arguments[1], context, out double radixValue, out object radixError))
            return radixError;
        int radix = (int)Math.Truncate(radixValue);
        if (radix is < 2 or > 36 || text.Length == 0)
            return OdfFormulaError.Num;

        int index = 0;
        bool negative = text[0] == '-';
        if (negative || text[0] == '+')
            index++;
        if (index == text.Length)
            return OdfFormulaError.Num;

        double result = 0;
        for (; index < text.Length; index++)
        {
            int digit = Digits.IndexOf(char.ToUpperInvariant(text[index]));
            if (digit < 0 || digit >= radix)
                return OdfFormulaError.Num;
            result = (result * radix) + digit;
            if (double.IsInfinity(result))
                return OdfFormulaError.Num;
        }

        return negative ? -result : result;
    }

    private static bool TryGetText(
        List<AstNode> arguments,
        IEvaluationContext context,
        out string text,
        out object error)
    {
        if (arguments.Count != 1)
        {
            text = string.Empty;
            error = OdfFormulaError.Value;
            return false;
        }

        object value = arguments[0].Evaluate(context);
        if (value is OdfFormulaError formulaError)
        {
            text = string.Empty;
            error = formulaError;
            return false;
        }

        text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        error = string.Empty;
        return true;
    }

    private static bool TryGetOneNumber(
        List<AstNode> arguments,
        IEvaluationContext context,
        out double number,
        out object error)
    {
        if (arguments.Count != 1)
        {
            number = 0;
            error = OdfFormulaError.Value;
            return false;
        }

        return TryGetNumber(arguments[0], context, out number, out error);
    }

    private static bool TryGetNumber(
        AstNode argument,
        IEvaluationContext context,
        out double number,
        out object error)
    {
        object value = argument.Evaluate(context);
        if (value is OdfFormulaError formulaError)
        {
            number = 0;
            error = formulaError;
            return false;
        }
        if (!FormulaCoercion.TryCoerceDouble(value, out number))
        {
            error = OdfFormulaError.Value;
            return false;
        }

        error = 0d;
        return true;
    }

    private static string ConvertFromDecimal(long value, int radix)
    {
        if (value == 0)
            return "0";
        Span<char> buffer = stackalloc char[65];
        int index = buffer.Length;
        while (value > 0)
        {
            buffer[--index] = Digits[(int)(value % radix)];
            value /= radix;
        }

#if NET10_0_OR_GREATER
        return new string(buffer[index..]);
#else
        return buffer.Slice(index).ToString();
#endif
    }
}
