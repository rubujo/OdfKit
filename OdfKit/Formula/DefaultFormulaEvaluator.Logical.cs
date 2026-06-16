using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Logical Functions


    private static object EvaluateIf(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;

        object testVal = arguments[0].Evaluate(context);
        if (testVal is OdfFormulaError)
            return testVal;

        bool test = FormulaCoercion.CoerceToBool(testVal);

        if (test)
        {
            return arguments[1].Evaluate(context);
        }
        else
        {
            return arguments.Count == 3 ? arguments[2].Evaluate(context) : false;
        }
    }

    private static object EvaluateAnd(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
            return OdfFormulaError.Value;

        bool hasLogical = false;
        bool result = true;

        foreach (var node in arguments)
        {
            var val = node.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            foreach (var innerVal in FormulaCoercion.FlattenValues(val))
            {
                if (FormulaCoercion.TryCoerceToBool(innerVal, out bool b))
                {
                    hasLogical = true;
                    result &= b;
                }
            }
        }

        return hasLogical ? result : OdfFormulaError.Value;
    }

    private static object EvaluateOr(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
            return OdfFormulaError.Value;

        bool hasLogical = false;
        bool result = false;

        foreach (var node in arguments)
        {
            var val = node.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;

            foreach (var innerVal in FormulaCoercion.FlattenValues(val))
            {
                if (FormulaCoercion.TryCoerceToBool(innerVal, out bool b))
                {
                    hasLogical = true;
                    result |= b;
                }
            }
        }

        return hasLogical ? result : OdfFormulaError.Value;
    }

    private static object EvaluateXor(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
            return OdfFormulaError.Value;
        bool hasLogical = false;
        bool result = false;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err)
                return err;
            foreach (var item in FormulaCoercion.FlattenValues(val))
            {
                if (FormulaCoercion.TryCoerceToBool(item, out bool b))
                {
                    hasLogical = true;
                    if (b)
                        result = !result;
                }
            }
        }
        return hasLogical ? result : OdfFormulaError.Value;
    }

    private static object EvaluateIfError(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var first = arguments[0].Evaluate(context);
        if (first is OdfFormulaError)
        {
            return arguments[1].Evaluate(context);
        }
        return first;
    }

    private static object EvaluateIfNa(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var first = arguments[0].Evaluate(context);
        if (first is OdfFormulaError err && err.ErrorType == OdfFormulaErrorType.NA)
        {
            return arguments[1].Evaluate(context);
        }
        return first;
    }

    private static object EvaluateIfs(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count % 2 != 0 || arguments.Count == 0)
            return OdfFormulaError.Value;
        for (int i = 0; i < arguments.Count; i += 2)
        {
            var condVal = arguments[i].Evaluate(context);
            if (condVal is OdfFormulaError err)
                return err;
            if (FormulaCoercion.CoerceToBool(condVal))
            {
                return arguments[i + 1].Evaluate(context);
            }
        }
        return OdfFormulaError.NA;
    }

    private static object EvaluateSwitch(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3)
            return OdfFormulaError.Value;
        var exprVal = arguments[0].Evaluate(context);
        if (exprVal is OdfFormulaError err)
            return err;

        int matchCount = (arguments.Count - 1) / 2;
        for (int i = 0; i < matchCount; i++)
        {
            int valIdx = 1 + i * 2;
            int resIdx = valIdx + 1;
            var caseVal = arguments[valIdx].Evaluate(context);
            if (caseVal is OdfFormulaError caseErr)
                return caseErr;
            if (FormulaCoercion.CompareValues(exprVal, caseVal) == 0)
            {
                return arguments[resIdx].Evaluate(context);
            }
        }

        if ((arguments.Count - 1) % 2 != 0)
        {
            return arguments[arguments.Count - 1].Evaluate(context);
        }
        return OdfFormulaError.NA;
    }

    private static object EvaluateIsBlank(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;

        var node = arguments[0];
        if (node is CellAddressNode cellNode)
        {
            var addr = cellNode.Address;
            if (context is OdfDomEvaluationContext domCtx)
            {
                return !domCtx.CellValues.ContainsKey(addr) && !domCtx.CellFormulas.ContainsKey(addr);
            }

            var type = context.GetType();
            var cellValuesProp = type.GetProperty("CellValues");
            var cellFormulasProp = type.GetProperty("CellFormulas");
            if (cellValuesProp != null && cellFormulasProp != null)
            {
                var vals = cellValuesProp.GetValue(context) as System.Collections.IDictionary;
                var forms = cellFormulasProp.GetValue(context) as System.Collections.IDictionary;
                if (vals != null && forms != null)
                {
                    return !vals.Contains(addr) && !forms.Contains(addr);
                }
            }
        }

        var val = node.Evaluate(context);
        return val == null;
    }

    private static object EvaluateIsError(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        return arguments[0].Evaluate(context) is OdfFormulaError;
    }

    private static object EvaluateIsNa(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        return arguments[0].Evaluate(context) is OdfFormulaError err && err.ErrorType == OdfFormulaErrorType.NA;
    }

    private static bool IsReferenceNode(AstNode node)
    {
        if (node is ParenthesizedNode p)
            return IsReferenceNode(p.Inner);
        return node is CellAddressNode ||
               node is RangeReferenceNode ||
               node is ReferenceUnionNode ||
               node is ReferenceIntersectionNode ||
               node is NamedRangeNode;
    }

    private static object EvaluateIsRef(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        return IsReferenceNode(arguments[0]);
    }

    private static object EvaluateType(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        object val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError)
            return 16.0;
        if (val is double)
            return 1.0;
        if (val is string)
            return 2.0;
        if (val is bool)
            return 4.0;
        if (val is object[,])
            return 64.0;
        return 1.0;
    }

    private static object EvaluateIsOdd(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        long n = (long)Math.Truncate(d);
        return Math.Abs(n % 2) == 1;
    }

    private static object EvaluateIsEven(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!FormulaCoercion.TryCoerceDouble(val, out double d))
            return OdfFormulaError.Value;
        long n = (long)Math.Truncate(d);
        return n % 2 == 0;
    }

    private static object EvaluateBitAnd(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var v1 = arguments[0].Evaluate(context);
        var v2 = arguments[1].Evaluate(context);
        if (v1 is OdfFormulaError err1)
            return err1;
        if (v2 is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(v1, out double d1) || !FormulaCoercion.TryCoerceDouble(v2, out double d2))
            return OdfFormulaError.Value;
        if (d1 < 0 || d2 < 0)
            return OdfFormulaError.Value;
        return (double)((long)d1 & (long)d2);
    }

    private static object EvaluateBitOr(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var v1 = arguments[0].Evaluate(context);
        var v2 = arguments[1].Evaluate(context);
        if (v1 is OdfFormulaError err1)
            return err1;
        if (v2 is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(v1, out double d1) || !FormulaCoercion.TryCoerceDouble(v2, out double d2))
            return OdfFormulaError.Value;
        if (d1 < 0 || d2 < 0)
            return OdfFormulaError.Value;
        return (double)((long)d1 | (long)d2);
    }

    private static object EvaluateBitXor(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var v1 = arguments[0].Evaluate(context);
        var v2 = arguments[1].Evaluate(context);
        if (v1 is OdfFormulaError err1)
            return err1;
        if (v2 is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(v1, out double d1) || !FormulaCoercion.TryCoerceDouble(v2, out double d2))
            return OdfFormulaError.Value;
        if (d1 < 0 || d2 < 0)
            return OdfFormulaError.Value;
        return (double)((long)d1 ^ (long)d2);
    }

    private static object EvaluateBitLShift(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var v1 = arguments[0].Evaluate(context);
        var v2 = arguments[1].Evaluate(context);
        if (v1 is OdfFormulaError err1)
            return err1;
        if (v2 is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(v1, out double d1) || !FormulaCoercion.TryCoerceDouble(v2, out double d2))
            return OdfFormulaError.Value;
        if (d1 < 0 || d2 < 0)
            return OdfFormulaError.Value;
        return (double)((long)d1 << (int)d2);
    }

    private static object EvaluateBitRShift(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var v1 = arguments[0].Evaluate(context);
        var v2 = arguments[1].Evaluate(context);
        if (v1 is OdfFormulaError err1)
            return err1;
        if (v2 is OdfFormulaError err2)
            return err2;
        if (!FormulaCoercion.TryCoerceDouble(v1, out double d1) || !FormulaCoercion.TryCoerceDouble(v2, out double d2))
            return OdfFormulaError.Value;
        if (d1 < 0 || d2 < 0)
            return OdfFormulaError.Value;
        return (double)((long)d1 >> (int)d2);
    }


    #endregion
}
