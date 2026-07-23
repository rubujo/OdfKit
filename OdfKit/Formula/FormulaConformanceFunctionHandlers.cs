using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// OpenFormula Medium 與 Large Group 相容函式處理常式。
/// </summary>
internal static class FormulaConformanceFunctionHandlers
{
    internal static object Evaluate(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        return name switch
        {
            "BESSELI" => EvaluateBessel(arguments, context, 0),
            "BESSELJ" => EvaluateBessel(arguments, context, 1),
            "BESSELK" => EvaluateBessel(arguments, context, 2),
            "BESSELY" => EvaluateBessel(arguments, context, 3),
            "BETADIST" => EvaluateBetaDist(arguments, context),
            "BETAINV" => EvaluateBetaInv(arguments, context),
            "GAMMADIST" => EvaluateGammaDist(arguments, context),
            "GAMMAINV" => EvaluateGammaInv(arguments, context),
            "LOGINV" => EvaluateLogInv(arguments, context),
            "LOGNORMDIST" => EvaluateLogNormDist(arguments, context),
            "CHISQDIST" or "LEGACY.CHIDIST" => EvaluateChiDist(arguments, context, name),
            "CHISQINV" or "LEGACY.CHIINV" => EvaluateChiInv(arguments, context, name),
            "FDIST" or "LEGACY.FDIST" => EvaluateFDist(arguments, context, name),
            "FINV" or "LEGACY.FINV" => EvaluateFInv(arguments, context, name),
            "LEGACY.TDIST" => EvaluateTDist(arguments, context),
            "TINV" => EvaluateTInv(arguments, context),
            "CRITBINOM" => EvaluateCritBinom(arguments, context),
            "HYPGEOMDIST" => EvaluateHypGeomDist(arguments, context),
            "KURT" => EvaluateMoment(arguments, context, 0),
            "SKEW" => EvaluateMoment(arguments, context, 1),
            "SKEWP" => EvaluateMoment(arguments, context, 2),
            "PERCENTRANK" => EvaluatePercentRank(arguments, context),
            "PROB" => EvaluateProb(arguments, context),
            "TRIMMEAN" => EvaluateTrimMean(arguments, context),
            "FTEST" or "LEGACY.CHITEST" or "TTEST" or "ZTEST" =>
                EvaluateStatisticalTest(name, arguments, context),
            "LINEST" or "LOGEST" or "TREND" or "GROWTH" =>
                EvaluateRegression(name, arguments, context),
            "STEYX" => EvaluateSteyx(arguments, context),
            "FREQUENCY" => EvaluateFrequency(arguments, context),
            "LOOKUP" => EvaluateLookup(arguments, context),
            "SUBTOTAL" => EvaluateSubtotal(arguments, context),
            "DAYS360" or "YEARFRAC" => EvaluateDayBasis(name, arguments, context),
            "EFFECT" or "NOMINAL" => EvaluateRateConversion(name, arguments, context),
            "DOLLARDE" or "DOLLARFR" => EvaluateDollarConversion(name, arguments, context),
            "XIRR" or "XNPV" => EvaluateIrregularCashFlow(name, arguments, context),
            "CUMIPMT" or "CUMPRINC" => EvaluateCumulativePayment(name, arguments, context),
            "DB" or "VDB" or "AMORLINC" => EvaluateDepreciation(name, arguments, context),
            "ACCRINT" or "ACCRINTM" or "DISC" or "INTRATE" or "RECEIVED" or
            "TBILLEQ" or "TBILLPRICE" or "TBILLYIELD" or "PRICEDISC" or
            "YIELDDISC" or "PRICEMAT" or "YIELDMAT" =>
                EvaluateSecurity(name, arguments, context),
            "COUPDAYBS" or "COUPDAYS" or "COUPDAYSNC" or "COUPNCD" or
            "COUPNUM" or "COUPPCD" => EvaluateCoupon(name, arguments, context),
            "PRICE" or "YIELD" or "DURATION" or "MDURATION" or
            "ODDFPRICE" or "ODDFYIELD" or "ODDLPRICE" or "ODDLYIELD" =>
                EvaluateBond(name, arguments, context),
            "FORMULA" => EvaluateFormula(arguments, context),
            "SHEET" => EvaluateSheet(arguments, context),
            "SHEETS" => EvaluateSheets(arguments, context),
            "HYPERLINK" => EvaluateHyperlink(arguments, context),
            "INFO" => EvaluateInfo(arguments, context),
            "EUROCONVERT" => EvaluateEuroConvert(arguments, context),
            "GETPIVOTDATA" => EvaluateGetPivotData(arguments, context),
            "MULTIPLE.OPERATIONS" => EvaluateMultipleOperations(arguments, context),
            "DDE" => OdfFormulaError.NA,
            _ => OdfFormulaError.Name
        };
    }

    private static object EvaluateBessel(
        List<AstNode> arguments,
        IEvaluationContext context,
        int kind)
    {
        if (!TryGetScalars(arguments, context, 2, 2, out double[] values, out object error))
            return error;
        double x = values[0];
        int order = (int)Math.Truncate(values[1]);
        if (order < 0 || (kind == 2 && x <= 0) || (kind == 3 && x <= 0))
            return OdfFormulaError.Num;
        if (kind == 0)
            return BesselSeries(x, order, false);
        if (kind == 1)
            return BesselSeries(x, order, true);
        if (kind == 2)
            return Integrate(t => Math.Exp(-x * Math.Cosh(t)) * Math.Cosh(order * t), 0, 20);
        return BesselY(x, order);
    }

    private static object EvaluateBetaDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 5, out double[] v, out object error))
            return error;
        double lower = v.Length >= 4 ? v[3] : 0;
        double upper = v.Length == 5 ? v[4] : 1;
        if (v[1] <= 0 || v[2] <= 0 || upper <= lower || v[0] < lower || v[0] > upper)
            return OdfFormulaError.Num;
        return RegularizedBeta((v[0] - lower) / (upper - lower), v[1], v[2]);
    }

    private static object EvaluateBetaInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 5, out double[] v, out object error))
            return error;
        double lower = v.Length >= 4 ? v[3] : 0;
        double upper = v.Length == 5 ? v[4] : 1;
        if (v[0] is < 0 or > 1 || v[1] <= 0 || v[2] <= 0 || upper <= lower)
            return OdfFormulaError.Num;
        return lower + ((upper - lower) * Invert(
            x => RegularizedBeta(x, v[1], v[2]), v[0], 0, 1));
    }

    private static object EvaluateGammaDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 4, 4, out double[] v, out object error))
            return error;
        if (v[0] < 0 || v[1] <= 0 || v[2] <= 0)
            return OdfFormulaError.Num;
        if (v[3] != 0)
            return RegularizedGammaP(v[1], v[0] / v[2]);
        return Math.Exp(((v[1] - 1) * Math.Log(v[0])) -
            (v[0] / v[2]) - LogGamma(v[1]) - (v[1] * Math.Log(v[2])));
    }

    private static object EvaluateGammaInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 3, out double[] v, out object error))
            return error;
        if (v[0] is <= 0 or >= 1 || v[1] <= 0 || v[2] <= 0)
            return OdfFormulaError.Num;
        double high = Math.Max(1, v[1] * v[2]);
        while (RegularizedGammaP(v[1], high / v[2]) < v[0] && high < 1e300)
            high *= 2;
        return Invert(x => RegularizedGammaP(v[1], x / v[2]), v[0], 0, high);
    }

    private static object EvaluateLogInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 3, out double[] v, out object error))
            return error;
        if (v[0] is <= 0 or >= 1 || v[2] <= 0)
            return OdfFormulaError.Num;
        return Math.Exp(v[1] + (v[2] * InverseNormal(v[0])));
    }

    private static object EvaluateLogNormDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 3, out double[] v, out object error))
            return error;
        if (v[0] <= 0 || v[2] <= 0)
            return OdfFormulaError.Num;
        return NormalCdf((Math.Log(v[0]) - v[1]) / v[2]);
    }

    private static object EvaluateChiDist(
        List<AstNode> arguments,
        IEvaluationContext context,
        string name)
    {
        int maximum = name == "CHISQDIST" ? 3 : 2;
        if (!TryGetScalars(arguments, context, 2, maximum, out double[] v, out object error))
            return error;
        if (v[0] < 0 || v[1] < 1)
            return OdfFormulaError.Num;
        double cdf = RegularizedGammaP(Math.Truncate(v[1]) / 2, v[0] / 2);
        if (name != "CHISQDIST")
            return 1 - cdf;
        if (v.Length == 3 && v[2] != 0)
            return cdf;
        return Math.Exp(((v[1] / 2 - 1) * Math.Log(v[0])) -
            (v[0] / 2) - ((v[1] / 2) * Math.Log(2)) - LogGamma(v[1] / 2));
    }

    private static object EvaluateChiInv(
        List<AstNode> arguments,
        IEvaluationContext context,
        string name)
    {
        if (!TryGetScalars(arguments, context, 2, 2, out double[] v, out object error))
            return error;
        if (v[0] is <= 0 or >= 1 || v[1] < 1)
            return OdfFormulaError.Num;
        double target = name == "LEGACY.CHIINV" ? 1 - v[0] : v[0];
        double high = Math.Max(1, v[1]);
        while (RegularizedGammaP(v[1] / 2, high / 2) < target)
            high *= 2;
        return Invert(x => RegularizedGammaP(v[1] / 2, x / 2), target, 0, high);
    }

    private static object EvaluateFDist(
        List<AstNode> arguments,
        IEvaluationContext context,
        string name)
    {
        int maximum = name == "FDIST" ? 4 : 3;
        if (!TryGetScalars(arguments, context, 3, maximum, out double[] v, out object error))
            return error;
        if (v[0] < 0 || v[1] < 1 || v[2] < 1)
            return OdfFormulaError.Num;
        double cdf = RegularizedBeta(
            (v[1] * v[0]) / ((v[1] * v[0]) + v[2]), v[1] / 2, v[2] / 2);
        bool cumulative = name != "FDIST" || v.Length < 4 || v[3] != 0;
        if (cumulative)
            return name == "LEGACY.FDIST" ? 1 - cdf : cdf;
        double numerator = Math.Pow(v[1] / v[2], v[1] / 2) *
            Math.Pow(v[0], (v[1] / 2) - 1);
        double denominator = Math.Exp(LogBeta(v[1] / 2, v[2] / 2)) *
            Math.Pow(1 + ((v[1] * v[0]) / v[2]), (v[1] + v[2]) / 2);
        return numerator / denominator;
    }

    private static object EvaluateFInv(
        List<AstNode> arguments,
        IEvaluationContext context,
        string name)
    {
        if (!TryGetScalars(arguments, context, 3, 3, out double[] v, out object error))
            return error;
        if (v[0] is <= 0 or >= 1 || v[1] < 1 || v[2] < 1)
            return OdfFormulaError.Num;
        double target = name == "LEGACY.FINV" ? 1 - v[0] : v[0];
        double high = 1;
        while (FCdf(high, v[1], v[2]) < target)
            high *= 2;
        return Invert(x => FCdf(x, v[1], v[2]), target, 0, high);
    }

    private static object EvaluateTDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 3, out double[] v, out object error))
            return error;
        if (v[0] < 0 || v[1] < 1 || (v[2] != 1 && v[2] != 2))
            return OdfFormulaError.Num;
        double tail = 0.5 * RegularizedBeta(v[1] / (v[1] + (v[0] * v[0])), v[1] / 2, 0.5);
        return v[2] == 1 ? tail : 2 * tail;
    }

    private static object EvaluateTInv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 2, 2, out double[] v, out object error))
            return error;
        if (v[0] is <= 0 or >= 1 || v[1] < 1)
            return OdfFormulaError.Num;
        return Invert(
            x => 1 - RegularizedBeta(v[1] / (v[1] + (x * x)), v[1] / 2, 0.5),
            1 - v[0], 0, 1e6);
    }

    private static object EvaluateCritBinom(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 3, out double[] v, out object error))
            return error;
        int trials = (int)Math.Truncate(v[0]);
        if (trials < 0 || v[1] is < 0 or > 1 || v[2] is < 0 or > 1)
            return OdfFormulaError.Num;
        double cumulative = 0;
        for (int value = 0; value <= trials; value++)
        {
            cumulative += Combination(trials, value) * Math.Pow(v[1], value) *
                Math.Pow(1 - v[1], trials - value);
            if (cumulative >= v[2])
                return (double)value;
        }
        return (double)trials;
    }

    private static object EvaluateHypGeomDist(List<AstNode> arguments, IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 4, 5, out double[] v, out object error))
            return error;
        int successes = (int)Math.Truncate(v[0]);
        int sample = (int)Math.Truncate(v[1]);
        int populationSuccesses = (int)Math.Truncate(v[2]);
        int population = (int)Math.Truncate(v[3]);
        bool cumulative = v.Length == 5 && v[4] != 0;
        if (successes < 0 || sample < 0 || populationSuccesses < 0 ||
            population <= 0 || sample > population || populationSuccesses > population)
            return OdfFormulaError.Num;
        double Probability(int k) => Combination(populationSuccesses, k) *
            Combination(population - populationSuccesses, sample - k) /
            Combination(population, sample);
        if (!cumulative)
            return Probability(successes);
        double result = 0;
        for (int k = Math.Max(0, sample - (population - populationSuccesses)); k <= successes; k++)
            result += Probability(k);
        return result;
    }

    private static object EvaluateMoment(
        List<AstNode> arguments,
        IEvaluationContext context,
        int kind)
    {
        if (!TryGetNumbers(arguments, context, out double[] values, out object error))
            return error;
        int count = values.Length;
        if (count < (kind == 0 ? 4 : kind == 1 ? 3 : 1))
            return OdfFormulaError.Div0;
        double mean = Mean(values);
        double m2 = 0;
        double m3 = 0;
        double m4 = 0;
        foreach (double value in values)
        {
            double d = value - mean;
            m2 += d * d;
            m3 += d * d * d;
            m4 += d * d * d * d;
        }
        if (m2 == 0)
            return OdfFormulaError.Div0;
        if (kind == 2)
            return m3 / count / Math.Pow(m2 / count, 1.5);
        if (kind == 1)
            return count * m3 / ((count - 1d) * (count - 2d) *
                Math.Pow(Math.Sqrt(m2 / (count - 1d)), 3));
        double variance = m2 / (count - 1d);
        return (count * (count + 1d) * m4 /
            ((count - 1d) * (count - 2d) * (count - 3d) * variance * variance)) -
            (3d * (count - 1d) * (count - 1d) / ((count - 2d) * (count - 3d)));
    }

    private static object EvaluatePercentRank(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 2 or > 3)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] values, out object error) ||
            !TryGetNumber(arguments[1], context, out double target, out error))
            return error;
        int digits = 3;
        if (arguments.Count == 3)
        {
            if (!TryGetNumber(arguments[2], context, out double digitsValue, out error))
                return error;
            digits = (int)Math.Truncate(digitsValue);
        }
        if (values.Length < 2 || digits < 1)
            return OdfFormulaError.Num;
        Array.Sort(values);
        if (target < values[0] || target > values[values.Length - 1])
            return OdfFormulaError.NA;
        int upper = Array.BinarySearch(values, target);
        double rank;
        if (upper >= 0)
            rank = upper / (double)(values.Length - 1);
        else
        {
            upper = ~upper;
            int lower = upper - 1;
            rank = (lower + ((target - values[lower]) / (values[upper] - values[lower]))) /
                (values.Length - 1);
        }
        return Math.Round(rank, digits, MidpointRounding.AwayFromZero);
    }

    private static object EvaluateProb(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 3 or > 4)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] values, out object error) ||
            !TryGetNumbers(arguments[1].Evaluate(context), out double[] probabilities, out error) ||
            !TryGetNumber(arguments[2], context, out double lower, out error))
            return error;
        double upper = lower;
        if (arguments.Count == 4 && !TryGetNumber(arguments[3], context, out upper, out error))
            return error;
        if (values.Length != probabilities.Length)
            return OdfFormulaError.NA;
        double sum = 0;
        double total = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (probabilities[i] is < 0 or > 1)
                return OdfFormulaError.Num;
            total += probabilities[i];
            if (values[i] >= lower && values[i] <= upper)
                sum += probabilities[i];
        }
        return Math.Abs(total - 1) <= 1e-7 ? sum : OdfFormulaError.Num;
    }

    private static object EvaluateTrimMean(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] values, out object error) ||
            !TryGetNumber(arguments[1], context, out double fraction, out error))
            return error;
        if (values.Length == 0 || fraction is < 0 or >= 1)
            return OdfFormulaError.Num;
        Array.Sort(values);
        int trimEachSide = (int)Math.Floor(values.Length * fraction / 2);
        double sum = 0;
        for (int i = trimEachSide; i < values.Length - trimEachSide; i++)
            sum += values[i];
        return sum / (values.Length - (2 * trimEachSide));
    }

    private static object EvaluateStatisticalTest(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (name == "ZTEST")
        {
            if (arguments.Count is < 2 or > 3)
                return OdfFormulaError.Value;
            if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] values, out object error) ||
                !TryGetNumber(arguments[1], context, out double mean, out error))
                return error;
            double sigma = StandardDeviation(values, true);
            if (arguments.Count == 3 &&
                !TryGetNumber(arguments[2], context, out sigma, out error))
                return error;
            if (values.Length == 0 || sigma <= 0)
                return OdfFormulaError.Div0;
            return 1 - NormalCdf((Mean(values) - mean) / (sigma / Math.Sqrt(values.Length)));
        }
        if (arguments.Count < 2)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] first, out object firstError) ||
            !TryGetNumbers(arguments[1].Evaluate(context), out double[] second, out firstError))
            return firstError;
        if (first.Length < 2 || second.Length < 2)
            return OdfFormulaError.Div0;
        if (name == "FTEST")
        {
            double ratio = Math.Pow(StandardDeviation(first, false), 2) /
                Math.Pow(StandardDeviation(second, false), 2);
            double cdf = FCdf(ratio, first.Length - 1, second.Length - 1);
            return 2 * Math.Min(cdf, 1 - cdf);
        }
        if (name == "LEGACY.CHITEST")
        {
            if (first.Length != second.Length)
                return OdfFormulaError.NA;
            double statistic = 0;
            for (int i = 0; i < first.Length; i++)
            {
                if (second[i] <= 0)
                    return OdfFormulaError.Div0;
                statistic += Math.Pow(first[i] - second[i], 2) / second[i];
            }
            return 1 - RegularizedGammaP((first.Length - 1) / 2d, statistic / 2);
        }
        if (arguments.Count != 4)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[2], context, out double tailsValue, out firstError) ||
            !TryGetNumber(arguments[3], context, out double typeValue, out firstError))
            return firstError;
        int tails = (int)Math.Truncate(tailsValue);
        int type = (int)Math.Truncate(typeValue);
        if (tails is not (1 or 2) || type is < 1 or > 3)
            return OdfFormulaError.Num;
        double t;
        double degrees;
        if (type == 1)
        {
            if (first.Length != second.Length || first.Length < 2)
                return OdfFormulaError.NA;
            var differences = new double[first.Length];
            for (int index = 0; index < first.Length; index++)
                differences[index] = first[index] - second[index];
            double standardError = StandardDeviation(differences, false) /
                Math.Sqrt(differences.Length);
            if (standardError == 0)
                return OdfFormulaError.Div0;
            t = Math.Abs(Mean(differences) / standardError);
            degrees = differences.Length - 1;
        }
        else
        {
            double firstVariance = Math.Pow(StandardDeviation(first, false), 2);
            double secondVariance = Math.Pow(StandardDeviation(second, false), 2);
            double difference = Mean(first) - Mean(second);
            if (type == 2)
            {
                degrees = first.Length + second.Length - 2;
                double pooled = (((first.Length - 1) * firstVariance) +
                    ((second.Length - 1) * secondVariance)) / degrees;
                t = Math.Abs(difference) /
                    Math.Sqrt(pooled * ((1d / first.Length) + (1d / second.Length)));
            }
            else
            {
                double firstTerm = firstVariance / first.Length;
                double secondTerm = secondVariance / second.Length;
                t = Math.Abs(difference) / Math.Sqrt(firstTerm + secondTerm);
                degrees = Math.Pow(firstTerm + secondTerm, 2) /
                    ((Math.Pow(firstTerm, 2) / (first.Length - 1)) +
                        (Math.Pow(secondTerm, 2) / (second.Length - 1)));
            }
        }
        double tail = 0.5 * RegularizedBeta(degrees / (degrees + (t * t)), degrees / 2, 0.5);
        return tails == 1 ? tail : 2 * tail;
    }

    private static object EvaluateRegression(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 4)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] ys, out object error))
            return error;
        double[,] xs;
        if (arguments.Count >= 2)
        {
            if (!TryGetRegressionMatrix(
                arguments[1].Evaluate(context),
                ys.Length,
                out xs,
                out error))
                return error;
        }
        else
        {
            xs = new double[ys.Length, 1];
            for (int row = 0; row < ys.Length; row++)
                xs[row, 0] = row + 1;
        }
        if (ys.Length < 2 || xs.GetLength(0) != ys.Length)
            return OdfFormulaError.NA;
        bool exponential = name is "LOGEST" or "GROWTH";
        if (exponential)
        {
            for (int i = 0; i < ys.Length; i++)
            {
                if (ys[i] <= 0)
                    return OdfFormulaError.Num;
                ys[i] = Math.Log(ys[i]);
            }
        }
        bool includeIntercept = true;
        int interceptArgument = name is "LINEST" or "LOGEST" ? 2 : 3;
        if (arguments.Count > interceptArgument)
        {
            object interceptValue = arguments[interceptArgument].Evaluate(context);
            if (interceptValue is OdfFormulaError formulaError)
                return formulaError;
            includeIntercept = FormulaCoercion.CoerceToBool(interceptValue);
        }
        bool includeStatistics = false;
        if (name is "LINEST" or "LOGEST" && arguments.Count == 4)
        {
            object statisticsValue = arguments[3].Evaluate(context);
            if (statisticsValue is OdfFormulaError formulaError)
                return formulaError;
            includeStatistics = FormulaCoercion.CoerceToBool(statisticsValue);
        }
        if (!TryFitLinearModel(
            xs,
            ys,
            includeIntercept,
            out double[] coefficients,
            out double[,] inverseNormal,
            out int modelRank))
            return OdfFormulaError.Num;
        if (name is "LINEST" or "LOGEST")
        {
            return CreateRegressionResult(
                xs,
                ys,
                coefficients,
                inverseNormal,
                modelRank,
                includeIntercept,
                exponential,
                includeStatistics);
        }
        double[,] predictionX = xs;
        if (arguments.Count >= 3 &&
            !TryGetRegressionMatrix(
                arguments[2].Evaluate(context),
                null,
                out predictionX,
                out error))
            return error;
        if (predictionX.GetLength(1) != xs.GetLength(1))
            return OdfFormulaError.NA;
        var predictions = new object[predictionX.GetLength(0), 1];
        for (int row = 0; row < predictionX.GetLength(0); row++)
        {
            double prediction = coefficients[coefficients.Length - 1];
            for (int column = 0; column < predictionX.GetLength(1); column++)
                prediction += coefficients[column] * predictionX[row, column];
            predictions[row, 0] = exponential ? Math.Exp(prediction) : prediction;
        }
        return predictions;
    }

    private static object EvaluateSteyx(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] ys, out object error) ||
            !TryGetNumbers(arguments[1].Evaluate(context), out double[] xs, out error))
            return error;
        if (xs.Length != ys.Length || xs.Length < 3)
            return OdfFormulaError.Div0;
        (double slope, double intercept) = LinearFit(xs, ys);
        double residual = 0;
        for (int i = 0; i < xs.Length; i++)
            residual += Math.Pow(ys[i] - intercept - (slope * xs[i]), 2);
        return Math.Sqrt(residual / (xs.Length - 2));
    }

    private static object EvaluateFrequency(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[0].Evaluate(context), out double[] values, out object error) ||
            !TryGetNumbers(arguments[1].Evaluate(context), out double[] bins, out error))
            return error;
        Array.Sort(bins);
        var result = new object[bins.Length + 1, 1];
        var counts = new int[bins.Length + 1];
        foreach (double value in values)
        {
            int index = 0;
            while (index < bins.Length && value > bins[index])
                index++;
            counts[index]++;
        }
        for (int i = 0; i < counts.Length; i++)
            result[i, 0] = (double)counts[i];
        return result;
    }

    private static object EvaluateLookup(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 2 or > 3)
            return OdfFormulaError.Value;
        object target = arguments[0].Evaluate(context);
        object lookupObject = arguments[1].Evaluate(context);
        object resultObject = arguments.Count == 3 ? arguments[2].Evaluate(context) : lookupObject;
        var lookups = new List<object>(FormulaCoercion.FlattenValues(lookupObject));
        var results = new List<object>(FormulaCoercion.FlattenValues(resultObject));
        if (lookups.Count == 0 || lookups.Count != results.Count)
            return OdfFormulaError.NA;
        int best = -1;
        for (int i = 0; i < lookups.Count; i++)
        {
            if (Equals(lookups[i], target))
                return results[i];
            if (TryCompare(lookups[i], target, out int comparison) && comparison <= 0)
                best = i;
        }
        return best >= 0 ? results[best] : OdfFormulaError.NA;
    }

    private static object EvaluateSubtotal(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[0], context, out double codeValue, out object error))
            return error;
        int code = (int)Math.Truncate(codeValue) % 100;
        var values = new List<double>();
        for (int i = 1; i < arguments.Count; i++)
        {
            if (!TryGetNumbers(arguments[i].Evaluate(context), out double[] current, out error))
                return error;
            values.AddRange(current);
        }
        if (values.Count == 0 && code is not 2 and not 3)
            return OdfFormulaError.Div0;
        return code switch
        {
            1 => Mean(values.ToArray()),
            2 or 3 => (double)values.Count,
            4 => Max(values),
            5 => Min(values),
            6 => Product(values),
            7 => StandardDeviation(values.ToArray(), false),
            8 => StandardDeviation(values.ToArray(), true),
            9 => Sum(values),
            10 => Math.Pow(StandardDeviation(values.ToArray(), false), 2),
            11 => Math.Pow(StandardDeviation(values.ToArray(), true), 2),
            _ => OdfFormulaError.Value
        };
    }

    private static object EvaluateDayBasis(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 2, name == "YEARFRAC" ? 3 : 3,
            out double[] v, out object error))
            return error;
        if (!TryDate(v[0], out DateTime start) || !TryDate(v[1], out DateTime end))
            return OdfFormulaError.Num;
        int basis = name == "YEARFRAC" && v.Length == 3 ? (int)v[2] : 0;
        if (name == "DAYS360")
            return (double)Days360(start, end, v.Length == 3 && v[2] != 0);
        return YearFraction(start, end, basis);
    }

    private static object EvaluateRateConversion(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 2, 2, out double[] v, out object error))
            return error;
        int periods = (int)Math.Truncate(v[1]);
        if (v[0] <= 0 || periods < 1)
            return OdfFormulaError.Num;
        return name == "EFFECT"
            ? Math.Pow(1 + (v[0] / periods), periods) - 1
            : periods * (Math.Pow(1 + v[0], 1d / periods) - 1);
    }

    private static object EvaluateDollarConversion(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 2, 2, out double[] v, out object error))
            return error;
        int denominator = (int)Math.Truncate(v[1]);
        if (denominator < 1)
            return OdfFormulaError.Div0;
        double integer = Math.Truncate(v[0]);
        double fraction = Math.Abs(v[0] - integer);
        int digits = (int)Math.Ceiling(Math.Log10(denominator));
        double scale = Math.Pow(10, digits);
        return name == "DOLLARDE"
            ? integer + (fraction * scale / denominator)
            : integer + (fraction * denominator / scale);
    }

    private static object EvaluateIrregularCashFlow(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        int minimum = name == "XIRR" ? 2 : 3;
        int maximum = name == "XIRR" ? 3 : 3;
        if (arguments.Count < minimum || arguments.Count > maximum)
            return OdfFormulaError.Value;
        if (!TryGetNumbers(arguments[name == "XIRR" ? 0 : 1].Evaluate(context),
                out double[] values, out object error) ||
            !TryGetNumbers(arguments[name == "XIRR" ? 1 : 2].Evaluate(context),
                out double[] dates, out error))
            return error;
        if (values.Length != dates.Length || values.Length < 2)
            return OdfFormulaError.Num;
        double Npv(double rate)
        {
            double result = 0;
            for (int i = 0; i < values.Length; i++)
                result += values[i] / Math.Pow(1 + rate, (dates[i] - dates[0]) / 365d);
            return result;
        }
        if (name == "XNPV")
        {
            if (!TryGetNumber(arguments[0], context, out double rate, out error))
                return error;
            if (rate <= -1)
                return OdfFormulaError.Num;
            return Npv(rate);
        }
        double guess = 0.1;
        if (arguments.Count == 3 &&
            !TryGetNumber(arguments[2], context, out guess, out error))
            return error;
        return SolveRoot(Npv, guess);
    }

    private static object EvaluateCumulativePayment(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 6, 6, out double[] v, out object error))
            return error;
        int periods = (int)Math.Truncate(v[1]);
        int start = (int)Math.Truncate(v[3]);
        int end = (int)Math.Truncate(v[4]);
        int type = (int)Math.Truncate(v[5]);
        if (v[0] <= 0 || periods <= 0 || start < 1 || end < start || end > periods ||
            (type != 0 && type != 1))
            return OdfFormulaError.Num;
        double payment = Payment(v[0], periods, v[2], 0, type);
        double balance = v[2];
        double result = 0;
        for (int period = 1; period <= end; period++)
        {
            double interest = type == 1 && period == 1 ? 0 : balance * v[0];
            double principal = -payment - interest;
            if (period >= start)
                result += name == "CUMIPMT" ? -interest : -principal;
            balance -= principal;
        }
        return result;
    }

    private static object EvaluateDepreciation(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (name == "DB")
        {
            if (!TryGetScalars(arguments, context, 4, 5, out double[] v, out object error))
                return error;
            int period = (int)Math.Truncate(v[3]);
            int months = v.Length == 5 ? (int)Math.Truncate(v[4]) : 12;
            if (v[0] < 0 || v[1] < 0 || v[2] <= 0 || period < 1 || months is < 1 or > 12)
                return OdfFormulaError.Num;
            double rate = Math.Round(1 - Math.Pow(v[1] / v[0], 1 / v[2]), 3);
            double book = v[0];
            double depreciation = book * rate * months / 12;
            for (int p = 2; p <= period; p++)
            {
                book -= depreciation;
                depreciation = book * rate * (p == (int)v[2] + 1 ? (12 - months) / 12d : 1);
            }
            return depreciation;
        }
        if (name == "AMORLINC")
        {
            if (!TryGetScalars(arguments, context, 6, 7, out double[] v, out object error))
                return error;
            int period = (int)Math.Truncate(v[4]);
            if (v[0] < 0 || v[3] < 0 || v[5] <= 0 || period < 0)
                return OdfFormulaError.Num;
            double first = Math.Min(v[0] - v[3], v[0] * v[5] * YearFraction(
                DateTime.FromOADate(v[1]), DateTime.FromOADate(v[2]), v.Length == 7 ? (int)v[6] : 0));
            return period == 0 ? first : Math.Min(v[0] - v[3] - first, v[0] * v[5]);
        }
        if (!TryGetScalars(arguments, context, 5, 7, out double[] values, out object err))
            return err;
        double factor = values.Length == 7 ? values[6] : 2;
        if (values[0] < 0 || values[1] < 0 || values[2] <= 0 ||
            values[3] < 0 || values[4] < values[3] || factor <= 0)
            return OdfFormulaError.Num;
        double bookValue = values[0];
        double result = 0;
        int startPeriod = (int)Math.Floor(values[3]);
        int endPeriod = (int)Math.Ceiling(values[4]);
        for (int p = 0; p < endPeriod; p++)
        {
            double declining = Math.Min(bookValue - values[1], bookValue * factor / values[2]);
            double straight = Math.Max(0, (bookValue - values[1]) / Math.Max(1, values[2] - p));
            double depreciation = values.Length >= 6 && values[5] != 0 ? declining : Math.Max(declining, straight);
            double overlap = Math.Max(0, Math.Min(p + 1, values[4]) - Math.Max(p, values[3]));
            if (p >= startPeriod)
                result += depreciation * overlap;
            bookValue -= depreciation;
        }
        return result;
    }

    private static object EvaluateSecurity(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 8, out double[] v, out object error))
            return error;
        if (!TryDate(v[0], out DateTime first) || !TryDate(v[1], out DateTime second) ||
            second <= first)
            return OdfFormulaError.Num;
        int basis = (int)(v.Length > 4 ? v[v.Length - 1] : 0);
        double fraction = YearFraction(first, second, basis);
        return name switch
        {
            "ACCRINTM" => v[2] * (v.Length > 3 ? v[3] : 0) * fraction,
            "ACCRINT" => v[3] * v[4] * YearFraction(second, DateTime.FromOADate(v[2]), basis),
            "DISC" => (v[2] - v[3]) / v[2] / fraction,
            "INTRATE" => (v[3] - v[2]) / v[2] / fraction,
            "RECEIVED" => v[2] / (1 - (v[3] * fraction)),
            "TBILLPRICE" => 100 * (1 - (v[2] * ((second - first).TotalDays / 360))),
            "TBILLYIELD" => (100 - v[2]) / v[2] * 360 / (second - first).TotalDays,
            "TBILLEQ" => 365 * v[2] /
                (360 - (v[2] * (second - first).TotalDays)),
            "PRICEDISC" => v[3] * (1 - (v[2] * fraction)),
            "YIELDDISC" => (v[3] - v[2]) / v[2] / fraction,
            "PRICEMAT" => (100 + (v[3] * 100 * YearFraction(
                DateTime.FromOADate(v[2]), second, basis))) /
                (1 + (v[4] * fraction)),
            "YIELDMAT" => ((100 + (100 * v[3] * YearFraction(
                DateTime.FromOADate(v[2]), second, basis))) / v[4] - 1) / fraction,
            _ => OdfFormulaError.Value
        };
    }

    private static object EvaluateCoupon(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (!TryGetScalars(arguments, context, 3, 4, out double[] v, out object error))
            return error;
        if (!TryDate(v[0], out DateTime settlement) || !TryDate(v[1], out DateTime maturity))
            return OdfFormulaError.Num;
        int frequency = (int)Math.Truncate(v[2]);
        int basis = v.Length == 4 ? (int)Math.Truncate(v[3]) : 0;
        if (settlement >= maturity || frequency is not (1 or 2 or 4) || basis is < 0 or > 4)
            return OdfFormulaError.Num;
        int months = 12 / frequency;
        DateTime previous = maturity;
        int count = 0;
        while (previous > settlement)
        {
            previous = previous.AddMonths(-months);
            count++;
        }
        DateTime next = previous.AddMonths(months);
        return name switch
        {
            "COUPNUM" => (double)count,
            "COUPPCD" => previous.ToOADate(),
            "COUPNCD" => next.ToOADate(),
            "COUPDAYBS" => DayCount(previous, settlement, basis),
            "COUPDAYSNC" => DayCount(settlement, next, basis),
            "COUPDAYS" => basis switch
            {
                2 => 360d / frequency,
                3 => 365d / frequency,
                _ => DayCount(previous, next, basis)
            },
            _ => OdfFormulaError.Value
        };
    }

    private static object EvaluateBond(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        int minimum = name.StartsWith("ODDF", StringComparison.Ordinal) ? 8 :
            name.StartsWith("ODDL", StringComparison.Ordinal) ? 7 : 5;
        int maximum = name.StartsWith("ODDF", StringComparison.Ordinal) ? 9 :
            name.StartsWith("ODDL", StringComparison.Ordinal) ? 8 : 7;
        if (!TryGetScalars(arguments, context, minimum, maximum, out double[] v, out object error))
            return error;
        if (!TryDate(v[0], out DateTime settlement) || !TryDate(v[1], out DateTime maturity))
            return OdfFormulaError.Num;
        bool priceFunction = name is "PRICE" or "ODDFPRICE" or "ODDLPRICE";
        bool oddFirst = name is "ODDFPRICE" or "ODDFYIELD";
        bool oddLast = name is "ODDLPRICE" or "ODDLYIELD";
        DateTime? issue = null;
        DateTime? firstCoupon = null;
        DateTime? lastInterest = null;
        double couponRate = v[2];
        double marketValue = v[3];
        double redemption = 100;
        int frequency;
        int basis;
        if (oddFirst)
        {
            if (!TryDate(v[2], out DateTime issueDate) ||
                !TryDate(v[3], out DateTime firstCouponDate))
            {
                return OdfFormulaError.Num;
            }
            issue = issueDate;
            firstCoupon = firstCouponDate;
            couponRate = v[4];
            marketValue = v[5];
            redemption = v[6];
            frequency = (int)Math.Truncate(v[7]);
            basis = v.Length == 9 ? (int)Math.Truncate(v[8]) : 0;
        }
        else if (oddLast)
        {
            if (!TryDate(v[2], out DateTime lastInterestDate))
                return OdfFormulaError.Num;
            lastInterest = lastInterestDate;
            couponRate = v[3];
            marketValue = v[4];
            redemption = v[5];
            frequency = (int)Math.Truncate(v[6]);
            basis = v.Length == 8 ? (int)Math.Truncate(v[7]) : 0;
        }
        else if (name is "PRICE" or "YIELD")
        {
            redemption = v[4];
            frequency = (int)Math.Truncate(v[5]);
            basis = v.Length == 7 ? (int)Math.Truncate(v[6]) : 0;
        }
        else
        {
            frequency = (int)Math.Truncate(v[4]);
            basis = v.Length == 6 ? (int)Math.Truncate(v[5]) : 0;
        }
        if (settlement >= maturity || frequency is not (1 or 2 or 4))
            return OdfFormulaError.Num;
        double Price(double yield)
            => CalculateBondPrice(
                settlement,
                maturity,
                couponRate,
                yield,
                redemption,
                frequency,
                basis,
                issue,
                firstCoupon,
                lastInterest);
        if (priceFunction)
            return Price(marketValue);
        if (name is "YIELD" or "ODDFYIELD" or "ODDLYIELD")
        {
            double targetPrice = marketValue;
            return SolveRoot(yield => Price(yield) - targetPrice, 0.05);
        }
        double bondYield = marketValue;
        int count = Math.Max(1, (int)Math.Ceiling(
            YearFraction(settlement, maturity, basis) * frequency));
        double weighted = 0;
        double priceValue = 0;
        for (int i = 1; i <= count; i++)
        {
            double cash = i == count
                ? (100 * couponRate / frequency) + 100
                : 100 * couponRate / frequency;
            double present = cash / Math.Pow(1 + (bondYield / frequency), i);
            weighted += (i / (double)frequency) * present;
            priceValue += present;
        }
        double duration = weighted / priceValue;
        return name == "MDURATION" ? duration / (1 + (bondYield / frequency)) : duration;
    }

    private static object EvaluateFormula(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1 || arguments[0] is not CellAddressNode cell)
            return OdfFormulaError.NA;
        string? formula = context.GetCellFormula(cell.Address);
        return string.IsNullOrEmpty(formula) ? OdfFormulaError.NA : formula!;
    }

    private static object EvaluateGetPivotData(
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count % 2 != 0)
            return OdfFormulaError.Value;
        object fieldValue = arguments[0].Evaluate(context);
        if (fieldValue is OdfFormulaError error)
            return error;
        string field = Convert.ToString(fieldValue, CultureInfo.InvariantCulture) ?? string.Empty;
        if (field.Length == 0)
            return OdfFormulaError.Value;
        OdfKit.Spreadsheet.OdfCellAddress anchor;
        if (arguments[1] is CellAddressNode cell)
        {
            anchor = cell.Address;
        }
        else if (arguments[1] is RangeReferenceNode range)
        {
            anchor = range.Range.StartAddress;
        }
        else
        {
            return OdfFormulaError.Value;
        }
        var filters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (int index = 2; index < arguments.Count; index += 2)
        {
            object filterNameValue = arguments[index].Evaluate(context);
            if (filterNameValue is OdfFormulaError filterError)
                return filterError;
            string filterName = Convert.ToString(
                filterNameValue,
                CultureInfo.InvariantCulture) ?? string.Empty;
            if (filterName.Length == 0)
                return OdfFormulaError.Value;
            object filterValue = arguments[index + 1].Evaluate(context);
            if (filterValue is OdfFormulaError valueError)
                return valueError;
            filters[filterName] = filterValue;
        }
        if (context is IOdfFormulaWorkbookContext workbook &&
            workbook.TryGetPivotData(field, anchor, filters, out object pivotResult))
        {
            return pivotResult;
        }
        try
        {
            return context.GetNamedRangeOrExpressionValue(field);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OdfFormulaError.NA;
        }
    }

    private static object EvaluateMultipleOperations(
        List<AstNode> arguments,
        IEvaluationContext context)
    {
        if (arguments.Count is not (3 or 5))
            return OdfFormulaError.Value;
        var values = new object[arguments.Count];
        for (int index = 0; index < arguments.Count; index++)
        {
            values[index] = arguments[index].Evaluate(context);
            if (values[index] is OdfFormulaError error)
                return error;
        }
        return context is IOdfFormulaWorkbookContext workbook &&
            workbook.TryEvaluateMultipleOperations(values, out object result)
            ? result
            : OdfFormulaError.NA;
    }

    private static object EvaluateSheet(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count > 1)
            return OdfFormulaError.Value;
        string? sheetName = arguments.Count == 0
            ? context.CurrentCell.SheetName
            : GetReferencedSheetName(arguments[0], context);
        if (context is not IOdfFormulaWorkbookContext workbook)
            return string.IsNullOrEmpty(sheetName) ? 1d : OdfFormulaError.NA;
        if (string.IsNullOrEmpty(sheetName))
            return workbook.SheetNames.Count > 0 ? 1d : OdfFormulaError.NA;
        for (int index = 0; index < workbook.SheetNames.Count; index++)
        {
            if (string.Equals(
                workbook.SheetNames[index],
                sheetName,
                StringComparison.OrdinalIgnoreCase))
            {
                return (double)(index + 1);
            }
        }
        return OdfFormulaError.NA;
    }

    private static object EvaluateSheets(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count > 1)
            return OdfFormulaError.Value;
        if (arguments.Count == 0)
        {
            return context is IOdfFormulaWorkbookContext workbook
                ? (double)workbook.SheetNames.Count
                : 1d;
        }
        return arguments[0].GetRanges(context).Count > 0 ? 1d : OdfFormulaError.NA;
    }

    private static string? GetReferencedSheetName(
        AstNode argument,
        IEvaluationContext context)
    {
        if (argument is CellAddressNode cell)
            return cell.Address.SheetName ?? context.CurrentCell.SheetName;
        if (argument is RangeReferenceNode range)
            return range.Range.StartAddress.SheetName ?? context.CurrentCell.SheetName;
        List<OdfKit.Spreadsheet.OdfCellRange> ranges = argument.GetRanges(context);
        return ranges.Count == 0
            ? null
            : ranges[0].StartAddress.SheetName ?? context.CurrentCell.SheetName;
    }

    private static object EvaluateHyperlink(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 1 or > 2)
            return OdfFormulaError.Value;
        object location = arguments[0].Evaluate(context);
        if (location is OdfFormulaError error)
            return error;
        if (location is not string text)
            return OdfFormulaError.Value;
        if (arguments.Count == 1)
            return text;
        object label = arguments[1].Evaluate(context);
        return label is OdfFormulaError ? label : Convert.ToString(label, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static object EvaluateInfo(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        string key = Convert.ToString(arguments[0].Evaluate(context), CultureInfo.InvariantCulture) ??
            string.Empty;
        return key.ToLowerInvariant() switch
        {
            "system" => "pcdos",
            "osversion" => Environment.OSVersion.VersionString,
            "recalc" => "Automatic",
            "release" => Environment.Version.ToString(),
            "directory" or "origin" or "memavail" or "memused" or "numfile" or
            "totmem" => OdfFormulaError.NA,
            _ => OdfFormulaError.Value
        };
    }

    private static object EvaluateEuroConvert(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count is < 3 or > 5)
            return OdfFormulaError.Value;
        if (!TryGetNumber(arguments[0], context, out double amount, out object error))
            return error;
        string source = Convert.ToString(arguments[1].Evaluate(context), CultureInfo.InvariantCulture) ??
            string.Empty;
        string target = Convert.ToString(arguments[2].Evaluate(context), CultureInfo.InvariantCulture) ??
            string.Empty;
        var rates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1,
            ["ATS"] = 13.7603,
            ["BEF"] = 40.3399,
            ["DEM"] = 1.95583,
            ["ESP"] = 166.386,
            ["FIM"] = 5.94573,
            ["FRF"] = 6.55957,
            ["IEP"] = 0.787564,
            ["ITL"] = 1936.27,
            ["LUF"] = 40.3399,
            ["NLG"] = 2.20371,
            ["PTE"] = 200.482,
            ["GRD"] = 340.75,
            ["SIT"] = 239.64,
            ["CYP"] = 0.585274,
            ["MTL"] = 0.4293,
            ["SKK"] = 30.126
        };
        return rates.TryGetValue(source, out double sourceRate) &&
            rates.TryGetValue(target, out double targetRate)
            ? amount / sourceRate * targetRate
            : OdfFormulaError.NA;
    }

    private static bool TryGetScalars(
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
        for (int i = 0; i < arguments.Count; i++)
        {
            if (!TryGetNumber(arguments[i], context, out values[i], out error))
                return false;
        }
        error = 0d;
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

    private static bool TryGetNumbers(
        List<AstNode> arguments,
        IEvaluationContext context,
        out double[] values,
        out object error)
    {
        var numbers = new List<double>();
        foreach (AstNode argument in arguments)
        {
            if (!TryGetNumbers(argument.Evaluate(context), out double[] current, out error))
            {
                values = [];
                return false;
            }
            numbers.AddRange(current);
        }
        values = numbers.ToArray();
        error = 0d;
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
        var numbers = new List<double>();
        foreach (object item in FormulaCoercion.FlattenValues(value))
        {
            if (FormulaCoercion.TryCoerceDouble(item, out double number))
                numbers.Add(number);
        }
        values = numbers.ToArray();
        error = values.Length == 0 ? OdfFormulaError.Value : 0d;
        return values.Length > 0;
    }

    private static bool TryGetRegressionMatrix(
        object value,
        int? expectedRows,
        out double[,] matrix,
        out object error)
    {
        if (value is OdfFormulaError formulaError)
        {
            matrix = new double[0, 0];
            error = formulaError;
            return false;
        }
        if (value is object[,] array)
        {
            int rows = array.GetLength(0);
            int columns = array.GetLength(1);
            bool transpose = expectedRows.HasValue &&
                rows != expectedRows.Value &&
                columns == expectedRows.Value;
            int outputRows = transpose ? columns : rows;
            int outputColumns = transpose ? rows : columns;
            if (expectedRows.HasValue && outputRows != expectedRows.Value)
            {
                matrix = new double[0, 0];
                error = OdfFormulaError.NA;
                return false;
            }
            matrix = new double[outputRows, outputColumns];
            for (int row = 0; row < outputRows; row++)
            {
                for (int column = 0; column < outputColumns; column++)
                {
                    object item = transpose ? array[column, row] : array[row, column];
                    if (!FormulaCoercion.TryCoerceDouble(item, out matrix[row, column]))
                    {
                        error = OdfFormulaError.Value;
                        return false;
                    }
                }
            }
            error = 0d;
            return outputRows > 0 && outputColumns > 0;
        }
        if (!TryGetNumbers(value, out double[] values, out error))
        {
            matrix = new double[0, 0];
            return false;
        }
        if (expectedRows.HasValue && values.Length != expectedRows.Value)
        {
            matrix = new double[0, 0];
            error = OdfFormulaError.NA;
            return false;
        }
        matrix = new double[values.Length, 1];
        for (int row = 0; row < values.Length; row++)
            matrix[row, 0] = values[row];
        error = 0d;
        return true;
    }

    private static bool TryFitLinearModel(
        double[,] predictors,
        double[] outcomes,
        bool includeIntercept,
        out double[] coefficients,
        out double[,] inverseNormal,
        out int modelRank)
    {
        int rows = predictors.GetLength(0);
        int predictorCount = predictors.GetLength(1);
        int parameterCount = predictorCount + (includeIntercept ? 1 : 0);
        if (rows < 2 || outcomes.Length != rows || parameterCount == 0)
        {
            coefficients = [];
            inverseNormal = new double[0, 0];
            modelRank = 0;
            return false;
        }

        var design = new double[rows, parameterCount];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < predictorCount; column++)
                design[row, column] = predictors[row, column];
            if (includeIntercept)
                design[row, predictorCount] = 1;
        }

        if (!TrySolveLeastSquares(
            design,
            outcomes,
            out double[] fitted,
            out inverseNormal,
            out modelRank))
        {
            coefficients = [];
            inverseNormal = new double[0, 0];
            return false;
        }
        coefficients = new double[predictorCount + 1];
        for (int index = 0; index < predictorCount; index++)
            coefficients[index] = fitted[index];
        coefficients[predictorCount] = includeIntercept ? fitted[predictorCount] : 0;
        return true;
    }

    private static bool TrySolveLeastSquares(
        double[,] design,
        double[] outcomes,
        out double[] solution,
        out double[,] inverseNormal,
        out int rank)
    {
        int rows = design.GetLength(0);
        int columns = design.GetLength(1);
        var work = (double[,])design.Clone();
        var q = new double[rows, columns];
        var r = new double[columns, columns];
        var permutation = new int[columns];
        var norms = new double[columns];
        double largestNorm = 0;
        for (int column = 0; column < columns; column++)
        {
            permutation[column] = column;
            norms[column] = ColumnSquaredNorm(work, column);
            largestNorm = Math.Max(largestNorm, Math.Sqrt(norms[column]));
        }

        rank = 0;
        double tolerance = Math.Max(rows, columns) * Math.Max(1, largestNorm) * 1e-12;
        for (int step = 0; step < columns; step++)
        {
            int pivot = step;
            for (int column = step + 1; column < columns; column++)
            {
                if (norms[column] > norms[pivot])
                    pivot = column;
            }
            if (Math.Sqrt(Math.Max(0, norms[pivot])) <= tolerance)
                break;

            if (pivot != step)
            {
                SwapColumns(work, pivot, step);
                (norms[pivot], norms[step]) = (norms[step], norms[pivot]);
                (permutation[pivot], permutation[step]) =
                    (permutation[step], permutation[pivot]);
                for (int previous = 0; previous < step; previous++)
                    (r[previous, pivot], r[previous, step]) =
                        (r[previous, step], r[previous, pivot]);
            }

            double norm = Math.Sqrt(ColumnSquaredNorm(work, step));
            if (norm <= tolerance)
                break;
            r[step, step] = norm;
            for (int row = 0; row < rows; row++)
                q[row, step] = work[row, step] / norm;

            for (int column = step + 1; column < columns; column++)
            {
                double projection = 0;
                for (int row = 0; row < rows; row++)
                    projection += q[row, step] * work[row, column];
                r[step, column] = projection;
                for (int row = 0; row < rows; row++)
                    work[row, column] -= projection * q[row, step];
                norms[column] = ColumnSquaredNorm(work, column);
            }
            rank++;
        }

        if (rank == 0 || rows <= rank)
        {
            solution = [];
            inverseNormal = new double[0, 0];
            return false;
        }

        var projected = new double[rank];
        for (int column = 0; column < rank; column++)
        {
            for (int row = 0; row < rows; row++)
                projected[column] += q[row, column] * outcomes[row];
        }
        var pivotedSolution = new double[columns];
        for (int row = rank - 1; row >= 0; row--)
        {
            double value = projected[row];
            for (int column = row + 1; column < rank; column++)
                value -= r[row, column] * pivotedSolution[column];
            pivotedSolution[row] = value / r[row, row];
        }

        solution = new double[columns];
        for (int column = 0; column < columns; column++)
            solution[permutation[column]] = pivotedSolution[column];

        var inverseR = new double[rank, rank];
        for (int target = 0; target < rank; target++)
        {
            for (int row = rank - 1; row >= 0; row--)
            {
                double value = row == target ? 1 : 0;
                for (int column = row + 1; column < rank; column++)
                    value -= r[row, column] * inverseR[column, target];
                inverseR[row, target] = value / r[row, row];
            }
        }

        inverseNormal = new double[columns, columns];
        for (int left = 0; left < rank; left++)
        {
            for (int right = 0; right < rank; right++)
            {
                double value = 0;
                for (int column = 0; column < rank; column++)
                    value += inverseR[left, column] * inverseR[right, column];
                inverseNormal[permutation[left], permutation[right]] = value;
            }
        }
        return true;
    }

    private static double ColumnSquaredNorm(double[,] matrix, int column)
    {
        double result = 0;
        for (int row = 0; row < matrix.GetLength(0); row++)
            result += matrix[row, column] * matrix[row, column];
        return result;
    }

    private static void SwapColumns(double[,] matrix, int left, int right)
    {
        for (int row = 0; row < matrix.GetLength(0); row++)
            (matrix[row, left], matrix[row, right]) =
                (matrix[row, right], matrix[row, left]);
    }

    private static object[,] CreateRegressionResult(
        double[,] predictors,
        double[] outcomes,
        double[] coefficients,
        double[,] inverseNormal,
        int modelRank,
        bool includeIntercept,
        bool exponential,
        bool includeStatistics)
    {
        int predictorCount = predictors.GetLength(1);
        int resultRows = includeStatistics ? 5 : 1;
        var result = new object[resultRows, predictorCount + 1];
        for (int row = 0; row < resultRows; row++)
        {
            for (int column = 0; column <= predictorCount; column++)
                result[row, column] = OdfFormulaError.NA;
        }
        for (int column = 0; column < predictorCount; column++)
        {
            double coefficient = coefficients[predictorCount - column - 1];
            result[0, column] = exponential ? Math.Exp(coefficient) : coefficient;
        }
        result[0, predictorCount] = exponential
            ? Math.Exp(coefficients[predictorCount])
            : coefficients[predictorCount];
        if (!includeStatistics)
            return result;

        int observations = outcomes.Length;
        int parameterCount = predictorCount + (includeIntercept ? 1 : 0);
        double degreesOfFreedom = observations - modelRank;
        double mean = Mean(outcomes);
        double residualSumSquares = 0;
        double regressionSumSquares = 0;
        for (int row = 0; row < observations; row++)
        {
            double prediction = coefficients[predictorCount];
            for (int column = 0; column < predictorCount; column++)
                prediction += coefficients[column] * predictors[row, column];
            double residual = outcomes[row] - prediction;
            residualSumSquares += residual * residual;
            double regressionDelta = includeIntercept ? prediction - mean : prediction;
            regressionSumSquares += regressionDelta * regressionDelta;
        }
        double variance = residualSumSquares / degreesOfFreedom;
        for (int column = 0; column < predictorCount; column++)
        {
            int coefficientIndex = predictorCount - column - 1;
            result[1, column] = Math.Sqrt(
                Math.Max(0, variance * inverseNormal[coefficientIndex, coefficientIndex]));
        }
        if (includeIntercept)
        {
            result[1, predictorCount] = Math.Sqrt(
                Math.Max(0, variance * inverseNormal[parameterCount - 1, parameterCount - 1]));
        }

        double totalSumSquares = regressionSumSquares + residualSumSquares;
        int regressionDegreesOfFreedom = modelRank - (includeIntercept ? 1 : 0);
        result[2, 0] = totalSumSquares == 0
            ? OdfFormulaError.Div0
            : regressionSumSquares / totalSumSquares;
        result[2, 1] = Math.Sqrt(variance);
        result[3, 0] = residualSumSquares == 0 || regressionDegreesOfFreedom == 0
            ? OdfFormulaError.Div0
            : (regressionSumSquares / regressionDegreesOfFreedom) / variance;
        result[3, 1] = degreesOfFreedom;
        result[4, 0] = regressionSumSquares;
        result[4, 1] = residualSumSquares;
        return result;
    }

    private static double[,] CreateAugmentedMatrix(double[,] matrix, double[] rightHandSide)
    {
        int size = matrix.GetLength(0);
        var augmented = new double[size, size + 1];
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
                augmented[row, column] = matrix[row, column];
            augmented[row, size] = rightHandSide[row];
        }
        return augmented;
    }

    private static bool TryInvertMatrix(double[,] matrix, out double[,] inverse)
    {
        int size = matrix.GetLength(0);
        inverse = new double[size, size];
        for (int column = 0; column < size; column++)
        {
            var rightHandSide = new double[size];
            rightHandSide[column] = 1;
            if (!TrySolveLinearSystem(
                CreateAugmentedMatrix(matrix, rightHandSide),
                size,
                out double[] solution))
            {
                inverse = new double[0, 0];
                return false;
            }
            for (int row = 0; row < size; row++)
                inverse[row, column] = solution[row];
        }
        return true;
    }

    private static bool TrySolveLinearSystem(
        double[,] augmented,
        int size,
        out double[] solution)
    {
        for (int column = 0; column < size; column++)
        {
            int pivot = column;
            for (int row = column + 1; row < size; row++)
            {
                if (Math.Abs(augmented[row, column]) > Math.Abs(augmented[pivot, column]))
                    pivot = row;
            }
            if (Math.Abs(augmented[pivot, column]) < 1e-14)
            {
                solution = [];
                return false;
            }
            if (pivot != column)
            {
                for (int item = column; item <= size; item++)
                {
                    double temporary = augmented[column, item];
                    augmented[column, item] = augmented[pivot, item];
                    augmented[pivot, item] = temporary;
                }
            }
            double divisor = augmented[column, column];
            for (int item = column; item <= size; item++)
                augmented[column, item] /= divisor;
            for (int row = 0; row < size; row++)
            {
                if (row == column)
                    continue;
                double factor = augmented[row, column];
                for (int item = column; item <= size; item++)
                    augmented[row, item] -= factor * augmented[column, item];
            }
        }
        solution = new double[size];
        for (int row = 0; row < size; row++)
            solution[row] = augmented[row, size];
        return true;
    }

    private static double BesselSeries(double x, int order, bool alternating)
    {
        double term = Math.Pow(x / 2, order) / Math.Exp(LogGamma(order + 1));
        double sum = term;
        for (int k = 1; k < 200; k++)
        {
            term *= (x * x / 4) / (k * (order + k));
            if (alternating)
                term = -term;
            sum += term;
            if (Math.Abs(term) <= Math.Abs(sum) * 1e-15)
                break;
        }
        return sum;
    }

    private static double BesselY(double x, int order)
    {
        if (x > 20)
        {
            double phase = x - ((order * Math.PI / 2) + (Math.PI / 4));
            return Math.Sqrt(2 / (Math.PI * x)) * Math.Sin(phase);
        }
        const double EulerGamma = 0.5772156649015329;
        double q = x * x / 4;
        double term = 1;
        double harmonic = 0;
        double series = 0;
        double derivativeSeries = 0;
        for (int k = 1; k < 200; k++)
        {
            harmonic += 1d / k;
            term *= -q / (k * k);
            double contribution = -harmonic * term;
            series += contribution;
            derivativeSeries += (2d * k / x) * contribution;
            if (Math.Abs(contribution) <= Math.Max(1, Math.Abs(series)) * 1e-15)
                break;
        }
        double j0 = BesselSeries(x, 0, true);
        double j1 = BesselSeries(x, 1, true);
        double logarithm = Math.Log(x / 2) + EulerGamma;
        double y0 = (2 / Math.PI) * ((logarithm * j0) + series);
        if (order == 0)
            return y0;
        double y1 = -(2 / Math.PI) *
            ((j0 / x) - (logarithm * j1) + derivativeSeries);
        if (order == 1)
            return y1;
        double previous = y0;
        double current = y1;
        for (int n = 1; n < order; n++)
        {
            double next = (2d * n / x * current) - previous;
            previous = current;
            current = next;
        }
        return current;
    }

    private static double RegularizedBeta(double x, double a, double b)
    {
        if (x <= 0)
            return 0;
        if (x >= 1)
            return 1;
        double factor = Math.Exp((a * Math.Log(x)) + (b * Math.Log(1 - x)) - LogBeta(a, b));
        return x < (a + 1) / (a + b + 2)
            ? factor * BetaFraction(x, a, b) / a
            : 1 - (factor * BetaFraction(1 - x, b, a) / b);
    }

    private static double BetaFraction(double x, double a, double b)
    {
        const double Tiny = 1e-300;
        double qab = a + b;
        double qap = a + 1;
        double qam = a - 1;
        double c = 1;
        double d = 1 - (qab * x / qap);
        if (Math.Abs(d) < Tiny)
            d = Tiny;
        d = 1 / d;
        double h = d;
        for (int m = 1; m <= 200; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + (aa * d);
            if (Math.Abs(d) < Tiny)
                d = Tiny;
            c = 1 + (aa / c);
            if (Math.Abs(c) < Tiny)
                c = Tiny;
            d = 1 / d;
            h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + (aa * d);
            if (Math.Abs(d) < Tiny)
                d = Tiny;
            c = 1 + (aa / c);
            if (Math.Abs(c) < Tiny)
                c = Tiny;
            d = 1 / d;
            double delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-14)
                break;
        }
        return h;
    }

    private static double RegularizedGammaP(double a, double x)
    {
        if (x <= 0)
            return 0;
        if (x < a + 1)
        {
            double sum = 1 / a;
            double term = sum;
            for (int n = 1; n < 1000; n++)
            {
                term *= x / (a + n);
                sum += term;
                if (Math.Abs(term) < Math.Abs(sum) * 1e-15)
                    break;
            }
            return sum * Math.Exp(-x + (a * Math.Log(x)) - LogGamma(a));
        }
        double b = x + 1 - a;
        double c = 1e300;
        double d = 1 / b;
        double h = d;
        for (int i = 1; i < 1000; i++)
        {
            double an = -i * (i - a);
            b += 2;
            d = (an * d) + b;
            if (Math.Abs(d) < 1e-300)
                d = 1e-300;
            c = b + (an / c);
            if (Math.Abs(c) < 1e-300)
                c = 1e-300;
            d = 1 / d;
            double delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-15)
                break;
        }
        return 1 - (Math.Exp(-x + (a * Math.Log(x)) - LogGamma(a)) * h);
    }

    private static double LogGamma(double value)
    {
        double[] coefficients =
        [
            676.5203681218851, -1259.1392167224028, 771.32342877765313,
            -176.61502916214059, 12.507343278686905, -0.13857109526572012,
            9.9843695780195716e-6, 1.5056327351493116e-7
        ];
        if (value < 0.5)
            return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * value)) -
                LogGamma(1 - value);
        value--;
        double sum = 0.99999999999980993;
        for (int i = 0; i < coefficients.Length; i++)
            sum += coefficients[i] / (value + i + 1);
        double shifted = value + coefficients.Length - 0.5;
        return 0.5 * Math.Log(2 * Math.PI) + ((value + 0.5) * Math.Log(shifted)) -
            shifted + Math.Log(sum);
    }

    private static double LogBeta(double a, double b)
        => LogGamma(a) + LogGamma(b) - LogGamma(a + b);

    private static double Invert(
        Func<double, double> function,
        double target,
        double low,
        double high)
    {
        for (int i = 0; i < 160; i++)
        {
            double middle = low + ((high - low) / 2);
            if (function(middle) < target)
                low = middle;
            else
                high = middle;
        }
        return low + ((high - low) / 2);
    }

    private static object SolveRoot(Func<double, double> function, double guess)
    {
        double x0 = Math.Max(-0.999999, guess);
        double x1 = x0 + 0.01;
        for (int i = 0; i < 100; i++)
        {
            double f0 = function(x0);
            double f1 = function(x1);
            if (!IsFinite(f0) || !IsFinite(f1) || Math.Abs(f1 - f0) < 1e-15)
                break;
            double next = x1 - (f1 * (x1 - x0) / (f1 - f0));
            next = Math.Max(-0.999999, next);
            if (Math.Abs(next - x1) < 1e-10)
                return next;
            x0 = x1;
            x1 = next;
        }
        return OdfFormulaError.Num;
    }

    private static double InverseNormal(double probability)
    {
        double low = -10;
        double high = 10;
        return Invert(NormalCdf, probability, low, high);
    }

    private static double NormalCdf(double value)
        => 0.5 * (1 + Erf(value / Math.Sqrt(2)));

    private static double Erf(double value)
    {
        double sign = Math.Sign(value);
        double x = Math.Abs(value);
        double t = 1 / (1 + (0.3275911 * x));
        double polynomial = t * (0.254829592 + (t * (-0.284496736 +
            (t * (1.421413741 + (t * (-1.453152027 + (t * 1.061405429))))))));
        return sign * (1 - (polynomial * Math.Exp(-x * x)));
    }

    private static double FCdf(double value, double d1, double d2)
        => RegularizedBeta((d1 * value) / ((d1 * value) + d2), d1 / 2, d2 / 2);

    private static double Combination(int n, int k)
    {
        if (k < 0 || k > n)
            return 0;
        k = Math.Min(k, n - k);
        double result = 1;
        for (int i = 1; i <= k; i++)
            result *= (n - k + i) / (double)i;
        return result;
    }

    private static (double Slope, double Intercept) LinearFit(double[] xs, double[] ys)
    {
        double meanX = Mean(xs);
        double meanY = Mean(ys);
        double numerator = 0;
        double denominator = 0;
        for (int i = 0; i < xs.Length; i++)
        {
            numerator += (xs[i] - meanX) * (ys[i] - meanY);
            denominator += Math.Pow(xs[i] - meanX, 2);
        }
        double slope = denominator == 0 ? 0 : numerator / denominator;
        return (slope, meanY - (slope * meanX));
    }

    private static double StandardDeviation(double[] values, bool population)
    {
        if (values.Length == 0 || (!population && values.Length < 2))
            return double.NaN;
        double mean = Mean(values);
        double sum = 0;
        foreach (double value in values)
            sum += Math.Pow(value - mean, 2);
        return Math.Sqrt(sum / (population ? values.Length : values.Length - 1));
    }

    private static double Mean(double[] values) => Sum(values) / values.Length;

    private static double Sum(IEnumerable<double> values)
    {
        double result = 0;
        foreach (double value in values)
            result += value;
        return result;
    }

    private static double Product(IEnumerable<double> values)
    {
        double result = 1;
        foreach (double value in values)
            result *= value;
        return result;
    }

    private static double Min(IEnumerable<double> values)
    {
        double result = double.PositiveInfinity;
        foreach (double value in values)
            result = Math.Min(result, value);
        return result;
    }

    private static double Max(IEnumerable<double> values)
    {
        double result = double.NegativeInfinity;
        foreach (double value in values)
            result = Math.Max(result, value);
        return result;
    }

    private static bool TryCompare(object left, object right, out int comparison)
    {
        if (FormulaCoercion.TryCoerceDouble(left, out double leftNumber) &&
            FormulaCoercion.TryCoerceDouble(right, out double rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }
        if (left is string leftText && right is string rightText)
        {
            comparison = StringComparer.OrdinalIgnoreCase.Compare(leftText, rightText);
            return true;
        }
        comparison = 0;
        return false;
    }

    private static bool TryDate(double serial, out DateTime date)
    {
        try
        {
            date = DateTime.FromOADate(serial);
            return true;
        }
        catch (ArgumentException)
        {
            date = default;
            return false;
        }
    }

    private static int Days360(DateTime start, DateTime end, bool european)
    {
        int startDay = european ? Math.Min(start.Day, 30) :
            start.Day == 31 ? 30 : start.Day;
        int endDay = european ? Math.Min(end.Day, 30) :
            end.Day == 31 && startDay == 30 ? 30 : end.Day;
        return ((end.Year - start.Year) * 360) +
            ((end.Month - start.Month) * 30) + endDay - startDay;
    }

    private static double YearFraction(DateTime start, DateTime end, int basis)
    {
        if (end < start)
            return -YearFraction(end, start, basis);
        return basis switch
        {
            0 => Days360(start, end, false) / 360d,
            1 => ActualActual(start, end),
            2 => (end - start).TotalDays / 360d,
            3 => (end - start).TotalDays / 365d,
            4 => Days360(start, end, true) / 360d,
            _ => double.NaN
        };
    }

    private static double ActualActual(DateTime start, DateTime end)
    {
        if (start.Year == end.Year)
            return (end - start).TotalDays / (DateTime.IsLeapYear(start.Year) ? 366 : 365);
        double result = (new DateTime(start.Year + 1, 1, 1) - start).TotalDays /
            (DateTime.IsLeapYear(start.Year) ? 366 : 365);
        for (int year = start.Year + 1; year < end.Year; year++)
            result++;
        result += (end - new DateTime(end.Year, 1, 1)).TotalDays /
            (DateTime.IsLeapYear(end.Year) ? 366 : 365);
        return result;
    }

    private static double DayCount(DateTime start, DateTime end, int basis)
        => basis is 0 or 4
            ? Days360(start, end, basis == 4)
            : (end - start).TotalDays;

    private static double Payment(double rate, double periods, double present, double future, int type)
    {
        if (rate == 0)
            return -(present + future) / periods;
        double factor = Math.Pow(1 + rate, periods);
        return -(present * factor + future) * rate /
            ((factor - 1) * (1 + (rate * type)));
    }

    private static double CalculateBondPrice(
        DateTime settlement,
        DateTime maturity,
        double couponRate,
        double yield,
        double redemption,
        int frequency,
        int basis,
        DateTime? issue,
        DateTime? firstCoupon,
        DateTime? lastInterest)
    {
        if (yield <= -frequency || couponRate < 0 || redemption <= 0)
            return double.NaN;
        int months = 12 / frequency;
        double coupon = redemption * couponRate / frequency;
        if (lastInterest.HasValue)
        {
            DateTime previousNormal = maturity.AddMonths(-months);
            double normalDays = Math.Max(1, DayCount(previousNormal, maturity, basis));
            double stubDays = DayCount(lastInterest.Value, maturity, basis);
            double accruedDays = DayCount(lastInterest.Value, settlement, basis);
            double remainingDays = DayCount(settlement, maturity, basis);
            double exponent = remainingDays / normalDays;
            double finalCash = redemption + (coupon * stubDays / normalDays);
            return (finalCash / Math.Pow(1 + (yield / frequency), exponent)) -
                (coupon * accruedDays / normalDays);
        }
        if (issue.HasValue && firstCoupon.HasValue)
        {
            DateTime previousNormal = firstCoupon.Value.AddMonths(-months);
            double normalDays = Math.Max(
                1,
                DayCount(previousNormal, firstCoupon.Value, basis));
            double firstCouponDays = DayCount(issue.Value, firstCoupon.Value, basis);
            double accruedDays = DayCount(issue.Value, settlement, basis);
            double firstExponent = DayCount(
                settlement,
                firstCoupon.Value,
                basis) / normalDays;
            double price = coupon * firstCouponDays / normalDays /
                Math.Pow(1 + (yield / frequency), firstExponent);
            DateTime paymentDate = firstCoupon.Value.AddMonths(months);
            int period = 1;
            while (paymentDate <= maturity)
            {
                double cash = coupon + (paymentDate == maturity ? redemption : 0);
                price += cash / Math.Pow(
                    1 + (yield / frequency),
                    firstExponent + period);
                paymentDate = paymentDate.AddMonths(months);
                period++;
            }
            return price - (coupon * accruedDays / normalDays);
        }
        DateTime previous = maturity;
        while (previous > settlement)
            previous = previous.AddMonths(-months);
        DateTime next = previous.AddMonths(months);
        double couponDays = Math.Max(1, DayCount(previous, next, basis));
        double accrued = DayCount(previous, settlement, basis);
        double remaining = DayCount(settlement, next, basis);
        double firstPeriod = remaining / couponDays;
        double standardPrice = 0;
        DateTime date = next;
        int index = 0;
        while (date <= maturity)
        {
            double cash = coupon + (date == maturity ? redemption : 0);
            standardPrice += cash / Math.Pow(
                1 + (yield / frequency),
                firstPeriod + index);
            date = date.AddMonths(months);
            index++;
        }
        return standardPrice - (coupon * accrued / couponDays);
    }

    private static double Integrate(Func<double, double> function, double start, double end)
    {
        const int Steps = 4096;
        double width = (end - start) / Steps;
        double sum = 0;
        for (int i = 0; i <= Steps; i++)
        {
            double weight = i == 0 || i == Steps ? 1 : i % 2 == 0 ? 2 : 4;
            sum += weight * function(start + (i * width));
        }
        return sum * width / 3;
    }

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);
}
