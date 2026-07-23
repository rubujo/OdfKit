using System;
using System.Collections.Generic;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula extended mathematical function handlers.
/// OpenFormula 擴充數學函式處理常式。
/// </summary>
internal static class FormulaExtendedMathFunctionHandlers
{
    internal static object EvaluateAcosh(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value =>
            Math.Log(value + Math.Sqrt((value * value) - 1)), static value => value >= 1);

    internal static object EvaluateAsinh(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value =>
            Math.Log(value + Math.Sqrt((value * value) + 1)));

    internal static object EvaluateAtanh(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value =>
            0.5 * Math.Log((1 + value) / (1 - value)), static value => Math.Abs(value) < 1);

    internal static object EvaluateAcot(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value =>
        {
            if (value == 0)
                return Math.PI / 2;
            double result = Math.Atan(1 / value);
            return result < 0 ? result + Math.PI : result;
        });

    internal static object EvaluateAcoth(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value =>
            0.5 * Math.Log((value + 1) / (value - 1)), static value => Math.Abs(value) > 1);

    internal static object EvaluateCosh(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, Math.Cosh);

    internal static object EvaluateSinh(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, Math.Sinh);

    internal static object EvaluateTanh(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, Math.Tanh);

    internal static object EvaluateCot(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => 1 / Math.Tan(value),
            static value => Math.Abs(Math.Sin(value)) > double.Epsilon);

    internal static object EvaluateCoth(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => 1 / Math.Tanh(value),
            static value => value != 0);

    internal static object EvaluateCsc(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => 1 / Math.Sin(value),
            static value => Math.Abs(Math.Sin(value)) > double.Epsilon);

    internal static object EvaluateCsch(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => 1 / Math.Sinh(value),
            static value => value != 0);

    internal static object EvaluateSec(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => 1 / Math.Cos(value),
            static value => Math.Abs(Math.Cos(value)) > double.Epsilon);

    internal static object EvaluateSech(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => 1 / Math.Cosh(value));

    internal static object EvaluateSqrtPi(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateUnary(arguments, context, static value => Math.Sqrt(value * Math.PI),
            static value => value >= 0);

    internal static object EvaluateGcd(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNonnegativeIntegers(arguments, context, out List<long>? values, out object error))
            return error;
        long result = 0;
        foreach (long value in values)
        {
            result = GreatestCommonDivisor(result, value);
        }

        return (double)result;
    }

    internal static object EvaluateLcm(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNonnegativeIntegers(arguments, context, out List<long>? values, out object error))
            return error;
        long result = 1;
        foreach (long value in values)
        {
            if (value == 0)
                return 0d;
            try
            {
                result = checked((result / GreatestCommonDivisor(result, value)) * value);
            }
            catch (OverflowException)
            {
                return OdfFormulaError.Num;
            }
        }

        return (double)result;
    }

    internal static object EvaluateCombin(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetTwoIntegers(arguments, context, out long number, out long chosen, out object error))
            return error;
        if (number < 0 || chosen < 0 || chosen > number)
            return OdfFormulaError.Num;
        chosen = Math.Min(chosen, number - chosen);
        double result = 1;
        for (long index = 1; index <= chosen; index++)
        {
            result *= (number - chosen + index) / (double)index;
        }

        return result;
    }

    internal static object EvaluateMultinomial(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNonnegativeIntegers(arguments, context, out List<long>? values, out object error))
            return error;
        long total = 0;
        double denominator = 1;
        foreach (long value in values)
        {
            try
            {
                total = checked(total + value);
            }
            catch (OverflowException)
            {
                return OdfFormulaError.Num;
            }
            denominator *= Factorial(value);
        }

        double result = Factorial(total) / denominator;
        return double.IsInfinity(result) || double.IsNaN(result)
            ? OdfFormulaError.Num
            : result;
    }

    internal static object EvaluateQuotient(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetTwoNumbers(arguments, context, out double numerator, out double denominator, out object error))
            return error;
        if (denominator == 0)
            return OdfFormulaError.Div0;
        return Math.Truncate(numerator / denominator);
    }

    internal static object EvaluateFactDouble(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetInteger(arguments, context, out long number, out object error))
            return error;
        if (number < -1)
            return OdfFormulaError.Num;
        double result = 1;
        for (long value = number; value > 1; value -= 2)
        {
            result *= value;
        }

        return double.IsInfinity(result) ? OdfFormulaError.Num : result;
    }

    internal static object EvaluateDelta(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 1 or > 2)
            return OdfFormulaError.Value;
        object firstValue = arguments[0].Evaluate(context);
        if (firstValue is OdfFormulaError firstError)
            return firstError;
        if (!FormulaCoercion.TryCoerceDouble(firstValue, out double first))
            return OdfFormulaError.Value;
        double second = 0;
        if (arguments.Count == 2)
        {
            object secondValue = arguments[1].Evaluate(context);
            if (secondValue is OdfFormulaError secondError)
                return secondError;
            if (!FormulaCoercion.TryCoerceDouble(secondValue, out second))
                return OdfFormulaError.Value;
        }

        return first == second ? 1d : 0d;
    }

    internal static object EvaluateGeStep(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 1 or > 2)
            return OdfFormulaError.Value;
        object firstValue = arguments[0].Evaluate(context);
        if (firstValue is OdfFormulaError firstError)
            return firstError;
        if (!FormulaCoercion.TryCoerceDouble(firstValue, out double first))
            return OdfFormulaError.Value;
        double step = 0;
        if (arguments.Count == 2)
        {
            object stepValue = arguments[1].Evaluate(context);
            if (stepValue is OdfFormulaError stepError)
                return stepError;
            if (!FormulaCoercion.TryCoerceDouble(stepValue, out step))
                return OdfFormulaError.Value;
        }

        return first >= step ? 1d : 0d;
    }

    private static object EvaluateUnary(
        List<AstNode> arguments,
        IEvaluationContext context,
        Func<double, double> operation,
        Func<double, bool>? domain = null)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        object value = arguments[0].Evaluate(context);
        if (value is OdfFormulaError error)
            return error;
        if (!FormulaCoercion.TryCoerceDouble(value, out double number))
            return OdfFormulaError.Value;
        if (domain is not null && !domain(number))
            return OdfFormulaError.Num;
        double result = operation(number);
        return double.IsNaN(result) || double.IsInfinity(result)
            ? OdfFormulaError.Num
            : result;
    }

    private static bool TryGetInteger(
        List<AstNode> arguments,
        IEvaluationContext context,
        out long number,
        out object error)
    {
        if (arguments.Count != 1)
        {
            number = 0;
            error = OdfFormulaError.Value;
            return false;
        }

        object value = arguments[0].Evaluate(context);
        if (value is OdfFormulaError formulaError)
        {
            number = 0;
            error = formulaError;
            return false;
        }
        if (!FormulaCoercion.TryCoerceDouble(value, out double numeric) ||
            numeric < long.MinValue ||
            numeric > long.MaxValue)
        {
            number = 0;
            error = OdfFormulaError.Value;
            return false;
        }

        number = (long)Math.Truncate(numeric);
        error = 0d;
        return true;
    }

    private static bool TryGetTwoIntegers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out long first,
        out long second,
        out object error)
    {
        if (arguments.Count != 2)
        {
            first = 0;
            second = 0;
            error = OdfFormulaError.Value;
            return false;
        }

        object firstValue = arguments[0].Evaluate(context);
        object secondValue = arguments[1].Evaluate(context);
        if (firstValue is OdfFormulaError firstError)
        {
            first = 0;
            second = 0;
            error = firstError;
            return false;
        }
        if (secondValue is OdfFormulaError secondError)
        {
            first = 0;
            second = 0;
            error = secondError;
            return false;
        }
        if (!FormulaCoercion.TryCoerceDouble(firstValue, out double firstNumber) ||
            !FormulaCoercion.TryCoerceDouble(secondValue, out double secondNumber) ||
            firstNumber < long.MinValue ||
            firstNumber > long.MaxValue ||
            secondNumber < long.MinValue ||
            secondNumber > long.MaxValue)
        {
            first = 0;
            second = 0;
            error = OdfFormulaError.Value;
            return false;
        }

        first = (long)Math.Truncate(firstNumber);
        second = (long)Math.Truncate(secondNumber);
        error = 0d;
        return true;
    }

    private static bool TryGetTwoNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out double first,
        out double second,
        out object error)
    {
        if (arguments.Count != 2)
        {
            first = 0;
            second = 0;
            error = OdfFormulaError.Value;
            return false;
        }

        object firstValue = arguments[0].Evaluate(context);
        object secondValue = arguments[1].Evaluate(context);
        if (firstValue is OdfFormulaError firstError)
        {
            first = 0;
            second = 0;
            error = firstError;
            return false;
        }
        if (secondValue is OdfFormulaError secondError)
        {
            first = 0;
            second = 0;
            error = secondError;
            return false;
        }
        first = 0;
        second = 0;
        if (!FormulaCoercion.TryCoerceDouble(firstValue, out first) ||
            !FormulaCoercion.TryCoerceDouble(secondValue, out second))
        {
            error = OdfFormulaError.Value;
            return false;
        }

        error = 0d;
        return true;
    }

    private static bool TryGetNonnegativeIntegers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out List<long> values,
        out object error)
    {
        values = [];
        if (arguments.Count == 0)
        {
            error = OdfFormulaError.Value;
            return false;
        }

        foreach (AstNode argument in arguments)
        {
            object argumentValue = argument.Evaluate(context);
            if (argumentValue is OdfFormulaError formulaError)
            {
                error = formulaError;
                return false;
            }

            foreach (object value in FormulaCoercion.FlattenValues(argumentValue))
            {
                if (!FormulaCoercion.TryCoerceDouble(value, out double number) ||
                    number < 0 ||
                    number > long.MaxValue)
                {
                    error = OdfFormulaError.Num;
                    return false;
                }

                values.Add((long)Math.Truncate(number));
            }
        }

        error = 0d;
        return true;
    }

    private static long GreatestCommonDivisor(long first, long second)
    {
        while (second != 0)
        {
            long remainder = first % second;
            first = second;
            second = remainder;
        }

        return Math.Abs(first);
    }

    private static double Factorial(long number)
    {
        double result = 1;
        for (long value = 2; value <= number; value++)
        {
            result *= value;
        }

        return result;
    }
}
