using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Date and Time Functions


    private static readonly DateTime Epoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private static bool TryCoerceDateTime(object val, out DateTime dt)
    {
        dt = DateTime.MinValue;
        if (val == null || val is OdfFormulaError)
            return false;

        if (val is double d)
        {
            try
            {
                dt = Epoch.AddDays(d);
                return true;
            }
            catch
            {
                return false;
            }
        }

        string s = val.ToString() ?? "";
        if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
        {
            try
            {
                dt = Epoch.AddDays(num);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (s.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var ts = System.Xml.XmlConvert.ToTimeSpan(s);
                dt = Epoch.Add(ts);
                return true;
            }
            catch { }
        }

        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
        {
            return true;
        }

        return false;
    }

    private static object EvaluateDate(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;
        var valY = arguments[0].Evaluate(context);
        var valM = arguments[1].Evaluate(context);
        var valD = arguments[2].Evaluate(context);

        if (valY is OdfFormulaError err1)
            return err1;
        if (valM is OdfFormulaError err2)
            return err2;
        if (valD is OdfFormulaError err3)
            return err3;

        if (!TryCoerceDouble(valY, out double yD) || !TryCoerceDouble(valM, out double mD) || !TryCoerceDouble(valD, out double dD))
            return OdfFormulaError.Value;

        int y = (int)yD;
        int m = (int)mD;
        int d = (int)dD;

        if (y >= 0 && y < 1900)
            y += 1900;
        try
        {
            DateTime baseDate = new DateTime(y, 1, 1);
            DateTime dt = baseDate.AddMonths(m - 1).AddDays(d - 1);
            return (dt - Epoch).TotalDays;
        }
        catch
        {
            return OdfFormulaError.Value;
        }
    }

    private static object EvaluateDay(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDateTime(val, out DateTime dt))
            return OdfFormulaError.Value;
        return (double)dt.Day;
    }

    private static object EvaluateHour(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDateTime(val, out DateTime dt))
            return OdfFormulaError.Value;
        return (double)dt.Hour;
    }

    private static object EvaluateMinute(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDateTime(val, out DateTime dt))
            return OdfFormulaError.Value;
        return (double)dt.Minute;
    }

    private static object EvaluateMonth(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDateTime(val, out DateTime dt))
            return OdfFormulaError.Value;
        return (double)dt.Month;
    }

    private static object EvaluateNow(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0)
            return OdfFormulaError.Value;
        return (DateTime.Now - Epoch).TotalDays;
    }

    private static object EvaluateSecond(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDateTime(val, out DateTime dt))
            return OdfFormulaError.Value;
        return (double)dt.Second;
    }

    private static object EvaluateTime(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;
        var valH = arguments[0].Evaluate(context);
        var valM = arguments[1].Evaluate(context);
        var valS = arguments[2].Evaluate(context);

        if (valH is OdfFormulaError err1)
            return err1;
        if (valM is OdfFormulaError err2)
            return err2;
        if (valS is OdfFormulaError err3)
            return err3;

        if (!TryCoerceDouble(valH, out double h) || !TryCoerceDouble(valM, out double m) || !TryCoerceDouble(valS, out double s))
            return OdfFormulaError.Value;

        return (h * 3600.0 + m * 60.0 + s) / 86400.0;
    }

    private static object EvaluateToday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0)
            return OdfFormulaError.Value;
        return (DateTime.Today - Epoch).TotalDays;
    }

    private static object EvaluateYear(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDateTime(val, out DateTime dt))
            return OdfFormulaError.Value;
        return (double)dt.Year;
    }

    private static object EvaluateOpenOfficeEasterSunday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!TryCoerceDouble(val, out double yearValue))
            return OdfFormulaError.Value;

        int year = (int)yearValue;
        if (year < 1 || year > 9999)
            return OdfFormulaError.Num;

        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;

        return (new DateTime(year, month, day) - Epoch).TotalDays;
    }

    private static object EvaluateOpenOfficeIsOmitted(List<AstNode> arguments)
    {
        return arguments.Count == 0;
    }


    #endregion
}
