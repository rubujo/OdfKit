using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Math Functions


    private static object EvaluateInt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Floor(d);
    }

    private static object EvaluateSign(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return (double)Math.Sign(d);
    }

    private static object EvaluateOdd(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        if (d == 0)
            return 1.0;

        double absD = Math.Abs(d);
        double ceilD = Math.Ceiling(absD);
        long n = (long)ceilD;
        if (n % 2 == 0)
            n++;
        return (double)(Math.Sign(d) * n);
    }

    private static object EvaluateEven(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        if (d == 0)
            return 0.0;

        double absD = Math.Abs(d);
        double ceilD = Math.Ceiling(absD);
        long n = (long)ceilD;
        if (n % 2 != 0)
            n++;
        return (double)(Math.Sign(d) * n);
    }

    private static object EvaluateProduct(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
            return 0.0;
        double prod = 1.0;
        bool hasValue = false;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;
            foreach (var item in FlattenValues(val))
            {
                if (TryCoerceDouble(item, out double d))
                {
                    prod *= d;
                    hasValue = true;
                }
            }
        }
        return hasValue ? prod : 0.0;
    }

    private static object EvaluateFact(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d) || d < 0)
            return OdfFormulaError.Value;
        int n = (int)d;
        double fact = 1.0;
        for (int i = 1; i <= n; i++)
            fact *= i;
        return fact;
    }

    private static object EvaluateMRound(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var numVal = arguments[0].Evaluate(context);
        var multVal = arguments[1].Evaluate(context);

        if (numVal is OdfFormulaError err1)
            return err1;
        if (multVal is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(numVal, out double number) || !TryCoerceDouble(multVal, out double multiple))
            return OdfFormulaError.Value;
        if (multiple == 0)
            return 0.0;
        if (Math.Sign(number) != Math.Sign(multiple))
            return OdfFormulaError.Num;

        return Math.Round(number / multiple) * multiple;
    }

    private static object EvaluateRoundUp(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var numVal = arguments[0].Evaluate(context);
        var digitsVal = arguments[1].Evaluate(context);

        if (numVal is OdfFormulaError err1)
            return err1;
        if (digitsVal is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(numVal, out double number) || !TryCoerceDouble(digitsVal, out double digits))
            return OdfFormulaError.Value;
        double scale = Math.Pow(10, (int)digits);
        if (number >= 0)
            return Math.Ceiling(number * scale) / scale;
        else
            return Math.Floor(number * scale) / scale;
    }

    private static object EvaluateRoundDown(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var numVal = arguments[0].Evaluate(context);
        var digitsVal = arguments[1].Evaluate(context);

        if (numVal is OdfFormulaError err1)
            return err1;
        if (digitsVal is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(numVal, out double number) || !TryCoerceDouble(digitsVal, out double digits))
            return OdfFormulaError.Value;
        double scale = Math.Pow(10, (int)digits);
        if (number >= 0)
            return Math.Floor(number * scale) / scale;
        else
            return Math.Ceiling(number * scale) / scale;
    }

    private static readonly Random RandGenerator = new();

    private static object EvaluateRand(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0)
            return OdfFormulaError.Value;
        lock (RandGenerator)
        {
            return RandGenerator.NextDouble();
        }
    }

    private static object EvaluateRandBetween(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var bottomVal = arguments[0].Evaluate(context);
        var topVal = arguments[1].Evaluate(context);

        if (bottomVal is OdfFormulaError err1)
            return err1;
        if (topVal is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(bottomVal, out double bottom) || !TryCoerceDouble(topVal, out double top))
            return OdfFormulaError.Value;
        if (bottom > top)
            return OdfFormulaError.Num;

        lock (RandGenerator)
        {
            return (double)RandGenerator.Next((int)bottom, (int)top + 1);
        }
    }

    private static object EvaluateAsin(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d) || d < -1 || d > 1)
            return OdfFormulaError.Value;
        return Math.Asin(d);
    }

    private static object EvaluateAcos(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d) || d < -1 || d > 1)
            return OdfFormulaError.Value;
        return Math.Acos(d);
    }

    private static object EvaluateAtan(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Atan(d);
    }

    private static object EvaluateAtan2(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var xVal = arguments[0].Evaluate(context);
        var yVal = arguments[1].Evaluate(context);

        if (xVal is OdfFormulaError err1)
            return err1;
        if (yVal is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(xVal, out double x) || !TryCoerceDouble(yVal, out double y))
            return OdfFormulaError.Value;
        if (x == 0 && y == 0)
            return OdfFormulaError.Div0;
        return Math.Atan2(y, x);
    }

    private static object EvaluateLog10(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d) || d <= 0)
            return OdfFormulaError.Value;
        return Math.Log10(d);
    }

    private static object EvaluateSumProduct(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
            return OdfFormulaError.Value;

        var arrays = new List<object[,]>();
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            if (val is object[,] arr)
            {
                arrays.Add(arr);
            }
            else
            {
                var single = new object[1, 1];
                single[0, 0] = val;
                arrays.Add(single);
            }
        }

        int rows = arrays[0].GetLength(0);
        int cols = arrays[0].GetLength(1);

        for (int i = 1; i < arrays.Count; i++)
        {
            if (arrays[i].GetLength(0) != rows || arrays[i].GetLength(1) != cols)
            {
                return OdfFormulaError.Value;
            }
        }

        double sum = 0.0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double term = 1.0;
                bool hasNum = true;
                for (int i = 0; i < arrays.Count; i++)
                {
                    object cellVal = arrays[i][r, c];
                    if (TryCoerceDouble(cellVal, out double d))
                    {
                        term *= d;
                    }
                    else
                    {
                        hasNum = false;
                        break;
                    }
                }
                if (hasNum)
                {
                    sum += term;
                }
            }
        }
        return sum;
    }

    private static object EvaluateAbs(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Abs(d);
    }

    private static object EvaluateSqrt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        if (d < 0)
            return OdfFormulaError.Num;
        return Math.Sqrt(d);
    }

    private static object EvaluateRound(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        double digits = 0;
        if (arguments.Count == 2)
        {
            var digitsVal = arguments[1].Evaluate(context);
            if (digitsVal is OdfFormulaError err2)
                return err2;
            if (!TryCoerceDouble(digitsVal, out digits))
                return OdfFormulaError.Value;
        }

        int count = (int)digits;
        if (count >= 0)
        {
            return Math.Round(num, count, MidpointRounding.AwayFromZero);
        }
        else
        {
            double factor = Math.Pow(10, -count);
            return Math.Round(num / factor, 0, MidpointRounding.AwayFromZero) * factor;
        }
    }

    private static object EvaluateMod(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var val1 = arguments[0].Evaluate(context);
        var val2 = arguments[1].Evaluate(context);
        if (val1 is OdfFormulaError err1)
            return err1;
        if (val2 is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(val1, out double n) || !TryCoerceDouble(val2, out double d))
            return OdfFormulaError.Value;

        if (d == 0)
            return OdfFormulaError.Div0;
        return n - d * Math.Floor(n / d);
    }

    private static object EvaluatePower(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var val1 = arguments[0].Evaluate(context);
        var val2 = arguments[1].Evaluate(context);
        if (val1 is OdfFormulaError err1)
            return err1;
        if (val2 is OdfFormulaError err2)
            return err2;

        if (!TryCoerceDouble(val1, out double b) || !TryCoerceDouble(val2, out double e))
            return OdfFormulaError.Value;

        if (b < 0 && Math.Abs(e - (int)e) > 1e-9)
            return OdfFormulaError.Num;
        if (b == 0 && e < 0)
            return OdfFormulaError.Div0;
        return Math.Pow(b, e);
    }

    private static object EvaluateLn(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        if (d <= 0)
            return OdfFormulaError.Num;
        return Math.Log(d);
    }

    private static object EvaluateLog(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;
        if (num <= 0)
            return OdfFormulaError.Num;

        double baseVal = 10;
        if (arguments.Count == 2)
        {
            var baseObject = arguments[1].Evaluate(context);
            if (baseObject is OdfFormulaError err2)
                return err2;
            if (!TryCoerceDouble(baseObject, out baseVal))
                return OdfFormulaError.Value;
            if (baseVal <= 0 || baseVal == 1)
                return OdfFormulaError.Num;
        }

        return Math.Log(num, baseVal);
    }

    private static object EvaluateExp(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Exp(d);
    }

    private static object EvaluateCeiling(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        if (arguments.Count == 1)
        {
            return Math.Ceiling(num);
        }

        var sigVal = arguments[1].Evaluate(context);
        if (sigVal is OdfFormulaError err2)
            return err2;
        if (!TryCoerceDouble(sigVal, out double significance))
            return OdfFormulaError.Value;
        if (significance == 0.0)
            return 0.0;

        int mode = 1;
        if (arguments.Count == 3)
        {
            var modeVal = arguments[2].Evaluate(context);
            if (modeVal is OdfFormulaError err3)
                return err3;
            if (!TryCoerceDouble(modeVal, out double m))
                return OdfFormulaError.Value;
            mode = (int)m;
        }

        if (num > 0 && significance < 0)
            return OdfFormulaError.Num;
        if (num == 0.0)
            return 0.0;

        if (mode == 0)
        {
            return Math.Ceiling(num / significance) * significance;
        }
        else
        {
            if (num < 0)
                return Math.Floor(num / significance) * significance;
            else
                return Math.Ceiling(num / significance) * significance;
        }
    }

    private static object EvaluateFloor(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        if (arguments.Count == 1)
        {
            return Math.Floor(num);
        }

        var sigVal = arguments[1].Evaluate(context);
        if (sigVal is OdfFormulaError err2)
            return err2;
        if (!TryCoerceDouble(sigVal, out double significance))
            return OdfFormulaError.Value;
        if (significance == 0.0)
            return 0.0;

        int mode = 1;
        if (arguments.Count == 3)
        {
            var modeVal = arguments[2].Evaluate(context);
            if (modeVal is OdfFormulaError err3)
                return err3;
            if (!TryCoerceDouble(modeVal, out double m))
                return OdfFormulaError.Value;
            mode = (int)m;
        }

        if (num > 0 && significance < 0)
            return OdfFormulaError.Num;
        if (num == 0.0)
            return 0.0;

        if (mode == 0)
        {
            return Math.Floor(num / significance) * significance;
        }
        else
        {
            if (num < 0)
                return Math.Ceiling(num / significance) * significance;
            else
                return Math.Floor(num / significance) * significance;
        }
    }

    private static object EvaluatePi(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0)
            return OdfFormulaError.Value;
        return Math.PI;
    }

    private static object EvaluateDegrees(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return d * 180.0 / Math.PI;
    }

    private static object EvaluateRadians(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return d * Math.PI / 180.0;
    }

    private static object EvaluateSin(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Sin(d);
    }

    private static object EvaluateCos(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Cos(d);
    }

    private static object EvaluateTan(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        return Math.Tan(d);
    }

    private static object EvaluateTrunc(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        double digits = 0;
        if (arguments.Count == 2)
        {
            var digitsVal = arguments[1].Evaluate(context);
            if (digitsVal is OdfFormulaError err2)
                return err2;
            if (!TryCoerceDouble(digitsVal, out digits))
                return OdfFormulaError.Value;
        }

        int count = (int)digits;
        double factor = Math.Pow(10, count);
        if (num < 0)
            return Math.Ceiling(num * factor) / factor;
        else
            return Math.Floor(num * factor) / factor;
    }


    #endregion
}
