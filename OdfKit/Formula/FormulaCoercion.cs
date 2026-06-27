using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using OdfKit.DOM;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 試算表公式求值時的型別轉換與值比較工具。
/// </summary>
internal static class FormulaCoercion
{
    /// <summary>
    /// 將儲存格值、陣列或參照清單展平為單一值序列。
    /// </summary>
    /// <remarks>
    /// 使用明確堆疊迭代，避免巢狀 <c>yield return</c> 配置多層狀態機（PERF-4a）。
    /// </remarks>
    public static IEnumerable<object> FlattenValues(object val)
    {
        var stack = new Stack<object>();
        stack.Push(val);
        while (stack.Count > 0)
        {
            object current = stack.Pop();
            if (current is OdfReferenceList refList)
            {
                for (int i = refList.References.Count - 1; i >= 0; i--)
                {
                    stack.Push(refList.References[i]);
                }
            }
            else if (current is object[,] arr)
            {
                int rows = arr.GetLength(0);
                int cols = arr.GetLength(1);
                for (int r = rows - 1; r >= 0; r--)
                {
                    for (int c = cols - 1; c >= 0; c--)
                    {
                        stack.Push(arr[r, c]);
                    }
                }
            }
            else
            {
                yield return current;
            }
        }
    }

    /// <summary>
    /// 嘗試將值轉換為 <see cref="double"/>。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCoerceDouble(object val, out double result)
    {
        if (val is double d)
        {
            result = d;
            return true;
        }

        if (val is bool b)
        {
            result = b ? 1.0 : 0.0;
            return true;
        }

        if (val is string s)
        {
            return OdfFastNumberParser.TryParse(s.AsSpan(), out result) ||
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        result = 0;
        return false;
    }

    /// <summary>
    /// 將值強制轉換為布林值（非布林／數值／TRUE/FALSE 字串時回傳 false）。
    /// </summary>
    public static bool CoerceToBool(object val)
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

    /// <summary>
    /// 嘗試將值轉換為布林值。
    /// </summary>
    public static bool TryCoerceToBool(object val, out bool result)
    {
        if (val is bool b)
        {
            result = b;
            return true;
        }

        if (val is double d)
        {
            result = d != 0;
            return true;
        }

        if (val is string s)
        {
            if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }
        }

        result = false;
        return false;
    }

    /// <summary>
    /// 比較兩個公式值（數值、布林或字串）。
    /// </summary>
    public static int CompareValues(object val1, object val2)
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
}
