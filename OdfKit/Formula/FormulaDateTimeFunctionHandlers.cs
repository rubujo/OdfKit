using System.Globalization;
using System;
using System.Collections.Generic;

using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 日期時間內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaDateTimeFunctionHandlers
{


    internal static readonly DateTime Epoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    internal static bool TryCoerceDateTime(object val, out DateTime dt)
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
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
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

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return true;
        }

        return false;
    }

    internal static object EvaluateDate(List<AstNode> arguments, IEvaluationContext context)
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

        if (!FormulaCoercion.TryCoerceDouble(valY, out double yD) || !FormulaCoercion.TryCoerceDouble(valM, out double mD) || !FormulaCoercion.TryCoerceDouble(valD, out double dD))
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

    internal static object EvaluateDay(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateHour(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateMinute(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateMonth(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateNow(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0)
            return OdfFormulaError.Value;
        return (DateTime.Now - Epoch).TotalDays;
    }

    internal static object EvaluateSecond(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateTime(List<AstNode> arguments, IEvaluationContext context)
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

        if (!FormulaCoercion.TryCoerceDouble(valH, out double h) || !FormulaCoercion.TryCoerceDouble(valM, out double m) || !FormulaCoercion.TryCoerceDouble(valS, out double s))
            return OdfFormulaError.Value;

        return (h * 3600.0 + m * 60.0 + s) / 86400.0;
    }

    internal static object EvaluateToday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0)
            return OdfFormulaError.Value;
        return (DateTime.Today - Epoch).TotalDays;
    }

    internal static object EvaluateYear(List<AstNode> arguments, IEvaluationContext context)
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

    internal static object EvaluateOpenOfficeEasterSunday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err)
            return err;
        if (!FormulaCoercion.TryCoerceDouble(val, out double yearValue))
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

    internal static object EvaluateOpenOfficeIsOmitted(List<AstNode> arguments)
    {
        return arguments.Count == 0;
    }



    internal static object EvaluateDateDif(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var endVal = arguments[1].Evaluate(context);
        var unitVal = arguments[2].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime start) || !TryCoerceDateTime(endVal, out DateTime end))
            return OdfFormulaError.Value;
        if (unitVal is not string unit)
            return OdfFormulaError.Value;

        if (start > end)
            return OdfFormulaError.Num;

        unit = unit.ToUpperInvariant();

        switch (unit)
        {
            case "Y":
                int years = end.Year - start.Year;
                if (end.Month < start.Month || (end.Month == start.Month && end.Day < start.Day))
                    years--;
                return (double)years;

            case "M":
                int months = (end.Year - start.Year) * 12 + end.Month - start.Month;
                if (end.Day < start.Day)
                    months--;
                return (double)months;

            case "D":
                return (double)(end - start).Days;

            case "MD":
                int mdDays = end.Day - start.Day;
                if (mdDays < 0)
                {
                    DateTime temp = end.AddMonths(-1);
                    int daysInPrevMonth = DateTime.DaysInMonth(temp.Year, temp.Month);
                    mdDays = daysInPrevMonth - start.Day + end.Day;
                }
                return (double)mdDays;

            case "YM":
                int ymMonths = end.Month - start.Month;
                if (end.Day < start.Day)
                    ymMonths--;
                if (ymMonths < 0)
                    ymMonths += 12;
                return (double)ymMonths;

            case "YD":
                DateTime startThisYear = new DateTime(start.Year, start.Month, start.Day);
                DateTime endThisYear = new DateTime(start.Year, end.Month, end.Day);
                if (endThisYear < startThisYear)
                {
                    endThisYear = endThisYear.AddYears(1);
                }
                return (double)(endThisYear - startThisYear).Days;

            default:
                return OdfFormulaError.Value;
        }
    }

    internal static object EvaluateDateValue(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is not string s)
            return OdfFormulaError.Value;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
        {
            return Math.Floor((dt.Date - Epoch).TotalDays);
        }
        return OdfFormulaError.Value;
    }

    internal static object EvaluateTimeValue(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is not string s)
            return OdfFormulaError.Value;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
        {
            return dt.TimeOfDay.TotalDays;
        }
        return OdfFormulaError.Value;
    }

    internal static object EvaluateWeekday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var dateVal = arguments[0].Evaluate(context);
        if (!TryCoerceDateTime(dateVal, out DateTime dt))
            return OdfFormulaError.Value;

        int type = 1;
        if (arguments.Count == 2)
        {
            var typeVal = arguments[1].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(typeVal, out double tD))
                return OdfFormulaError.Value;
            type = (int)tD;
        }

        int dayOfWeek = (int)dt.DayOfWeek;

        switch (type)
        {
            case 1:
                return (double)(dayOfWeek + 1);
            case 2:
                return (double)(dayOfWeek == 0 ? 7 : dayOfWeek);
            case 3:
                return (double)(dayOfWeek == 0 ? 6 : dayOfWeek - 1);
            default:
                return OdfFormulaError.Num;
        }
    }

    internal static object EvaluateWeekNum(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var dateVal = arguments[0].Evaluate(context);
        if (!TryCoerceDateTime(dateVal, out DateTime dt))
            return OdfFormulaError.Value;

        int type = 1;
        if (arguments.Count == 2)
        {
            var typeVal = arguments[1].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(typeVal, out double tD))
                return OdfFormulaError.Value;
            type = (int)tD;
        }

        DateTime jan1 = new DateTime(dt.Year, 1, 1);
        int d = (dt - jan1).Days;

        int w;
        if (type == 1)
        {
            w = (int)jan1.DayOfWeek;
            return (double)((d + w) / 7 + 1);
        }
        else if (type == 2 || type == 11)
        {
            int dow = (int)jan1.DayOfWeek;
            w = dow == 0 ? 6 : dow - 1;
            return (double)((d + w) / 7 + 1);
        }
        else
        {
            w = (int)jan1.DayOfWeek;
            return (double)((d + w) / 7 + 1);
        }
    }

    internal static object EvaluateWorkday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var daysVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime current) || !FormulaCoercion.TryCoerceDouble(daysVal, out double daysD))
            return OdfFormulaError.Value;

        int days = (int)daysD;

        var holidaySet = new HashSet<DateTime>();
        if (arguments.Count == 3)
        {
            var holVal = arguments[2].Evaluate(context);
            foreach (var item in FormulaCoercion.FlattenValues(holVal))
            {
                if (TryCoerceDateTime(item, out DateTime hDate))
                {
                    holidaySet.Add(hDate.Date);
                }
            }
        }

        int step = days < 0 ? -1 : 1;
        int remaining = Math.Abs(days);

        while (remaining > 0)
        {
            current = current.AddDays(step);
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday && !holidaySet.Contains(current.Date))
            {
                remaining--;
            }
        }

        return (current.Date - Epoch).TotalDays;
    }

    internal static object EvaluateNetWorkDays(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var endVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime start) || !TryCoerceDateTime(endVal, out DateTime end))
            return OdfFormulaError.Value;

        var holidaySet = new HashSet<DateTime>();
        if (arguments.Count == 3)
        {
            var holVal = arguments[2].Evaluate(context);
            foreach (var item in FormulaCoercion.FlattenValues(holVal))
            {
                if (TryCoerceDateTime(item, out DateTime hDate))
                {
                    holidaySet.Add(hDate.Date);
                }
            }
        }

        bool swap = false;
        if (start > end)
        {
            var tmp = start;
            start = end;
            end = tmp;
            swap = true;
        }

        int workdays = 0;
        DateTime curr = start.Date;
        while (curr <= end.Date)
        {
            if (curr.DayOfWeek != DayOfWeek.Saturday && curr.DayOfWeek != DayOfWeek.Sunday && !holidaySet.Contains(curr))
            {
                workdays++;
            }
            curr = curr.AddDays(1);
        }

        return swap ? (double)(-workdays) : (double)workdays;
    }

    internal static object EvaluateEDate(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var mVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime dt) || !FormulaCoercion.TryCoerceDouble(mVal, out double mD))
            return OdfFormulaError.Value;

        int months = (int)mD;
        try
        {
            DateTime result = dt.AddMonths(months);
            return (result - Epoch).TotalDays;
        }
        catch
        {
            return OdfFormulaError.Value;
        }
    }

    internal static object EvaluateEOMonth(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var mVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime dt) || !FormulaCoercion.TryCoerceDouble(mVal, out double mD))
            return OdfFormulaError.Value;

        int months = (int)mD;
        try
        {
            DateTime result = dt.AddMonths(months);
            int daysInMonth = DateTime.DaysInMonth(result.Year, result.Month);
            DateTime eoMonth = new DateTime(result.Year, result.Month, daysInMonth);
            return (eoMonth - Epoch).TotalDays;
        }
        catch
        {
            return OdfFormulaError.Value;
        }
    }
}

