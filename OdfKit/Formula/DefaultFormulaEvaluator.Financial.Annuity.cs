using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Financial Functions - Annuity

    private static object EvaluatePmt(List<AstNode> arguments, IEvaluationContext context)
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

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!TryCoerceDouble(valType, out type))
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

    private static object EvaluateFv(List<AstNode> arguments, IEvaluationContext context)
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

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPmt, out double pmt))
            return OdfFormulaError.Value;

        double pv = 0;
        if (arguments.Count >= 4)
        {
            var valPv = arguments[3].Evaluate(context);
            if (valPv is OdfFormulaError err4)
                return err4;
            if (!TryCoerceDouble(valPv, out pv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!TryCoerceDouble(valType, out type))
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

    private static object EvaluatePv(List<AstNode> arguments, IEvaluationContext context)
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

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPmt, out double pmt))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!TryCoerceDouble(valType, out type))
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

    private static object EvaluateNper(List<AstNode> arguments, IEvaluationContext context)
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

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valPmt, out double pmt) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!TryCoerceDouble(valType, out type))
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

    private static object EvaluateRate(List<AstNode> arguments, IEvaluationContext context)
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

        if (!TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPmt, out double pmt) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4)
                return err4;
            if (!TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count >= 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5)
                return err5;
            if (!TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        double guess = 0.1;
        if (arguments.Count == 6)
        {
            var valGuess = arguments[5].Evaluate(context);
            if (valGuess is OdfFormulaError err6)
                return err6;
            if (!TryCoerceDouble(valGuess, out guess))
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

        // Secant method solver
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

    #endregion
}
