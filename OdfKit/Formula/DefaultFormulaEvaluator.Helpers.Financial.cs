using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Helpers & Coercion - Financial

    private static object EvaluateSln(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;
        var costVal = arguments[0].Evaluate(context);
        var salvageVal = arguments[1].Evaluate(context);
        var lifeVal = arguments[2].Evaluate(context);

        if (!FormulaCoercion.TryCoerceDouble(costVal, out double cost) ||
            !FormulaCoercion.TryCoerceDouble(salvageVal, out double salvage) ||
            !FormulaCoercion.TryCoerceDouble(lifeVal, out double life))
        {
            return OdfFormulaError.Value;
        }

        if (life == 0)
            return OdfFormulaError.Div0;
        return (cost - salvage) / life;
    }

    private static object EvaluateDdb(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 4 || arguments.Count > 5)
            return OdfFormulaError.Value;
        var costVal = arguments[0].Evaluate(context);
        var salvageVal = arguments[1].Evaluate(context);
        var lifeVal = arguments[2].Evaluate(context);
        var periodVal = arguments[3].Evaluate(context);

        if (!FormulaCoercion.TryCoerceDouble(costVal, out double cost) ||
            !FormulaCoercion.TryCoerceDouble(salvageVal, out double salvage) ||
            !FormulaCoercion.TryCoerceDouble(lifeVal, out double life) ||
            !FormulaCoercion.TryCoerceDouble(periodVal, out double period))
        {
            return OdfFormulaError.Value;
        }

        double factor = 2.0;
        if (arguments.Count == 5)
        {
            var factorVal = arguments[4].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(factorVal, out factor))
                return OdfFormulaError.Value;
        }

        if (cost < 0 || salvage < 0 || life <= 0 || period <= 0 || period > life || factor <= 0)
            return OdfFormulaError.Num;

        double bookValue = cost;
        double dep = 0;
        for (int i = 1; i <= (int)period; i++)
        {
            dep = bookValue * factor / life;
            if (bookValue - dep < salvage)
            {
                dep = bookValue - salvage;
            }
            if (dep < 0)
                dep = 0;
            bookValue -= dep;
        }
        return dep;
    }

    private static object EvaluateIrr(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var valNode = arguments[0].Evaluate(context);
        if (valNode is OdfFormulaError err)
            return err;

        var cfs = new List<double>();
        foreach (var item in FormulaCoercion.FlattenValues(valNode))
        {
            if (FormulaCoercion.TryCoerceDouble(item, out double d))
            {
                cfs.Add(d);
            }
        }

        if (cfs.Count < 2)
            return OdfFormulaError.Num;

        double guess = 0.1;
        if (arguments.Count == 2)
        {
            var guessVal = arguments[1].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(guessVal, out guess))
                return OdfFormulaError.Value;
        }

        double r = guess;
        const int MaxIterations = 100;
        const double Tolerance = 1e-7;

        for (int i = 0; i < MaxIterations; i++)
        {
            double npv = 0;
            double dNpv = 0;
            for (int t = 0; t < cfs.Count; t++)
            {
                double denom = Math.Pow(1.0 + r, t);
                if (Math.Abs(denom) < 1e-15)
                    return OdfFormulaError.Num;
                npv += cfs[t] / denom;
                if (t > 0)
                {
                    dNpv -= t * cfs[t] / Math.Pow(1.0 + r, t + 1);
                }
            }

            if (Math.Abs(dNpv) < 1e-15)
                return OdfFormulaError.Num;
            double nextR = r - npv / dNpv;
            if (Math.Abs(nextR - r) < Tolerance)
            {
                return nextR;
            }
            r = nextR;
        }

        return OdfFormulaError.Num;
    }

    private static object EvaluateMirr(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;
        var valNode = arguments[0].Evaluate(context);
        var fRateVal = arguments[1].Evaluate(context);
        var rRateVal = arguments[2].Evaluate(context);

        if (valNode is OdfFormulaError err)
            return err;
        if (!FormulaCoercion.TryCoerceDouble(fRateVal, out double fRate) || !FormulaCoercion.TryCoerceDouble(rRateVal, out double rRate))
            return OdfFormulaError.Value;

        var cfs = new List<double>();
        foreach (var item in FormulaCoercion.FlattenValues(valNode))
        {
            if (FormulaCoercion.TryCoerceDouble(item, out double d))
            {
                cfs.Add(d);
            }
        }

        int n = cfs.Count;
        if (n < 2)
            return OdfFormulaError.Num;

        double pvOutflow = 0;
        double fvInflow = 0;
        bool hasPositive = false;
        bool hasNegative = false;

        for (int t = 0; t < n; t++)
        {
            double cf = cfs[t];
            if (cf < 0)
            {
                pvOutflow += -cf / Math.Pow(1.0 + fRate, t);
                hasNegative = true;
            }
            else if (cf > 0)
            {
                fvInflow += cf * Math.Pow(1.0 + rRate, n - 1 - t);
                hasPositive = true;
            }
        }

        if (!hasPositive || !hasNegative)
            return OdfFormulaError.Num;
        if (pvOutflow == 0)
            return OdfFormulaError.Div0;

        try
        {
            double mirr = Math.Pow(fvInflow / pvOutflow, 1.0 / (n - 1)) - 1.0;
            return mirr;
        }
        catch
        {
            return OdfFormulaError.Num;
        }
    }






    #endregion
}
