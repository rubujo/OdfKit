using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Math Functions - Standard

    private static object EvaluateAbs(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        double digits = 0;
        if (arguments.Count == 2)
        {
            var digitsVal = arguments[1].Evaluate(context);
            if (digitsVal is OdfFormulaError err2)
                return err2;
            if (!FormulaCoercion.TryCoerceDouble(digitsVal, out digits))
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

        if (!FormulaCoercion.TryCoerceDouble(val1, out double n) || !FormulaCoercion.TryCoerceDouble(val2, out double d))
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

        if (!FormulaCoercion.TryCoerceDouble(val1, out double b) || !FormulaCoercion.TryCoerceDouble(val2, out double e))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;
        if (num <= 0)
            return OdfFormulaError.Num;

        double baseVal = 10;
        if (arguments.Count == 2)
        {
            var baseObject = arguments[1].Evaluate(context);
            if (baseObject is OdfFormulaError err2)
                return err2;
            if (!FormulaCoercion.TryCoerceDouble(baseObject, out baseVal))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        if (arguments.Count == 1)
        {
            return Math.Ceiling(num);
        }

        var sigVal = arguments[1].Evaluate(context);
        if (sigVal is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(sigVal, out double significance))
            return OdfFormulaError.Value;
        if (significance == 0.0)
            return 0.0;

        int mode = 1;
        if (arguments.Count == 3)
        {
            var modeVal = arguments[2].Evaluate(context);
            if (modeVal is OdfFormulaError err3)
                return err3;
            if (!FormulaCoercion.TryCoerceDouble(modeVal, out double m))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        if (arguments.Count == 1)
        {
            return Math.Floor(num);
        }

        var sigVal = arguments[1].Evaluate(context);
        if (sigVal is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(sigVal, out double significance))
            return OdfFormulaError.Value;
        if (significance == 0.0)
            return 0.0;

        int mode = 1;
        if (arguments.Count == 3)
        {
            var modeVal = arguments[2].Evaluate(context);
            if (modeVal is OdfFormulaError err3)
                return err3;
            if (!FormulaCoercion.TryCoerceDouble(modeVal, out double m))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double num))
            return OdfFormulaError.Value;

        double digits = 0;
        if (arguments.Count == 2)
        {
            var digitsVal = arguments[1].Evaluate(context);
            if (digitsVal is OdfFormulaError err2)
                return err2;
            if (!FormulaCoercion.TryCoerceDouble(digitsVal, out digits))
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
