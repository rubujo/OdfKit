using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula Large-group compatibility, engineering, and compact financial handlers.
/// OpenFormula Large Group 相容性、工程及精簡財務函式處理常式。
/// </summary>
internal static class FormulaLargeCompatibilityFunctionHandlers
{
    private const string Digits = "0123456789ABCDEF";

    internal static object EvaluateArabic(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetOneText(arguments, context, out string text, out object error))
            return error;
        if (text.Length == 0)
            return 0d;

        int total = 0;
        int previous = 0;
        foreach (char character in text.ToUpperInvariant())
        {
            int current = character switch
            {
                'I' => 1,
                'V' => 5,
                'X' => 10,
                'L' => 50,
                'C' => 100,
                'D' => 500,
                'M' => 1000,
                _ => 0
            };
            if (current == 0)
                return OdfFormulaError.Value;
            total += current > previous ? current - (2 * previous) : current;
            previous = current;
        }

        return (double)total;
    }

    internal static object EvaluateCombina(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetTwoIntegers(arguments, context, out long number, out long chosen, out object error))
            return error;
        if (number < 0 || chosen < 0)
            return OdfFormulaError.Num;
        return Combination(number + chosen - 1, chosen);
    }

    internal static object EvaluatePermutationA(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetTwoIntegers(arguments, context, out long number, out long chosen, out object error))
            return error;
        if (number < 0 || chosen < 0)
            return OdfFormulaError.Num;
        double result = Math.Pow(number, chosen);
        return IsFinite(result) ? result : OdfFormulaError.Num;
    }

    internal static object EvaluateGamma(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetOneNumber(arguments, context, out double number, out object error))
            return error;
        if (number <= 0 && number == Math.Truncate(number))
            return OdfFormulaError.Num;
        double result = Gamma(number);
        return IsFinite(result) ? result : OdfFormulaError.Num;
    }

    internal static object EvaluatePhi(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetOneNumber(arguments, context, out double number, out object error))
            return error;
        return Math.Exp(-0.5 * number * number) / Math.Sqrt(2 * Math.PI);
    }

    internal static object EvaluateGauss(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetOneNumber(arguments, context, out double number, out object error))
            return error;
        return (0.5 * (1 + Erf(number / Math.Sqrt(2)))) - 0.5;
    }

    internal static object EvaluateBinomialRange(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 3 or > 4)
            return OdfFormulaError.Value;
        if (!TryGetInteger(arguments[0], context, out long trials, out object trialsError))
            return trialsError;
        if (!TryGetNumber(arguments[1], context, out double probability, out object probabilityError))
            return probabilityError;
        if (!TryGetInteger(arguments[2], context, out long start, out object startError))
            return startError;
        long end = start;
        if (arguments.Count == 4 &&
            !TryGetInteger(arguments[3], context, out end, out object endError))
        {
            return endError;
        }
        if (trials < 0 ||
            probability is < 0 or > 1 ||
            start < 0 ||
            end < start ||
            end > trials)
        {
            return OdfFormulaError.Num;
        }

        double result = 0;
        for (long successes = start; successes <= end; successes++)
        {
            result += Combination(trials, successes) *
                Math.Pow(probability, successes) *
                Math.Pow(1 - probability, trials - successes);
        }

        return result;
    }

    internal static object EvaluateNumberValue(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 1 or > 3)
            return OdfFormulaError.Value;
        object value = arguments[0].Evaluate(context);
        if (value is OdfFormulaError formulaError)
            return formulaError;
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        string decimalSeparator = ".";
        string groupSeparator = ",";
        if (arguments.Count >= 2 &&
            !TryGetText(arguments[1], context, out decimalSeparator, out object decimalError))
        {
            return decimalError;
        }
        if (arguments.Count == 3 &&
            !TryGetText(arguments[2], context, out groupSeparator, out object groupError))
        {
            return groupError;
        }
        if (decimalSeparator.Length != 1 ||
            groupSeparator.Length > 1 ||
            decimalSeparator == groupSeparator)
        {
            return OdfFormulaError.Value;
        }

        if (groupSeparator.Length == 1)
            text = text.Replace(groupSeparator, string.Empty);
        if (decimalSeparator != ".")
            text = text.Replace(decimalSeparator, ".");
        return double.TryParse(
            text,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out double result)
            ? result
            : OdfFormulaError.Value;
    }

    internal static object EvaluateErrorType(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        return arguments[0].Evaluate(context) is OdfFormulaError error
            ? (double)error.ErrorType + 1
            : OdfFormulaError.NA;
    }

    internal static object EvaluateFvSchedule(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[0], context, out double principal, out object error))
            return error;
        object schedule = arguments[1].Evaluate(context);
        if (schedule is OdfFormulaError scheduleError)
            return scheduleError;
        foreach (object item in FormulaCoercion.FlattenValues(schedule))
        {
            if (!FormulaCoercion.TryCoerceDouble(item, out double rate))
                continue;
            principal *= 1 + rate;
        }

        return principal;
    }

    internal static object EvaluateIsPmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 4, out double[] values, out object error))
            return error;
        double rate = values[0];
        double period = Math.Truncate(values[1]);
        double periods = Math.Truncate(values[2]);
        if (periods == 0)
            return OdfFormulaError.Div0;
        return values[3] * rate * ((period / periods) - 1);
    }

    internal static object EvaluatePDuration(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, out double[] values, out object error))
            return error;
        if (values[0] <= 0 || values[1] <= 0 || values[2] <= 0)
            return OdfFormulaError.Num;
        return Math.Log(values[2] / values[1]) / Math.Log(1 + values[0]);
    }

    internal static object EvaluateRri(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, out double[] values, out object error))
            return error;
        if (values[0] <= 0 || values[1] == 0)
            return OdfFormulaError.Num;
        double result = Math.Pow(values[2] / values[1], 1 / values[0]) - 1;
        return IsFinite(result) ? result : OdfFormulaError.Num;
    }

    internal static object EvaluateBin2Dec(List<AstNode> arguments, IEvaluationContext context)
        => ConvertTextToDecimal(arguments, context, 2, 10);

    internal static object EvaluateHex2Dec(List<AstNode> arguments, IEvaluationContext context)
        => ConvertTextToDecimal(arguments, context, 16, 40);

    internal static object EvaluateOct2Dec(List<AstNode> arguments, IEvaluationContext context)
        => ConvertTextToDecimal(arguments, context, 8, 30);

    internal static object EvaluateBin2Hex(List<AstNode> arguments, IEvaluationContext context)
        => ConvertRadix(arguments, context, 2, 10, 16, 40);

    internal static object EvaluateBin2Oct(List<AstNode> arguments, IEvaluationContext context)
        => ConvertRadix(arguments, context, 2, 10, 8, 30);

    internal static object EvaluateHex2Bin(List<AstNode> arguments, IEvaluationContext context)
        => ConvertRadix(arguments, context, 16, 40, 2, 10);

    internal static object EvaluateHex2Oct(List<AstNode> arguments, IEvaluationContext context)
        => ConvertRadix(arguments, context, 16, 40, 8, 30);

    internal static object EvaluateOct2Bin(List<AstNode> arguments, IEvaluationContext context)
        => ConvertRadix(arguments, context, 8, 30, 2, 10);

    internal static object EvaluateOct2Hex(List<AstNode> arguments, IEvaluationContext context)
        => ConvertRadix(arguments, context, 8, 30, 16, 40);

    internal static object EvaluateDec2Bin(List<AstNode> arguments, IEvaluationContext context)
        => ConvertDecimal(arguments, context, 2, 10);

    internal static object EvaluateDec2Hex(List<AstNode> arguments, IEvaluationContext context)
        => ConvertDecimal(arguments, context, 16, 40);

    internal static object EvaluateDec2Oct(List<AstNode> arguments, IEvaluationContext context)
        => ConvertDecimal(arguments, context, 8, 30);

    private static object ConvertTextToDecimal(
        List<AstNode> arguments,
        IEvaluationContext context,
        int radix,
        int bitWidth)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        if (!TryGetText(arguments[0], context, out string text, out object error))
            return error;
        return TryParseSigned(text, radix, bitWidth, out long result)
            ? (double)result
            : OdfFormulaError.Num;
    }

    private static object ConvertRadix(
        List<AstNode> arguments,
        IEvaluationContext context,
        int sourceRadix,
        int sourceBits,
        int targetRadix,
        int targetBits)
    {
        if (arguments.Count is < 1 or > 2)
            return OdfFormulaError.Value;
        if (!TryGetText(arguments[0], context, out string text, out object error))
            return error;
        if (!TryParseSigned(text, sourceRadix, sourceBits, out long value))
            return OdfFormulaError.Num;
        return FormatSigned(value, targetRadix, targetBits, GetPlaces(arguments, context, value, out error), error);
    }

    private static object ConvertDecimal(
        List<AstNode> arguments,
        IEvaluationContext context,
        int radix,
        int bitWidth)
    {
        if (arguments.Count is < 1 or > 2)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[0], context, out double numeric, out object error))
        {
            return error;
        }
        if (numeric < long.MinValue || numeric > long.MaxValue)
            return OdfFormulaError.Num;
        long value = (long)Math.Truncate(numeric);
        return FormatSigned(value, radix, bitWidth, GetPlaces(arguments, context, value, out error), error);
    }

    private static int GetPlaces(
        List<AstNode> arguments,
        IEvaluationContext context,
        long value,
        out object error)
    {
        error = 0d;
        if (arguments.Count == 1 || value < 0)
            return 0;
        if (!TryGetInteger(arguments[1], context, out long places, out error) ||
            places is < 1 or > 10)
        {
            error = OdfFormulaError.Num;
            return -1;
        }

        return (int)places;
    }

    private static object FormatSigned(long value, int radix, int bitWidth, int places, object error)
    {
        if (places < 0)
            return error;
        long minimum = -(1L << (bitWidth - 1));
        long maximum = (1L << (bitWidth - 1)) - 1;
        if (value < minimum || value > maximum)
            return OdfFormulaError.Num;
        ulong encoded = value < 0
            ? (ulong)((1L << bitWidth) + value)
            : (ulong)value;
        string result = ConvertUnsigned(encoded, radix);
        if (places > 0)
        {
            if (result.Length > places)
                return OdfFormulaError.Num;
            result = result.PadLeft(places, '0');
        }

        return result;
    }

    private static bool TryParseSigned(string text, int radix, int bitWidth, out long value)
    {
        text = text.Trim();
        int maximumDigits = radix switch
        {
            2 => bitWidth,
            8 => bitWidth / 3,
            _ => bitWidth / 4
        };
        if (text.Length is 0 || text.Length > maximumDigits)
        {
            value = 0;
            return false;
        }

        ulong parsed = 0;
        foreach (char character in text)
        {
            int digit = Digits.IndexOf(char.ToUpperInvariant(character));
            if (digit < 0 || digit >= radix)
            {
                value = 0;
                return false;
            }
            parsed = (parsed * (uint)radix) + (uint)digit;
        }

        ulong signBit = 1UL << (bitWidth - 1);
        value = text.Length == maximumDigits && (parsed & signBit) != 0
            ? (long)(parsed - (1UL << bitWidth))
            : (long)parsed;
        return true;
    }

    private static string ConvertUnsigned(ulong value, int radix)
    {
        if (value == 0)
            return "0";
        Span<char> buffer = stackalloc char[64];
        int index = buffer.Length;
        while (value > 0)
        {
            buffer[--index] = Digits[(int)(value % (uint)radix)];
            value /= (uint)radix;
        }

#if NET10_0_OR_GREATER
        return new string(buffer[index..]);
#else
        return buffer.Slice(index).ToString();
#endif
    }

    private static double Combination(long number, long chosen)
    {
        if (chosen < 0 || chosen > number)
            return 0;
        chosen = Math.Min(chosen, number - chosen);
        double result = 1;
        for (long index = 1; index <= chosen; index++)
        {
            result *= (number - chosen + index) / (double)index;
        }

        return result;
    }

    private static double Gamma(double value)
    {
        double[] coefficients =
        [
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7
        ];
        if (value < 0.5)
            return Math.PI / (Math.Sin(Math.PI * value) * Gamma(1 - value));
        value--;
        double sum = 0.99999999999980993;
        for (int index = 0; index < coefficients.Length; index++)
        {
            sum += coefficients[index] / (value + index + 1);
        }
        double shifted = value + coefficients.Length - 0.5;
        return Math.Sqrt(2 * Math.PI) *
            Math.Pow(shifted, value + 0.5) *
            Math.Exp(-shifted) *
            sum;
    }

    private static double Erf(double value)
    {
        double sign = Math.Sign(value);
        double absolute = Math.Abs(value);
        double t = 1 / (1 + (0.3275911 * absolute));
        double polynomial = t *
            (0.254829592 +
                (t * (-0.284496736 +
                    (t * (1.421413741 +
                        (t * (-1.453152027 + (t * 1.061405429))))))));
        return sign * (1 - (polynomial * Math.Exp(-absolute * absolute)));
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool TryGetOneText(
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

        return TryGetText(arguments[0], context, out text, out error);
    }

    private static bool TryGetText(
        AstNode argument,
        IEvaluationContext context,
        out string text,
        out object error)
    {
        object value = argument.Evaluate(context);
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

    private static bool TryGetTwoIntegers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out long first,
        out long second,
        out object error)
    {
        error = OdfFormulaError.Value;
        if (arguments.Count != 2 ||
            !TryGetInteger(arguments[0], context, out first, out error) ||
            !TryGetInteger(arguments[1], context, out second, out error))
        {
            first = 0;
            second = 0;
            return false;
        }

        return true;
    }

    private static bool TryGetInteger(
        AstNode argument,
        IEvaluationContext context,
        out long number,
        out object error)
    {
        if (!TryGetNumber(argument, context, out double value, out error) ||
            value < long.MinValue ||
            value > long.MaxValue)
        {
            number = 0;
            return false;
        }
        number = (long)Math.Truncate(value);
        return true;
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

    private static bool TryGetNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        int count,
        out double[] values,
        out object error)
    {
        values = new double[count];
        if (arguments.Count != count)
        {
            error = OdfFormulaError.Value;
            return false;
        }
        for (int index = 0; index < count; index++)
        {
            if (!TryGetNumber(arguments[index], context, out values[index], out error))
                return false;
        }
        error = 0d;
        return true;
    }
}
