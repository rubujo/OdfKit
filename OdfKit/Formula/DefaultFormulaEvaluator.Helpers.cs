using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Helpers & Coercion

    private static IEnumerable<object> FlattenValues(object val)
    {
        if (val is OdfReferenceList refList)
        {
            foreach (var r in refList.References)
            {
                foreach (var item in FlattenValues(r))
                    yield return item;
            }
        }
        else if (val is object[,] arr)
        {
            foreach (var item in arr)
                yield return item;
        }
        else
        {
            yield return val;
        }
    }

    private static bool TryCoerceDouble(object val, out double result)
    {
        if (val is double d)
        { result = d; return true; }
        if (val is bool b)
        { result = b ? 1.0 : 0.0; return true; }
        if (val is string s)
            return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
        result = 0;
        return false;
    }

    private static bool CoerceToBool(object val)
    {
        if (val is bool b)
            return b;
        if (val is double d)
            return d != 0;
        if (val is string s)
        {
            if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
                return true;
            if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return false;
    }

    private static bool TryCoerceToBool(object val, out bool result)
    {
        if (val is bool b)
        { result = b; return true; }
        if (val is double d)
        { result = d != 0; return true; }
        if (val is string s)
        {
            if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
            { result = true; return true; }
            if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            { result = false; return true; }
        }
        result = false;
        return false;
    }

    private static int CompareValues(object val1, object val2)
    {
        if (val1 is double d1 && val2 is double d2)
            return d1.CompareTo(d2);
        if (val1 is bool b1 && val2 is bool b2)
            return b1.CompareTo(b2);

        if (TryCoerceDouble(val1, out double num1) && TryCoerceDouble(val2, out double num2))
            return num1.CompareTo(num2);

        string s1 = val1?.ToString() ?? "";
        string s2 = val2?.ToString() ?? "";
        return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
    }


    #endregion
}
