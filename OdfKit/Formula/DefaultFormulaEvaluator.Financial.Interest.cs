using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Financial Functions - Interest

    private static object EvaluateIpmt(List<AstNode> arguments, IEvaluationContext context)
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

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valPer, out double per) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 5)
        {
            var valFv = arguments[4].Evaluate(context);
            if (valFv is OdfFormulaError err5)
                return err5;
            if (!TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 6)
        {
            var valType = arguments[5].Evaluate(context);
            if (valType is OdfFormulaError err6)
                return err6;
            if (!TryCoerceDouble(valType, out type))
                return OdfFormulaError.Value;
        }

        if (per < 1 || per > nper)
            return OdfFormulaError.Value;

        // Calculate standard PMT
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

        // Simple balance progression
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

    private static object EvaluatePpmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 4 || arguments.Count > 6)
            return OdfFormulaError.Value;

        // PPMT = PMT - IPMT
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
            if (!TryCoerceDouble(valFv, out fv))
                return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 6)
        {
            var valType = arguments[5].Evaluate(context);
            if (valType is OdfFormulaError err6)
                return err6;
            if (!TryCoerceDouble(valType, out type))
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


    #endregion
}
