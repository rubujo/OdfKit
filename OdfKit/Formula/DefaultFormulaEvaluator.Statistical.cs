using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Statistical Functions


    private static object EvaluateSum(List<AstNode> arguments, IEvaluationContext context)
    {
        double sum = 0;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            foreach (var innerVal in FlattenValues(val))
            {
                if (TryCoerceDouble(innerVal, out double d))
                {
                    sum += d;
                }
            }
        }
        return sum;
    }

    private static object EvaluateAverage(List<AstNode> arguments, IEvaluationContext context)
    {
        double sum = 0;
        int count = 0;

        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            foreach (var innerVal in FlattenValues(val))
            {
                if (TryCoerceDouble(innerVal, out double d))
                {
                    sum += d;
                    count++;
                }
            }
        }

        return count == 0 ? OdfFormulaError.Div0 : sum / count;
    }

    private static object EvaluateCount(List<AstNode> arguments, IEvaluationContext context)
    {
        int count = 0;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError)
                continue;

            foreach (var innerVal in FlattenValues(val))
            {
                if (innerVal is double || (innerVal is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)))
                {
                    count++;
                }
            }
        }
        return (double)count;
    }

    private static object EvaluateSumIf(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;

        if (arguments[0] is not RangeReferenceNode rangeNode)
            return OdfFormulaError.Value;
        var range = rangeNode.Range;

        var criteriaVal = arguments[1].Evaluate(context);
        if (criteriaVal is OdfFormulaError err)
            return err;

        OdfCellRange sumRange = range;
        if (arguments.Count == 3)
        {
            if (arguments[2] is not RangeReferenceNode sumRangeNode)
                return OdfFormulaError.Value;
            sumRange = sumRangeNode.Range;
        }

        object[,] rangeValues = context.GetRangeValues(range);
        object[,] sumValues = context.GetRangeValues(sumRange);

        int rows = rangeValues.GetLength(0);
        int cols = rangeValues.GetLength(1);

        double sum = 0;
        var criteria = new CriteriaMatcher(criteriaVal);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                object cellVal = rangeValues[r, c];
                if (criteria.Matches(cellVal))
                {
                    if (r < sumValues.GetLength(0) && c < sumValues.GetLength(1))
                    {
                        object sumVal = sumValues[r, c];
                        if (TryCoerceDouble(sumVal, out double num))
                        {
                            sum += num;
                        }
                    }
                }
            }
        }

        return sum;
    }

    private static object EvaluateCountIf(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;

        if (arguments[0] is not RangeReferenceNode rangeNode)
            return OdfFormulaError.Value;
        var range = rangeNode.Range;

        var criteriaVal = arguments[1].Evaluate(context);
        if (criteriaVal is OdfFormulaError err)
            return err;

        object[,] rangeValues = context.GetRangeValues(range);
        int rows = rangeValues.GetLength(0);
        int cols = rangeValues.GetLength(1);

        int count = 0;
        var criteria = new CriteriaMatcher(criteriaVal);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (criteria.Matches(rangeValues[r, c]))
                {
                    count++;
                }
            }
        }

        return (double)count;
    }


    #endregion

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
