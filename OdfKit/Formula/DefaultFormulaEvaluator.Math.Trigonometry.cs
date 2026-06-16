using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Math Functions - Trigonometry & Products

    private static object EvaluateAsin(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!FormulaCoercion.TryCoerceDouble(val, out double d) || d < -1 || d > 1)
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d) || d < -1 || d > 1)
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
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

        if (!FormulaCoercion.TryCoerceDouble(xVal, out double x) || !FormulaCoercion.TryCoerceDouble(yVal, out double y))
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
        if (!FormulaCoercion.TryCoerceDouble(val, out double d) || d <= 0)
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
                    if (FormulaCoercion.TryCoerceDouble(cellVal, out double d))
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

    #endregion
}
