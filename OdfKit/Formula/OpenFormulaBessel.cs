using System;

namespace OdfKit.Formula;

/// <summary>
/// 依 NIST DLMF 級數、漸近展開、遞迴與積分表示式計算整數階 Bessel 函式。
/// </summary>
internal static class OpenFormulaBessel
{
    private const double EulerGamma = 0.577215664901532860606512090082402431;
    private const double LogDoubleMax = 709.782712893384;
    private const double LogDoubleMin = -745.133219101941;

    internal static double I(double x, int order)
    {
        if (x == 0)
            return order == 0 ? 1 : 0;

        double sign = x < 0 && (order & 1) != 0 ? -1 : 1;
        double absoluteX = Math.Abs(x);
        if (absoluteX / 2 == 0)
            return order == 0 ? 1 : sign * 0;
        double logTerm = (order * Math.Log(absoluteX / 2)) -
            LogGamma(order + 1);
        double logRatioNumerator = 2 * Math.Log(absoluteX / 2);
        double maximumLog = logTerm;
        double scaledSum = 1;
        bool decreasing = false;

        for (int k = 1; k < 10000; k++)
        {
            double previousLog = logTerm;
            logTerm += logRatioNumerator - Math.Log(k) - Math.Log(order + k);
            if (logTerm > maximumLog)
            {
                scaledSum = (scaledSum * Math.Exp(maximumLog - logTerm)) + 1;
                maximumLog = logTerm;
            }
            else
            {
                scaledSum += Math.Exp(logTerm - maximumLog);
            }

            decreasing |= logTerm < previousLog;
            if (decreasing && logTerm - maximumLog < -38)
                break;
        }

        double logResult = maximumLog + Math.Log(scaledSum);
        if (logResult > LogDoubleMax)
            return double.PositiveInfinity;
        if (logResult < LogDoubleMin)
            return 0;
        return sign * Math.Exp(logResult);
    }

    internal static double J(double x, int order)
    {
        if (x == 0)
            return order == 0 ? 1 : 0;

        double sign = x < 0 && (order & 1) != 0 ? -1 : 1;
        double absoluteX = Math.Abs(x);
        if (absoluteX / 2 == 0)
            return order == 0 ? 1 : sign * 0;
        double j0 = absoluteX >= 18
            ? HankelExpansion(absoluteX, 0)
            : JSeries(absoluteX, 0);
        if (order == 0)
            return sign * j0;
        double j1 = absoluteX >= 18
            ? HankelExpansion(absoluteX, 1)
            : JSeries(absoluteX, 1);
        if (order == 1)
            return sign * j1;

        double value;
        if (order <= absoluteX)
        {
            double previous = j0;
            double current = j1;
            for (int n = 1; n < order; n++)
            {
                double next = ((2d * n / absoluteX) * current) - previous;
                previous = current;
                current = next;
            }

            value = current;
        }
        else
        {
            value = JBackward(absoluteX, order, j0, j1);
        }

        return sign * value;
    }

    internal static double K(double x, int order)
    {
        if (x <= 0)
            return double.NaN;

        double ratio = order / x;
        double peak = order == 0
            ? 0
            : Math.Log(ratio + Math.Sqrt((ratio * ratio) + 1));
        double upper = Math.Max(12, peak + 12);
        double Integrand(double t)
        {
            double exponent = (-x * Math.Cosh(t)) + LogCosh(order * t);
            if (exponent < LogDoubleMin)
                return 0;
            if (exponent > LogDoubleMax)
                return double.PositiveInfinity;
            return Math.Exp(exponent);
        }

        double left = peak > 0
            ? AdaptiveSimpson(Integrand, 0, peak, 2e-14, 24)
            : 0;
        double right = AdaptiveSimpson(Integrand, peak, upper, 2e-14, 24);
        return left + right;
    }

    internal static double Y(double x, int order)
    {
        if (x == 0)
            return double.NaN;

        double sign = x < 0 && (order & 1) != 0 ? -1 : 1;
        double absoluteX = Math.Abs(x);
        double value;
        if (absoluteX >= 18)
        {
            value = YIntegral(absoluteX, order);
        }
        else
        {
            (double y0, double y1) = YBaseSeries(absoluteX);
            if (order == 0)
            {
                value = y0;
            }
            else if (order == 1)
            {
                value = y1;
            }
            else
            {
                double previous = y0;
                double current = y1;
                for (int n = 1; n < order; n++)
                {
                    double next = ((2d * n / absoluteX) * current) - previous;
                    previous = current;
                    current = next;
                }

                value = current;
            }
        }

        return sign * value;
    }

    private static double YIntegral(double x, int order)
    {
        int intervals = Math.Max(
            16,
            Math.Min(4096, (int)Math.Ceiling((x + order) / 4)));
        double intervalWidth = Math.PI / intervals;
        double oscillatory = 0;
        double compensation = 0;
        double OscillatoryIntegrand(double theta) =>
            Math.Sin((x * Math.Sin(theta)) - (order * theta));
        for (int interval = 0; interval < intervals; interval++)
        {
            double start = interval * intervalWidth;
            double end = start + intervalWidth;
            double contribution = AdaptiveSimpson(
                OscillatoryIntegrand,
                start,
                end,
                2e-14,
                16);
            double adjusted = contribution - compensation;
            double next = oscillatory + adjusted;
            compensation = (next - oscillatory) - adjusted;
            oscillatory = next;
        }

        double peak = order > x
            ? Math.Log(
                (order / x) +
                Math.Sqrt(((double)order * order / (x * x)) - 1))
            : 0;
        double upper = Math.Max(8, peak + 8);
        double TailIntegrand(double t)
        {
            double decay = -x * Math.Sinh(t);
            double positiveExponent = decay + (order * t);
            double negativeExponent = decay - (order * t);
            double positive = positiveExponent > LogDoubleMax
                ? double.PositiveInfinity
                : positiveExponent < LogDoubleMin ? 0 : Math.Exp(positiveExponent);
            double negative = negativeExponent < LogDoubleMin
                ? 0
                : Math.Exp(negativeExponent);
            return (order & 1) == 0
                ? positive + negative
                : positive - negative;
        }

        double tail = peak > 0
            ? AdaptiveSimpson(TailIntegrand, 0, peak, 2e-14, 24) +
                AdaptiveSimpson(TailIntegrand, peak, upper, 2e-14, 24)
            : AdaptiveSimpson(TailIntegrand, 0, upper, 2e-14, 24);
        return (oscillatory - tail) / Math.PI;
    }

    private static double JSeries(double x, int order)
    {
        double logInitial = (order * Math.Log(x / 2)) - LogGamma(order + 1);
        if (logInitial < LogDoubleMin)
            return 0;

        double term = Math.Exp(logInitial);
        double sum = term;
        double compensation = 0;
        double square = x * x / 4;
        for (int k = 1; k < 10000; k++)
        {
            term *= -square / (k * (double)(order + k));
            double adjusted = term - compensation;
            double next = sum + adjusted;
            compensation = (next - sum) - adjusted;
            sum = next;
            if (Math.Abs(term) <= Math.Max(1, Math.Abs(sum)) * 2e-16)
                break;
        }

        return sum;
    }

    private static double JBackward(
        double x,
        int order,
        double j0,
        double j1)
    {
        int start = order + Math.Max(32, (int)Math.Ceiling(Math.Sqrt(40d * order)));
        double next = 0;
        double current = 1;
        double target = 0;
        for (int n = start; n >= 1; n--)
        {
            double previous = ((2d * n / x) * current) - next;
            next = current;
            current = previous;
            if (Math.Max(Math.Abs(current), Math.Abs(next)) > 1e200)
            {
                current *= 1e-200;
                next *= 1e-200;
                target *= 1e-200;
            }

            if (n - 1 == order)
                target = current;
        }

        double scale = Math.Abs(j0) >= Math.Abs(j1)
            ? j0 / current
            : j1 / next;
        return target * scale;
    }

    private static (double Y0, double Y1) YBaseSeries(double x)
    {
        double q = x * x / 4;
        double term = 1;
        double harmonic = 0;
        double series = 0;
        double derivativeSeries = 0;
        for (int k = 1; k < 10000; k++)
        {
            harmonic += 1d / k;
            term *= -q / (k * (double)k);
            double contribution = -harmonic * term;
            series += contribution;
            derivativeSeries += (2d * k / x) * contribution;
            if (Math.Abs(contribution) <= Math.Max(1, Math.Abs(series)) * 2e-16)
                break;
        }

        double j0 = JSeries(x, 0);
        double j1 = JSeries(x, 1);
        double logarithm = Math.Log(x / 2) + EulerGamma;
        double y0 = (2 / Math.PI) * ((logarithm * j0) + series);
        double y1 = -(2 / Math.PI) *
            ((j0 / x) - (logarithm * j1) + derivativeSeries);
        return (y0, y1);
    }

    private static double HankelExpansion(double x, int order)
    {
        double mu = 4d * order * order;
        double even = 1;
        double odd = 0;
        double coefficient = 1;
        double inversePower = 1;
        double previousMagnitude = double.PositiveInfinity;
        for (int k = 1; k <= 18; k++)
        {
            coefficient *= (mu - Math.Pow((2 * k) - 1, 2)) / (k * 8d);
            inversePower /= x;
            double term = coefficient * inversePower;
            double magnitude = Math.Abs(term);
            if (magnitude > previousMagnitude)
                break;
            previousMagnitude = magnitude;

            int pair = k / 2;
            double sign = (pair & 1) == 0 ? 1 : -1;
            if ((k & 1) == 0)
                even += sign * term;
            else
                odd += sign * term;
        }

        double phase = x - (order * Math.PI / 2) - (Math.PI / 4);
        double scale = Math.Sqrt(2 / (Math.PI * x));
        return scale * ((Math.Cos(phase) * even) - (Math.Sin(phase) * odd));
    }

    private static double LogCosh(double value)
    {
        double absolute = Math.Abs(value);
        return absolute > 20
            ? absolute - Math.Log(2)
            : Math.Log(Math.Cosh(value));
    }

    private static double AdaptiveSimpson(
        Func<double, double> function,
        double start,
        double end,
        double tolerance,
        int depth)
    {
        if (start == end)
            return 0;
        double middle = start + ((end - start) / 2);
        double startValue = function(start);
        double middleValue = function(middle);
        double endValue = function(end);
        double whole = Simpson(start, end, startValue, middleValue, endValue);
        double absoluteTolerance = Math.Max(1e-300, Math.Abs(whole) * tolerance);
        return AdaptiveSimpsonCore(
            function,
            start,
            end,
            startValue,
            middleValue,
            endValue,
            whole,
            absoluteTolerance,
            depth);
    }

    private static double AdaptiveSimpsonCore(
        Func<double, double> function,
        double start,
        double end,
        double startValue,
        double middleValue,
        double endValue,
        double whole,
        double tolerance,
        int depth)
    {
        double middle = start + ((end - start) / 2);
        double leftMiddle = start + ((middle - start) / 2);
        double rightMiddle = middle + ((end - middle) / 2);
        double leftMiddleValue = function(leftMiddle);
        double rightMiddleValue = function(rightMiddle);
        double left = Simpson(
            start,
            middle,
            startValue,
            leftMiddleValue,
            middleValue);
        double right = Simpson(
            middle,
            end,
            middleValue,
            rightMiddleValue,
            endValue);
        double difference = left + right - whole;
        if (depth == 0 ||
            double.IsNaN(difference) ||
            double.IsInfinity(difference) ||
            Math.Abs(difference) <= 15 * tolerance)
        {
            return left + right + (difference / 15);
        }

        return AdaptiveSimpsonCore(
                function,
                start,
                middle,
                startValue,
                leftMiddleValue,
                middleValue,
                left,
                tolerance / 2,
                depth - 1) +
            AdaptiveSimpsonCore(
                function,
                middle,
                end,
                middleValue,
                rightMiddleValue,
                endValue,
                right,
                tolerance / 2,
                depth - 1);
    }

    private static double Simpson(
        double start,
        double end,
        double startValue,
        double middleValue,
        double endValue) =>
        (end - start) * (startValue + (4 * middleValue) + endValue) / 6;

    private static double LogGamma(double value)
    {
        double[] coefficients =
        [
            676.5203681218851, -1259.1392167224028, 771.32342877765313,
            -176.61502916214059, 12.507343278686905, -0.13857109526572012,
            9.9843695780195716e-6, 1.5056327351493116e-7
        ];
        value--;
        double sum = 0.99999999999980993;
        for (int index = 0; index < coefficients.Length; index++)
            sum += coefficients[index] / (value + index + 1);
        double shifted = value + coefficients.Length - 0.5;
        return (0.5 * Math.Log(2 * Math.PI)) +
            ((value + 0.5) * Math.Log(shifted)) -
            shifted +
            Math.Log(sum);
    }
}
