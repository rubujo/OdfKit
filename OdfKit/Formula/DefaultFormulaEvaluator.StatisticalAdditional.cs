using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Additional Statistical Functions


    private static object EvaluateMax(List<AstNode> arguments, IEvaluationContext context)
    {
        double max = double.MinValue;
        bool hasNumber = false;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;
            foreach (var innerVal in FlattenValues(val))
            {
                if (TryCoerceDouble(innerVal, out double d))
                {
                    if (d > max)
                        max = d;
                    hasNumber = true;
                }
            }
        }
        return hasNumber ? max : 0.0;
    }

    private static object EvaluateMin(List<AstNode> arguments, IEvaluationContext context)
    {
        double min = double.MaxValue;
        bool hasNumber = false;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;
            foreach (var innerVal in FlattenValues(val))
            {
                if (TryCoerceDouble(innerVal, out double d))
                {
                    if (d < min)
                        min = d;
                    hasNumber = true;
                }
            }
        }
        return hasNumber ? min : 0.0;
    }


    #endregion
}
