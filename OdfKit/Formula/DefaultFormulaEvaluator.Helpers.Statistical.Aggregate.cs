using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Statistical Functions - Aggregate

    private static object EvaluateMedian(List<AstNode> arguments, IEvaluationContext context)
    {
        var nums = new List<double>();
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;
            foreach (var item in FlattenValues(val))
            {
                if (TryCoerceDouble(item, out double d))
                {
                    nums.Add(d);
                }
            }
        }
        if (nums.Count == 0)
            return OdfFormulaError.Num;
        nums.Sort();
        int mid = nums.Count / 2;
        if (nums.Count % 2 != 0)
        {
            return nums[mid];
        }
        else
        {
            return (nums[mid - 1] + nums[mid]) / 2.0;
        }
    }

    private static List<double> GetNumbers(List<AstNode> arguments, IEvaluationContext context, out object? error)
    {
        error = null;
        var nums = new List<double>();
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
            {
                error = err;
                return nums;
            }
            foreach (var item in FlattenValues(val))
            {
                if (TryCoerceDouble(item, out double d))
                {
                    nums.Add(d);
                }
            }
        }
        return nums;
    }

    private static object EvaluateStDev(List<AstNode> arguments, IEvaluationContext context)
    {
        var nums = GetNumbers(arguments, context, out var err);
        if (err != null)
            return err;
        if (nums.Count < 2)
            return OdfFormulaError.Div0;
        double avg = nums.Average();
        double sumSq = nums.Sum(d => (d - avg) * (d - avg));
        return Math.Sqrt(sumSq / (nums.Count - 1));
    }

    private static object EvaluateStDevP(List<AstNode> arguments, IEvaluationContext context)
    {
        var nums = GetNumbers(arguments, context, out var err);
        if (err != null)
            return err;
        if (nums.Count < 1)
            return OdfFormulaError.Div0;
        double avg = nums.Average();
        double sumSq = nums.Sum(d => (d - avg) * (d - avg));
        return Math.Sqrt(sumSq / nums.Count);
    }

    private static object EvaluateVar(List<AstNode> arguments, IEvaluationContext context)
    {
        var nums = GetNumbers(arguments, context, out var err);
        if (err != null)
            return err;
        if (nums.Count < 2)
            return OdfFormulaError.Div0;
        double avg = nums.Average();
        double sumSq = nums.Sum(d => (d - avg) * (d - avg));
        return sumSq / (nums.Count - 1);
    }

    private static object EvaluateVarP(List<AstNode> arguments, IEvaluationContext context)
    {
        var nums = GetNumbers(arguments, context, out var err);
        if (err != null)
            return err;
        if (nums.Count < 1)
            return OdfFormulaError.Div0;
        double avg = nums.Average();
        double sumSq = nums.Sum(d => (d - avg) * (d - avg));
        return sumSq / nums.Count;
    }

    private static object EvaluateLarge(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var kVal = arguments[1].Evaluate(context);
        if (!TryCoerceDouble(kVal, out double kD))
            return OdfFormulaError.Value;
        int k = (int)kD;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        var nums = new List<double>();
        foreach (var item in FlattenValues(val))
        {
            if (TryCoerceDouble(item, out double d))
            {
                nums.Add(d);
            }
        }

        if (k < 1 || k > nums.Count)
            return OdfFormulaError.Num;
        nums.Sort((x, y) => y.CompareTo(x));
        return nums[k - 1];
    }

    private static object EvaluateSmall(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var kVal = arguments[1].Evaluate(context);
        if (!TryCoerceDouble(kVal, out double kD))
            return OdfFormulaError.Value;
        int k = (int)kD;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        var nums = new List<double>();
        foreach (var item in FlattenValues(val))
        {
            if (TryCoerceDouble(item, out double d))
            {
                nums.Add(d);
            }
        }

        if (k < 1 || k > nums.Count)
            return OdfFormulaError.Num;
        nums.Sort();
        return nums[k - 1];
    }

    private static object EvaluateRank(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var numVal = arguments[0].Evaluate(context);
        if (!TryCoerceDouble(numVal, out double number))
            return OdfFormulaError.Value;

        var refVal = arguments[1].Evaluate(context);
        if (refVal is OdfFormulaError err)
            return err;

        bool ascending = false;
        if (arguments.Count == 3)
        {
            var orderVal = arguments[2].Evaluate(context);
            if (!TryCoerceDouble(orderVal, out double oD))
                return OdfFormulaError.Value;
            ascending = oD != 0;
        }

        var nums = new List<double>();
        foreach (var item in FlattenValues(refVal))
        {
            if (TryCoerceDouble(item, out double d))
            {
                nums.Add(d);
            }
        }

        if (!nums.Contains(number))
            return OdfFormulaError.NA;

        if (ascending)
        {
            nums.Sort();
            return (double)(nums.IndexOf(number) + 1);
        }
        else
        {
            nums.Sort((x, y) => y.CompareTo(x));
            return (double)(nums.IndexOf(number) + 1);
        }
    }

    private static object EvaluatePercentile(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var pVal = arguments[1].Evaluate(context);
        if (!TryCoerceDouble(pVal, out double p) || p < 0 || p > 1)
            return OdfFormulaError.Num;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        var nums = new List<double>();
        foreach (var item in FlattenValues(val))
        {
            if (TryCoerceDouble(item, out double d))
                nums.Add(d);
        }
        if (nums.Count == 0)
            return OdfFormulaError.Num;

        nums.Sort();
        double idx = p * (nums.Count - 1);
        int i = (int)Math.Floor(idx);
        double f = idx - i;
        if (i >= nums.Count - 1)
            return nums[nums.Count - 1];
        return nums[i] + f * (nums[i + 1] - nums[i]);
    }

    private static object EvaluateQuartile(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var qVal = arguments[1].Evaluate(context);
        if (!TryCoerceDouble(qVal, out double qD))
            return OdfFormulaError.Value;
        int q = (int)qD;
        if (q < 0 || q > 4)
            return OdfFormulaError.Num;

        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;

        var nums = new List<double>();
        foreach (var item in FlattenValues(val))
        {
            if (TryCoerceDouble(item, out double d))
                nums.Add(d);
        }
        if (nums.Count == 0)
            return OdfFormulaError.Num;

        nums.Sort();
        double p = q switch
        {
            0 => 0.0,
            1 => 0.25,
            2 => 0.5,
            3 => 0.75,
            4 => 1.0,
            _ => 0.0
        };

        double idx = p * (nums.Count - 1);
        int i = (int)Math.Floor(idx);
        double f = idx - i;
        if (i >= nums.Count - 1)
            return nums[nums.Count - 1];
        return nums[i] + f * (nums[i + 1] - nums[i]);
    }


    #endregion
}
