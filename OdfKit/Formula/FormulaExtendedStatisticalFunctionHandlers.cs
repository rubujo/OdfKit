using System;
using System.Collections.Generic;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula extended statistical function handlers.
/// OpenFormula 擴充統計函式處理常式。
/// </summary>
internal static class FormulaExtendedStatisticalFunctionHandlers
{
    internal static object EvaluateAveDev(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, out List<double>? numbers, out object error))
            return error;
        double mean = Mean(numbers);
        double total = 0;
        foreach (double number in numbers)
        {
            total += Math.Abs(number - mean);
        }

        return total / numbers.Count;
    }

    internal static object EvaluateCorrel(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetPairs(arguments, context, out List<double>? left, out List<double>? right, out object error))
            return error;
        double leftMean = Mean(left);
        double rightMean = Mean(right);
        double numerator = 0;
        double leftSquares = 0;
        double rightSquares = 0;
        for (int index = 0; index < left.Count; index++)
        {
            double leftDifference = left[index] - leftMean;
            double rightDifference = right[index] - rightMean;
            numerator += leftDifference * rightDifference;
            leftSquares += leftDifference * leftDifference;
            rightSquares += rightDifference * rightDifference;
        }

        double denominator = Math.Sqrt(leftSquares * rightSquares);
        return denominator == 0 ? OdfFormulaError.Div0 : numerator / denominator;
    }

    internal static object EvaluateCovar(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetPairs(arguments, context, out List<double>? left, out List<double>? right, out object error))
            return error;
        double leftMean = Mean(left);
        double rightMean = Mean(right);
        double total = 0;
        for (int index = 0; index < left.Count; index++)
        {
            total += (left[index] - leftMean) * (right[index] - rightMean);
        }

        return total / left.Count;
    }

    internal static object EvaluateDevSq(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, out List<double>? numbers, out object error))
            return error;
        double mean = Mean(numbers);
        double total = 0;
        foreach (double number in numbers)
        {
            double difference = number - mean;
            total += difference * difference;
        }

        return total;
    }

    internal static object EvaluateGeoMean(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, out List<double>? numbers, out object error))
            return error;
        double logarithmSum = 0;
        foreach (double number in numbers)
        {
            if (number <= 0)
                return OdfFormulaError.Num;
            logarithmSum += Math.Log(number);
        }

        return Math.Exp(logarithmSum / numbers.Count);
    }

    internal static object EvaluateHarMean(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, out List<double>? numbers, out object error))
            return error;
        double reciprocalSum = 0;
        foreach (double number in numbers)
        {
            if (number <= 0)
                return OdfFormulaError.Num;
            reciprocalSum += 1 / number;
        }

        return numbers.Count / reciprocalSum;
    }

    internal static object EvaluateIntercept(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetPairs(arguments, context, out List<double>? knownY, out List<double>? knownX, out object error))
            return error;
        object slope = CalculateSlope(knownY, knownX);
        return slope is double slopeValue
            ? Mean(knownY) - (slopeValue * Mean(knownX))
            : slope;
    }

    internal static object EvaluateSlope(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetPairs(arguments, context, out List<double>? knownY, out List<double>? knownX, out object error))
            return error;
        return CalculateSlope(knownY, knownX);
    }

    internal static object EvaluateRsq(List<AstNode> arguments, IEvaluationContext context)
    {
        object correlation = EvaluateCorrel(arguments, context);
        return correlation is double number ? number * number : correlation;
    }

    internal static object EvaluateStandardize(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetThreeNumbers(arguments, context, out double value, out double mean, out double standardDeviation, out object error))
            return error;
        return standardDeviation <= 0
            ? OdfFormulaError.Num
            : (value - mean) / standardDeviation;
    }

    internal static object EvaluateSumSq(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, out List<double>? numbers, out object error))
            return error;
        double total = 0;
        foreach (double number in numbers)
        {
            total += number * number;
        }

        return total;
    }

    internal static object EvaluateSumX2MY2(List<AstNode> arguments, IEvaluationContext context)
        => EvaluatePairSum(arguments, context, static (left, right) => (left * left) - (right * right));

    internal static object EvaluateSumX2PY2(List<AstNode> arguments, IEvaluationContext context)
        => EvaluatePairSum(arguments, context, static (left, right) => (left * left) + (right * right));

    internal static object EvaluateSumXMY2(List<AstNode> arguments, IEvaluationContext context)
        => EvaluatePairSum(arguments, context, static (left, right) =>
        {
            double difference = left - right;
            return difference * difference;
        });

    internal static object EvaluateAverageA(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetAValues(arguments, context, out List<double>? numbers, out object error))
            return error;
        return Mean(numbers);
    }

    internal static object EvaluateMaxA(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetAValues(arguments, context, out List<double>? numbers, out object error))
            return error;
        double result = numbers[0];
        foreach (double number in numbers)
        {
            result = Math.Max(result, number);
        }

        return result;
    }

    internal static object EvaluateMinA(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetAValues(arguments, context, out List<double>? numbers, out object error))
            return error;
        double result = numbers[0];
        foreach (double number in numbers)
        {
            result = Math.Min(result, number);
        }

        return result;
    }

    internal static object EvaluateVarA(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateAVariance(arguments, context, sample: true, squareRoot: false);

    internal static object EvaluateVarPA(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateAVariance(arguments, context, sample: false, squareRoot: false);

    internal static object EvaluateStDevA(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateAVariance(arguments, context, sample: true, squareRoot: true);

    internal static object EvaluateStDevPA(List<AstNode> arguments, IEvaluationContext context)
        => EvaluateAVariance(arguments, context, sample: false, squareRoot: true);

    private static object EvaluateAVariance(
        List<AstNode> arguments,
        IEvaluationContext context,
        bool sample,
        bool squareRoot)
    {
        if (!TryGetAValues(arguments, context, out List<double>? numbers, out object error))
            return error;
        int divisor = sample ? numbers.Count - 1 : numbers.Count;
        if (divisor <= 0)
            return OdfFormulaError.Div0;
        double mean = Mean(numbers);
        double total = 0;
        foreach (double number in numbers)
        {
            double difference = number - mean;
            total += difference * difference;
        }

        double variance = total / divisor;
        return squareRoot ? Math.Sqrt(variance) : variance;
    }

    private static object EvaluatePairSum(
        List<AstNode> arguments,
        IEvaluationContext context,
        Func<double, double, double> operation)
    {
        if (!TryGetPairs(arguments, context, out List<double>? left, out List<double>? right, out object error))
            return error;
        double total = 0;
        for (int index = 0; index < left.Count; index++)
        {
            total += operation(left[index], right[index]);
        }

        return total;
    }

    private static object CalculateSlope(List<double> knownY, List<double> knownX)
    {
        double xMean = Mean(knownX);
        double yMean = Mean(knownY);
        double numerator = 0;
        double denominator = 0;
        for (int index = 0; index < knownX.Count; index++)
        {
            double xDifference = knownX[index] - xMean;
            numerator += xDifference * (knownY[index] - yMean);
            denominator += xDifference * xDifference;
        }

        return denominator == 0 ? OdfFormulaError.Div0 : numerator / denominator;
    }

    private static bool TryGetNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out List<double> numbers,
        out object error)
    {
        numbers = [];
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
                if (FormulaCoercion.TryCoerceDouble(value, out double number))
                    numbers.Add(number);
            }
        }

        if (numbers.Count == 0)
        {
            error = OdfFormulaError.Div0;
            return false;
        }

        error = 0d;
        return true;
    }

    private static bool TryGetAValues(
        List<AstNode> arguments,
        IEvaluationContext context,
        out List<double> numbers,
        out object error)
    {
        numbers = [];
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
                numbers.Add(value switch
                {
                    double number => number,
                    bool logical => logical ? 1d : 0d,
                    string text when FormulaCoercion.TryCoerceDouble(text, out double number) => number,
                    _ => 0d
                });
            }
        }

        if (numbers.Count == 0)
        {
            error = OdfFormulaError.Div0;
            return false;
        }

        error = 0d;
        return true;
    }

    private static bool TryGetPairs(
        List<AstNode> arguments,
        IEvaluationContext context,
        out List<double> left,
        out List<double> right,
        out object error)
    {
        left = [];
        right = [];
        if (arguments.Count != 2)
        {
            error = OdfFormulaError.Value;
            return false;
        }

        object leftValue = arguments[0].Evaluate(context);
        object rightValue = arguments[1].Evaluate(context);
        if (leftValue is OdfFormulaError leftError)
        {
            error = leftError;
            return false;
        }
        if (rightValue is OdfFormulaError rightError)
        {
            error = rightError;
            return false;
        }

        var leftValues = new List<object>(FormulaCoercion.FlattenValues(leftValue));
        var rightValues = new List<object>(FormulaCoercion.FlattenValues(rightValue));
        if (leftValues.Count != rightValues.Count)
        {
            error = OdfFormulaError.NA;
            return false;
        }
        for (int index = 0; index < leftValues.Count; index++)
        {
            bool hasLeft = FormulaCoercion.TryCoerceDouble(leftValues[index], out double leftNumber);
            bool hasRight = FormulaCoercion.TryCoerceDouble(rightValues[index], out double rightNumber);
            if (hasLeft && hasRight)
            {
                left.Add(leftNumber);
                right.Add(rightNumber);
            }
        }

        if (left.Count == 0)
        {
            error = OdfFormulaError.Div0;
            return false;
        }

        error = 0d;
        return true;
    }

    private static bool TryGetThreeNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out double first,
        out double second,
        out double third,
        out object error)
    {
        first = 0;
        second = 0;
        third = 0;
        if (arguments.Count != 3)
        {
            error = OdfFormulaError.Value;
            return false;
        }

        object firstValue = arguments[0].Evaluate(context);
        object secondValue = arguments[1].Evaluate(context);
        object thirdValue = arguments[2].Evaluate(context);
        if (firstValue is OdfFormulaError firstError)
        {
            error = firstError;
            return false;
        }
        if (secondValue is OdfFormulaError secondError)
        {
            error = secondError;
            return false;
        }
        if (thirdValue is OdfFormulaError thirdError)
        {
            error = thirdError;
            return false;
        }
        if (!FormulaCoercion.TryCoerceDouble(firstValue, out first) ||
            !FormulaCoercion.TryCoerceDouble(secondValue, out second) ||
            !FormulaCoercion.TryCoerceDouble(thirdValue, out third))
        {
            error = OdfFormulaError.Value;
            return false;
        }

        error = 0d;
        return true;
    }

    private static double Mean(List<double> numbers)
    {
        double total = 0;
        foreach (double number in numbers)
        {
            total += number;
        }

        return total / numbers.Count;
    }
}
