using System;
using System.Collections.Generic;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula probability-distribution and special-function handlers.
/// OpenFormula 機率分佈及特殊函式處理常式。
/// </summary>
internal static class FormulaDistributionFunctionHandlers
{
    internal static object EvaluateErf(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 2, out double[] values, out object error))
            return error;
        return values.Length == 1 ? Erf(values[0]) : Erf(values[1]) - Erf(values[0]);
    }

    internal static object EvaluateErfc(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 1, out double[] values, out object error))
            return error;
        return 1 - Erf(values[0]);
    }

    internal static object EvaluateGammaLn(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 1, out double[] values, out object error))
            return error;
        return values[0] > 0 ? LogGamma(values[0]) : OdfFormulaError.Num;
    }

    internal static object EvaluateFisher(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 1, out double[] values, out object error))
            return error;
        double number = values[0];
        return number is > -1 and < 1
            ? 0.5 * Math.Log((1 + number) / (1 - number))
            : OdfFormulaError.Num;
    }

    internal static object EvaluateFisherInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 1, out double[] values, out object error))
            return error;
        return Math.Tanh(values[0]);
    }

    internal static object EvaluateExponDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, 3, out double[] values, out object error))
            return error;
        if (values[0] < 0 || values[1] <= 0)
            return OdfFormulaError.Num;
        return IsTrue(values[2])
            ? 1 - Math.Exp(-values[1] * values[0])
            : values[1] * Math.Exp(-values[1] * values[0]);
    }

    internal static object EvaluateBinomDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 4, 4, out double[] values, out object error))
            return error;
        long successes = (long)Math.Truncate(values[0]);
        long trials = (long)Math.Truncate(values[1]);
        double probability = values[2];
        if (successes < 0 || trials < successes || probability is < 0 or > 1)
            return OdfFormulaError.Num;
        if (!IsTrue(values[3]))
            return BinomialProbability(trials, successes, probability);
        double result = 0;
        for (long current = 0; current <= successes; current++)
        {
            result += BinomialProbability(trials, current, probability);
        }

        return result;
    }

    internal static object EvaluateNegBinomDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, 3, out double[] values, out object error))
            return error;
        long failures = (long)Math.Truncate(values[0]);
        long successes = (long)Math.Truncate(values[1]);
        double probability = values[2];
        if (failures < 0 || successes < 1 || probability is < 0 or > 1)
            return OdfFormulaError.Num;
        return Combination(failures + successes - 1, failures) *
            Math.Pow(probability, successes) *
            Math.Pow(1 - probability, failures);
    }

    internal static object EvaluatePoisson(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, 3, out double[] values, out object error))
            return error;
        long events = (long)Math.Truncate(values[0]);
        double mean = values[1];
        if (events < 0 || mean <= 0)
            return OdfFormulaError.Num;
        if (!IsTrue(values[2]))
            return PoissonProbability(events, mean);
        double result = 0;
        for (long current = 0; current <= events; current++)
        {
            result += PoissonProbability(current, mean);
        }

        return result;
    }

    internal static object EvaluateWeibull(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 4, 4, out double[] values, out object error))
            return error;
        double number = values[0];
        double shape = values[1];
        double scale = values[2];
        if (number < 0 || shape <= 0 || scale <= 0)
            return OdfFormulaError.Num;
        double powered = Math.Pow(number / scale, shape);
        return IsTrue(values[3])
            ? 1 - Math.Exp(-powered)
            : (shape / Math.Pow(scale, shape)) *
                Math.Pow(number, shape - 1) *
                Math.Exp(-powered);
    }

    internal static object EvaluateNormDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 4, 4, out double[] values, out object error))
            return error;
        if (values[2] <= 0)
            return OdfFormulaError.Num;
        double standardized = (values[0] - values[1]) / values[2];
        return IsTrue(values[3])
            ? NormalCdf(standardized)
            : Math.Exp(-0.5 * standardized * standardized) /
                (values[2] * Math.Sqrt(2 * Math.PI));
    }

    internal static object EvaluateNormSdist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 1, out double[] values, out object error))
            return error;
        return NormalCdf(values[0]);
    }

    internal static object EvaluateNormInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, 3, out double[] values, out object error))
            return error;
        if (values[0] is <= 0 or >= 1 || values[2] <= 0)
            return OdfFormulaError.Num;
        return values[1] + (values[2] * InverseNormal(values[0]));
    }

    internal static object EvaluateNormSInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 1, 1, out double[] values, out object error))
            return error;
        return values[0] is > 0 and < 1
            ? InverseNormal(values[0])
            : OdfFormulaError.Num;
    }

    internal static object EvaluateConfidence(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetNumbers(arguments, context, 3, 3, out double[] values, out object error))
            return error;
        if (values[0] is <= 0 or >= 1 || values[1] <= 0 || values[2] < 1)
            return OdfFormulaError.Num;
        return InverseNormal(1 - (values[0] / 2)) *
            values[1] /
            Math.Sqrt(Math.Truncate(values[2]));
    }

    private static double BinomialProbability(long trials, long successes, double probability)
        => Combination(trials, successes) *
            Math.Pow(probability, successes) *
            Math.Pow(1 - probability, trials - successes);

    private static double PoissonProbability(long events, double mean)
        => Math.Exp((-mean) + (events * Math.Log(mean)) - LogGamma(events + 1));

    private static double Combination(long number, long chosen)
    {
        chosen = Math.Min(chosen, number - chosen);
        double result = 1;
        for (long index = 1; index <= chosen; index++)
        {
            result *= (number - chosen + index) / (double)index;
        }

        return result;
    }

    private static double NormalCdf(double value)
        => 0.5 * (1 + Erf(value / Math.Sqrt(2)));

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

    private static double LogGamma(double value)
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
            return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * value)) - LogGamma(1 - value);
        value--;
        double sum = 0.99999999999980993;
        for (int index = 0; index < coefficients.Length; index++)
        {
            sum += coefficients[index] / (value + index + 1);
        }
        double shifted = value + coefficients.Length - 0.5;
        return 0.5 * Math.Log(2 * Math.PI) +
            ((value + 0.5) * Math.Log(shifted)) -
            shifted +
            Math.Log(sum);
    }

    private static double InverseNormal(double probability)
    {
        double[] a =
        [
            -39.69683028665376, 220.9460984245205, -275.9285104469687,
            138.3577518672690, -30.66479806614716, 2.506628277459239
        ];
        double[] b =
        [
            -54.47609879822406, 161.5858368580409, -155.6989798598866,
            66.80131188771972, -13.28068155288572
        ];
        double[] c =
        [
            -0.007784894002430293, -0.3223964580411365, -2.400758277161838,
            -2.549732539343734, 4.374664141464968, 2.938163982698783
        ];
        double[] d =
        [
            0.007784695709041462, 0.3224671290700398,
            2.445134137142996, 3.754408661907416
        ];
        if (probability < 0.02425)
        {
            double q = Math.Sqrt(-2 * Math.Log(probability));
            double numerator = (((((c[0] * q) + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5];
            double denominator = ((((d[0] * q) + d[1]) * q + d[2]) * q + d[3]) * q + 1;
            return numerator / denominator;
        }
        if (probability > 0.97575)
            return -InverseNormal(1 - probability);
        double centered = probability - 0.5;
        double squared = centered * centered;
        double centralNumerator =
            (((((a[0] * squared) + a[1]) * squared + a[2]) * squared + a[3]) *
                squared + a[4]) *
            squared +
            a[5];
        double centralDenominator =
            (((((b[0] * squared) + b[1]) * squared + b[2]) * squared + b[3]) *
                squared + b[4]) *
            squared +
            1;
        return centralNumerator * centered / centralDenominator;
    }

    private static bool IsTrue(double value) => value != 0;

    private static bool TryGetNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        int minimum,
        int maximum,
        out double[] values,
        out object error)
    {
        values = [];
        if (arguments.Count < minimum || arguments.Count > maximum)
        {
            error = OdfFormulaError.Value;
            return false;
        }
        values = new double[arguments.Count];
        for (int index = 0; index < arguments.Count; index++)
        {
            object value = arguments[index].Evaluate(context);
            if (value is OdfFormulaError formulaError)
            {
                error = formulaError;
                return false;
            }
            if (!FormulaCoercion.TryCoerceDouble(value, out values[index]))
            {
                error = OdfFormulaError.Value;
                return false;
            }
        }
        error = 0d;
        return true;
    }
}
