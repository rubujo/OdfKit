using System;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

internal class CriteriaMatcher
{
    private readonly string? _op;
    private readonly object _operand;

    public CriteriaMatcher(object criteria)
    {
        if (criteria is string strCriteria)
        {
            strCriteria = strCriteria.Trim();
            if (strCriteria.StartsWith("<="))
            { _op = "<="; _operand = ParseOperand(strCriteria.Substring(2)); }
            else if (strCriteria.StartsWith(">="))
            { _op = ">="; _operand = ParseOperand(strCriteria.Substring(2)); }
            else if (strCriteria.StartsWith("<>"))
            { _op = "<>"; _operand = ParseOperand(strCriteria.Substring(2)); }
            else if (strCriteria.StartsWith("<"))
            { _op = "<"; _operand = ParseOperand(strCriteria.Substring(1)); }
            else if (strCriteria.StartsWith(">"))
            { _op = ">"; _operand = ParseOperand(strCriteria.Substring(1)); }
            else if (strCriteria.StartsWith("="))
            { _op = "="; _operand = ParseOperand(strCriteria.Substring(1)); }
            else
            {
                _op = "=";
                _operand = ParseOperand(strCriteria);
            }
        }
        else
        {
            _op = "=";
            _operand = criteria;
        }
    }

    private static object ParseOperand(string val)
    {
        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
            return num;
        if (bool.TryParse(val, out bool b))
            return b;
        return val;
    }

    public bool Matches(object cellVal)
    {
        if (cellVal == null || cellVal is OdfFormulaError)
            return false;

        int comp = Compare(cellVal, _operand);

        return _op switch
        {
            "=" => comp == 0,
            "<" => comp < 0,
            ">" => comp > 0,
            "<=" => comp <= 0,
            ">=" => comp >= 0,
            "<>" => comp != 0,
            _ => false
        };
    }

    private static int Compare(object val1, object val2)
    {
        if (val1 is double d1 && val2 is double d2)
            return d1.CompareTo(d2);
        if (val1 is bool b1 && val2 is bool b2)
            return b1.CompareTo(b2);

        if (double.TryParse(val1.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double n1) &&
            double.TryParse(val2.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double n2))
        {
            return n1.CompareTo(n2);
        }

        return string.Compare(val1.ToString(), val2.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
