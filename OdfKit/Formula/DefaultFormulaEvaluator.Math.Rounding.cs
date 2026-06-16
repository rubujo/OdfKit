using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Math Functions - Rounding & Random

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

    #endregion
}
