using System;
using System.Collections.Generic;

using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 財務內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaFinancialFunctionHandlers
{


    internal static object EvaluatePmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5)
            return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valNper = arguments[1].Evaluate(context);
        var valPv = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1)
            return err1;
        if (valNper is OdfFormulaError err2)
            return err2;
        if (valPv is OdfFormulaError err3)
            return err3;

        if (!FormulaCoercion.TryCoerceDouble(valRate, out double rate) ||
            !FormulaCoercion.TryCoerceDouble(valNper, out double nper) ||
            !FormulaCoercion.TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!FormulaCoercion.TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        if (nper <= 0)
            return OdfFormulaError.Value;

        if (rate == 0)
        {
            return -(pv + fv) / nper;
        }
        else
        {
            double p = Math.Pow(1 + rate, nper);
            if (type != 0)
            {
                return -(pv * p + fv) * rate / ((p - 1) * (1 + rate));
            }
            else
            {
                return -(pv * p + fv) * rate / (p - 1);
            }
        }
    }

    internal static object EvaluateFv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5)
            return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valNper = arguments[1].Evaluate(context);
        var valPmt = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1)
            return err1;
        if (valNper is OdfFormulaError err2)
            return err2;
        if (valPmt is OdfFormulaError err3)
            return err3;

        if (!FormulaCoercion.TryCoerceDouble(valRate, out double rate) ||
            !FormulaCoercion.TryCoerceDouble(valNper, out double nper) ||
            !FormulaCoercion.TryCoerceDouble(valPmt, out double pmt))
            return OdfFormulaError.Value;

        double pv = 0;
        if (arguments.Count >= 4)
        {
            var valPv = arguments[3].Evaluate(context);
            if (valPv is OdfFormulaError err4)
                return err4;
            if (!FormulaCoercion.TryCoerceDouble(valPv, out pv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        if (rate == 0)
        {
            return -(pv + pmt * nper);
        }
        else
        {
            double p = Math.Pow(1 + rate, nper);
            if (type != 0)
            {
                return -pv * p - pmt * (1 + rate) * (p - 1) / rate;
            }
            else
            {
                return -pv * p - pmt * (p - 1) / rate;
            }
        }
    }

    internal static object EvaluatePv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5)
            return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valNper = arguments[1].Evaluate(context);
        var valPmt = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1)
            return err1;
        if (valNper is OdfFormulaError err2)
            return err2;
        if (valPmt is OdfFormulaError err3)
            return err3;

        if (!FormulaCoercion.TryCoerceDouble(valRate, out double rate) ||
            !FormulaCoercion.TryCoerceDouble(valNper, out double nper) ||
            !FormulaCoercion.TryCoerceDouble(valPmt, out double pmt))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!FormulaCoercion.TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        if (rate == 0)
        {
            return -(fv + pmt * nper);
        }
        else
        {
            double p = Math.Pow(1 + rate, nper);
            if (type != 0)
            {
                return (-fv - pmt * (1 + rate) * (p - 1) / rate) / p;
            }
            else
            {
                return (-fv - pmt * (p - 1) / rate) / p;
            }
        }
    }

    internal static object EvaluateNper(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5)
            return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valPmt = arguments[1].Evaluate(context);
        var valPv = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1)
            return err1;
        if (valPmt is OdfFormulaError err2)
            return err2;
        if (valPv is OdfFormulaError err3)
            return err3;

        if (!FormulaCoercion.TryCoerceDouble(valRate, out double rate) ||
            !FormulaCoercion.TryCoerceDouble(valPmt, out double pmt) ||
            !FormulaCoercion.TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!FormulaCoercion.TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        if (pmt == 0)
            return OdfFormulaError.Value;

        if (rate == 0)
        {
            return -(pv + fv) / pmt;
        }
        else
        {
            double num = pmt * (1 + rate * (type != 0 ? 1 : 0)) - fv * rate;
            double den = pmt * (1 + rate * (type != 0 ? 1 : 0)) + pv * rate;
            if (num * den <= 0)
                return OdfFormulaError.Num;
            return Math.Log(num / den) / Math.Log(1 + rate);
        }
    }

    internal static object EvaluateRate(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 6)
            return OdfFormulaError.Value;
        var valNper = arguments[0].Evaluate(context);
        var valPmt = arguments[1].Evaluate(context);
        var valPv = arguments[2].Evaluate(context);

        if (valNper is OdfFormulaError err1)
            return err1;
        if (valPmt is OdfFormulaError err2)
            return err2;
        if (valPv is OdfFormulaError err3)
            return err3;

        if (!FormulaCoercion.TryCoerceDouble(valNper, out double nper) ||
            !FormulaCoercion.TryCoerceDouble(valPmt, out double pmt) ||
            !FormulaCoercion.TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!FormulaCoercion.TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count >= 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        double guess = 0.1;
        if (arguments.Count == 6)
        {
            var valGuess = arguments[5].Evaluate(context);
            if (valGuess is OdfFormulaError err6)
                return err6;
            if (!FormulaCoercion.TryCoerceDouble(valGuess, out guess))
                return OdfFormulaError.Value;
        }

        double F(double r)
        {
            if (r == 0)
            {
                return pv + pmt * nper + fv;
            }
            else
            {
                double p = Math.Pow(1 + r, nper);
                return pv * p + pmt * (1 + r * (type != 0 ? 1 : 0)) * (p - 1) / r + fv;
            }
        }

        // 割線法求解器
        double r0 = guess;
        double r1 = guess * 1.1 + 0.01;

        for (int i = 0; i < 100; i++)
        {
            double f0 = F(r0);
            double f1 = F(r1);

            if (Math.Abs(f1 - f0) < 1e-15)
                break;

            double r_next = r1 - f1 * (r1 - r0) / (f1 - f0);
            if (Math.Abs(r_next - r1) < 1e-9)
            {
                return r_next;
            }

            r0 = r1;
            r1 = r_next;
        }

        return OdfFormulaError.Num;
    }



    internal static object EvaluateIpmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 4 || arguments.Count > 6)
            return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valPer = arguments[1].Evaluate(context);
        var valNper = arguments[2].Evaluate(context);
        var valPv = arguments[3].Evaluate(context);

        if (valRate is OdfFormulaError err1)
            return err1;
        if (valPer is OdfFormulaError err2)
            return err2;
        if (valNper is OdfFormulaError err3)
            return err3;
        if (valPv is OdfFormulaError err4)
            return err4;

        if (!FormulaCoercion.TryCoerceDouble(valRate, out double rate) ||
            !FormulaCoercion.TryCoerceDouble(valPer, out double per) ||
            !FormulaCoercion.TryCoerceDouble(valNper, out double nper) ||
            !FormulaCoercion.TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 5)
        {
            var valFv = arguments[4].Evaluate(context);
            if (valFv is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 6)
        {
            var valType = arguments[5].Evaluate(context);
            if (valType is OdfFormulaError err6)
                return err6;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        if (per < 1 || per > nper)
            return OdfFormulaError.Value;

        // 計算標準每期付款額 (PMT)
        var pmtArgs = new List<AstNode> {
            new LiteralNode(rate),
            new LiteralNode(nper),
            new LiteralNode(pv),
            new LiteralNode(fv),
            new LiteralNode(type)
        };
        var pmtResult = EvaluatePmt(pmtArgs, context);
        if (pmtResult is OdfFormulaError)
            return pmtResult;
        double pmt = (double)pmtResult;

        // 簡易餘額遞進
        double balance = pv;
        double interest = 0;

        for (int t = 1; t <= per; t++)
        {
            if (type != 0)
            {
                if (t == 1)
                {
                    interest = 0;
                    balance = balance + pmt;
                }
                else
                {
                    interest = (balance) * rate;
                    balance = balance + interest + pmt;
                }
            }
            else
            {
                interest = balance * rate;
                balance = balance + interest + pmt;
            }
        }

        return -interest;
    }

    internal static object EvaluatePpmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 4 || arguments.Count > 6)
            return OdfFormulaError.Value;

        // 每期本金償還額 (PPMT) = 每期付款額 (PMT) - 每期利息 (IPMT)
        var rate = arguments[0].Evaluate(context);
        var per = arguments[1].Evaluate(context);
        var nper = arguments[2].Evaluate(context);
        var pv = arguments[3].Evaluate(context);

        if (rate is OdfFormulaError err1)
            return err1;
        if (per is OdfFormulaError err2)
            return err2;
        if (nper is OdfFormulaError err3)
            return err3;
        if (pv is OdfFormulaError err4)
            return err4;

        double fv = 0;
        if (arguments.Count >= 5)
        {
            var valFv = arguments[4].Evaluate(context);
            if (valFv is OdfFormulaError err5)
                return err5;
            if (!FormulaCoercion.TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 6)
        {
            var valType = arguments[5].Evaluate(context);
            if (valType is OdfFormulaError err6)
                return err6;
            if (!FormulaCoercion.TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        var pmtArgs = new List<AstNode> {
            new LiteralNode(rate),
            new LiteralNode(nper),
            new LiteralNode(pv),
            new LiteralNode(fv),
            new LiteralNode(type)
        };
        var pmtResult = EvaluatePmt(pmtArgs, context);
        if (pmtResult is OdfFormulaError)
            return pmtResult;

        var ipmtResult = EvaluateIpmt(arguments, context);
        if (ipmtResult is OdfFormulaError)
            return ipmtResult;

        return (double)pmtResult - (double)ipmtResult;
    }



    internal static object EvaluateSln(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateDdb(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateIrr(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateMirr(List<AstNode> arguments, IEvaluationContext context)
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
}

