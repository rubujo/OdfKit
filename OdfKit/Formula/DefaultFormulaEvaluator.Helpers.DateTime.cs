using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Helpers & Coercion - DateTime

    private static object EvaluateDateDif(List<AstNode> arguments, IEvaluationContext context)
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

    private static object EvaluateDateValue(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is not string s)
            return OdfFormulaError.Value;
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dt))
        {
            return Math.Floor((dt.Date - Epoch).TotalDays);
        }
        return OdfFormulaError.Value;
    }

    private static object EvaluateTimeValue(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is not string s)
            return OdfFormulaError.Value;
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dt))
        {
            return dt.TimeOfDay.TotalDays;
        }
        return OdfFormulaError.Value;
    }

    private static object EvaluateWeekday(List<AstNode> arguments, IEvaluationContext context)
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
            if (!TryCoerceDouble(typeVal, out double tD))
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

    private static object EvaluateWeekNum(List<AstNode> arguments, IEvaluationContext context)
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
            if (!TryCoerceDouble(typeVal, out double tD))
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

    private static object EvaluateWorkday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var daysVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime current) || !TryCoerceDouble(daysVal, out double daysD))
            return OdfFormulaError.Value;

        int days = (int)daysD;

        var holidaySet = new HashSet<DateTime>();
        if (arguments.Count == 3)
        {
            var holVal = arguments[2].Evaluate(context);
            foreach (var item in FlattenValues(holVal))
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

    private static object EvaluateNetWorkDays(List<AstNode> arguments, IEvaluationContext context)
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
            foreach (var item in FlattenValues(holVal))
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

    private static object EvaluateEDate(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var mVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime dt) || !TryCoerceDouble(mVal, out double mD))
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

    private static object EvaluateEOMonth(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2)
            return OdfFormulaError.Value;
        var startVal = arguments[0].Evaluate(context);
        var mVal = arguments[1].Evaluate(context);

        if (!TryCoerceDateTime(startVal, out DateTime dt) || !TryCoerceDouble(mVal, out double mD))
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


    #endregion
}
