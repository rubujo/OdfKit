using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula complex-number function handlers.
/// OpenFormula 複數函式處理常式。
/// </summary>
internal static class FormulaComplexFunctionHandlers
{
    internal static object EvaluateComplex(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 2 or > 3)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[0], context, out double real, out object realError))
            return realError;
        if (!TryGetNumber(arguments[1], context, out double imaginary, out object imaginaryError))
            return imaginaryError;

        char suffix = 'i';
        if (arguments.Count == 3)
        {
            object suffixValue = arguments[2].Evaluate(context);
            if (suffixValue is OdfFormulaError suffixError)
                return suffixError;
            string suffixText = suffixValue?.ToString() ?? string.Empty;
            if (suffixText.Length != 1 || (suffixText[0] != 'i' && suffixText[0] != 'j'))
                return OdfFormulaError.Value;
            suffix = suffixText[0];
        }

        return Format(new Complex(real, imaginary), suffix);
    }

    internal static object EvaluateImAbs(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryReal(arguments, context, static value => value.Magnitude);

    internal static object EvaluateImaginary(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryReal(arguments, context, static value => value.Imaginary);

    internal static object EvaluateImArgument(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetOne(arguments, context, out Complex value, out _, out object error))
            return error;
        return value == Complex.Zero ? OdfFormulaError.Div0 : value.Phase;
    }

    internal static object EvaluateImConjugate(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Conjugate);

    internal static object EvaluateImCos(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Cos);

    internal static object EvaluateImCot(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, static value => 1 / Complex.Tan(value));

    internal static object EvaluateImCsc(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, static value => 1 / Complex.Sin(value));

    internal static object EvaluateImCsch(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, static value => 1 / Complex.Sinh(value));

    internal static object EvaluateImExp(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Exp);

    internal static object EvaluateImLn(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Log);

    internal static object EvaluateImLog10(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Log10);

    internal static object EvaluateImLog2(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, static value => Complex.Log(value, 2));

    internal static object EvaluateImReal(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryReal(arguments, context, static value => value.Real);

    internal static object EvaluateImSec(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, static value => 1 / Complex.Cos(value));

    internal static object EvaluateImSech(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, static value => 1 / Complex.Cosh(value));

    internal static object EvaluateImSin(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Sin);

    internal static object EvaluateImSqrt(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Sqrt);

    internal static object EvaluateImTan(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnaryComplex(arguments, context, Complex.Tan);

    internal static object EvaluateImDiv(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateBinary(arguments, context, static (left, right) => left / right, rejectZeroRight: true);

    internal static object EvaluateImPower(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateBinary(arguments, context, Complex.Pow);

    internal static object EvaluateImSub(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateBinary(arguments, context, static (left, right) => left - right);

    internal static object EvaluateImSum(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateAggregate(arguments, context, Complex.Zero, static (left, right) => left + right);

    internal static object EvaluateImProduct(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateAggregate(arguments, context, Complex.One, static (left, right) => left * right);

    private static object EvaluateUnaryReal(
        List<AstNode> arguments,
        IEvaluationContext context,
        Func<Complex, double> operation)
    {
        if (!TryGetOne(arguments, context, out Complex value, out _, out object error))
            return error;
        double result = operation(value);
        return double.IsNaN(result) || double.IsInfinity(result)
            ? OdfFormulaError.Num
            : result;
    }

    private static object EvaluateUnaryComplex(
        List<AstNode> arguments,
        IEvaluationContext context,
        Func<Complex, Complex> operation)
    {
        if (!TryGetOne(arguments, context, out Complex value, out char suffix, out object error))
            return error;
        Complex result = operation(value);
        return IsFinite(result) ? Format(result, suffix) : OdfFormulaError.Num;
    }

    private static object EvaluateBinary(
        List<AstNode> arguments,
        IEvaluationContext context,
        Func<Complex, Complex, Complex> operation,
        bool rejectZeroRight = false)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        if (!TryGetComplex(arguments[0].Evaluate(context), out Complex left, out char suffix, out object leftError))
            return leftError;
        if (!TryGetComplex(arguments[1].Evaluate(context), out Complex right, out _, out object rightError))
            return rightError;
        if (rejectZeroRight && right == Complex.Zero)
            return OdfFormulaError.Num;
        Complex result = operation(left, right);
        return IsFinite(result) ? Format(result, suffix) : OdfFormulaError.Num;
    }

    private static object EvaluateAggregate(
        List<AstNode> arguments,
        IEvaluationContext context,
        Complex seed,
        Func<Complex, Complex, Complex> operation)
    {
        if (arguments.Count == 0)
            return OdfFormulaError.Value;
        Complex result = seed;
        char suffix = 'i';
        bool hasValue = false;
        foreach (AstNode argument in arguments)
        {
            object argumentValue = argument.Evaluate(context);
            if (argumentValue is OdfFormulaError argumentError)
                return argumentError;
            foreach (object value in FormulaCoercion.FlattenValues(argumentValue))
            {
                if (!TryGetComplex(value, out Complex complex, out char valueSuffix, out object error))
                    return error;
                if (!hasValue)
                    suffix = valueSuffix;
                result = operation(result, complex);
                hasValue = true;
            }
        }

        return hasValue && IsFinite(result) ? Format(result, suffix) : OdfFormulaError.Value;
    }

    private static bool TryGetOne(
        List<AstNode> arguments,
        IEvaluationContext context,
        out Complex value,
        out char suffix,
        out object error)
    {
        if (arguments.Count != 1)
        {
            value = Complex.Zero;
            suffix = 'i';
            error = OdfFormulaError.Value;
            return false;
        }

        return TryGetComplex(arguments[0].Evaluate(context), out value, out suffix, out error);
    }

    private static bool TryGetComplex(
        object value,
        out Complex complex,
        out char suffix,
        out object error)
    {
        if (value is OdfFormulaError formulaError)
        {
            complex = Complex.Zero;
            suffix = 'i';
            error = formulaError;
            return false;
        }
        if (FormulaCoercion.TryCoerceDouble(value, out double number))
        {
            complex = new Complex(number, 0);
            suffix = 'i';
            error = 0d;
            return true;
        }
        if (value is not string text || !TryParse(text, out complex, out suffix))
        {
            complex = Complex.Zero;
            suffix = 'i';
            error = OdfFormulaError.Value;
            return false;
        }

        error = 0d;
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

    private static bool TryParse(string text, out Complex value, out char suffix)
    {
        string normalized = text.Trim();
        suffix = 'i';
        bool hasSuffix = normalized.EndsWith("i", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("j", StringComparison.OrdinalIgnoreCase);
        if (!hasSuffix)
        {
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
            {
                value = new Complex(real, 0);
                return true;
            }

            value = Complex.Zero;
            return false;
        }

        suffix = char.ToLowerInvariant(normalized[normalized.Length - 1]);
        normalized = normalized.Substring(0, normalized.Length - 1);
        int separatorIndex = FindImaginarySeparator(normalized);
        string realText = separatorIndex > 0 ? normalized.Substring(0, separatorIndex) : "0";
        string imaginaryText = separatorIndex > 0 ? normalized.Substring(separatorIndex) : normalized;
        if (imaginaryText is "" or "+")
            imaginaryText = "1";
        else if (imaginaryText == "-")
            imaginaryText = "-1";

        if (!double.TryParse(realText, NumberStyles.Float, CultureInfo.InvariantCulture, out double realPart) ||
            !double.TryParse(imaginaryText, NumberStyles.Float, CultureInfo.InvariantCulture, out double imaginaryPart))
        {
            value = Complex.Zero;
            return false;
        }

        value = new Complex(realPart, imaginaryPart);
        return true;
    }

    private static int FindImaginarySeparator(string text)
    {
        for (int index = text.Length - 1; index > 0; index--)
        {
            char current = text[index];
            if ((current == '+' || current == '-') &&
                text[index - 1] is not 'e' and not 'E')
            {
                return index;
            }
        }

        return -1;
    }

    private static string Format(Complex value, char suffix)
    {
        double real = NormalizeZero(value.Real);
        double imaginary = NormalizeZero(value.Imaginary);
        if (imaginary == 0)
            return real.ToString("G15", CultureInfo.InvariantCulture);

        string imaginaryText = Math.Abs(imaginary) == 1
            ? string.Empty
            : Math.Abs(imaginary).ToString("G15", CultureInfo.InvariantCulture);
        string sign = imaginary < 0 ? "-" : "+";
        if (real == 0)
            return (imaginary < 0 ? "-" : string.Empty) + imaginaryText + suffix;
        return real.ToString("G15", CultureInfo.InvariantCulture) + sign + imaginaryText + suffix;
    }

    private static bool IsFinite(Complex value)
        => !double.IsNaN(value.Real) &&
            !double.IsInfinity(value.Real) &&
            !double.IsNaN(value.Imaginary) &&
            !double.IsInfinity(value.Imaginary);

    private static double NormalizeZero(double value)
        => Math.Abs(value) < 1e-14 ? 0 : value;
}
