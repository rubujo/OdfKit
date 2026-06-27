using System;
using System.Globalization;
using System.Linq;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 提供試算表 OpenFormula 公式的強型別建構入口。
/// </summary>
public static class Formula
{
    /// <summary>
    /// 建立儲存格參照。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c> 或 <c>Sheet1.A1</c></param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Cell(string address) => OdfSpreadsheetFormula.Reference(address);

    /// <summary>
    /// 建立儲存格範圍參照。
    /// </summary>
    /// <param name="rangeAddress">儲存格範圍，例如 <c>A1:A10</c></param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Range(string rangeAddress) => OdfSpreadsheetFormula.Reference(rangeAddress);

    /// <summary>
    /// 建立數值常數。
    /// </summary>
    /// <param name="value">數值</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Number(double value) => OdfSpreadsheetFormula.Atom(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// 建立文字常數。
    /// </summary>
    /// <param name="value">文字內容</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Text(string value) => OdfSpreadsheetFormula.Atom("\"" + value.Replace("\"", "\"\"") + "\"");

    /// <summary>
    /// 建立原始公式片段。
    /// </summary>
    /// <param name="expression">不含 <c>of:=</c> 前綴的公式片段</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Raw(string expression) => OdfSpreadsheetFormula.Raw(expression);

    /// <summary>
    /// 建立 <c>SUM</c> 公式。
    /// </summary>
    /// <param name="rangeAddress">要加總的範圍</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Sum(string rangeAddress) => Function("SUM", Range(rangeAddress));

    /// <summary>
    /// 建立 <c>AVERAGE</c> 公式。
    /// </summary>
    /// <param name="rangeAddress">要平均的範圍</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Average(string rangeAddress) => Function("AVERAGE", Range(rangeAddress));

    /// <summary>
    /// 建立 <c>MIN</c> 公式。
    /// </summary>
    /// <param name="rangeAddress">要計算的範圍</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Min(string rangeAddress) => Function("MIN", Range(rangeAddress));

    /// <summary>
    /// 建立 <c>MAX</c> 公式。
    /// </summary>
    /// <param name="rangeAddress">要計算的範圍</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Max(string rangeAddress) => Function("MAX", Range(rangeAddress));

    /// <summary>
    /// 建立 <c>COUNT</c> 公式。
    /// </summary>
    /// <param name="rangeAddress">要計數的範圍</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Count(string rangeAddress) => Function("COUNT", Range(rangeAddress));

    /// <summary>
    /// 建立 <c>COUNTA</c> 公式。
    /// </summary>
    /// <param name="rangeAddress">要計數的範圍</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula CountA(string rangeAddress) => Function("COUNTA", Range(rangeAddress));

    /// <summary>
    /// 建立函式呼叫。
    /// </summary>
    /// <param name="name">函式名稱</param>
    /// <param name="arguments">函式引數</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Function(string name, params OdfSpreadsheetFormula[] arguments)
    {
        string body = name + "(" + string.Join(";", arguments.Select(argument => argument.Body)) + ")";
        return OdfSpreadsheetFormula.Atom(body);
    }
}

/// <summary>
/// 表示可轉成 <c>table:formula</c> 屬性的 OpenFormula 公式片段。
/// </summary>
public readonly struct OdfSpreadsheetFormula : IEquatable<OdfSpreadsheetFormula>
{
    private readonly int _precedence;

    private OdfSpreadsheetFormula(string body, int precedence)
    {
        Body = body;
        _precedence = precedence;
    }

    /// <summary>
    /// 取得不含 <c>of:=</c> 前綴的公式主體。
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// 建立原子公式片段。
    /// </summary>
    /// <param name="body">公式主體</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Atom(string body) => new(body ?? string.Empty, 3);

    /// <summary>
    /// 建立原始公式片段，會移除選用的 <c>of:=</c> 前綴。
    /// </summary>
    /// <param name="body">公式主體</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Raw(string body)
    {
        if (body is null)
        {
            return new(string.Empty, 3);
        }

        return body.StartsWith("of:=", StringComparison.Ordinal)
            ? new(body.Substring(4), 3)
            : new(body, 3);
    }

    /// <summary>
    /// 建立儲存格或範圍參照。
    /// </summary>
    /// <param name="address">儲存格或範圍位址</param>
    /// <returns>公式片段</returns>
    public static OdfSpreadsheetFormula Reference(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return new("[]", 3);
        }

        string trimmed = address.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return new(trimmed, 3);
        }

        int rangeSeparator = trimmed.IndexOf(':');
        if (rangeSeparator >= 0)
        {
            string start = NormalizeAddress(trimmed.Substring(0, rangeSeparator));
            string end = NormalizeAddress(trimmed.Substring(rangeSeparator + 1));
            return new("[" + start + ":" + end + "]", 3);
        }

        return new("[" + NormalizeAddress(trimmed) + "]", 3);
    }

    /// <summary>
    /// 建立加法公式。
    /// </summary>
    /// <param name="right">右側運算元</param>
    /// <returns>公式片段</returns>
    public OdfSpreadsheetFormula Add(OdfSpreadsheetFormula right) => Binary("+", right, 1);

    /// <summary>
    /// 建立加法公式。
    /// </summary>
    /// <param name="right">右側數值</param>
    /// <returns>公式片段</returns>
    public OdfSpreadsheetFormula Add(double right) => Add(Formula.Number(right));

    /// <summary>
    /// 建立減法公式。
    /// </summary>
    /// <param name="right">右側運算元</param>
    /// <returns>公式片段</returns>
    public OdfSpreadsheetFormula Subtract(OdfSpreadsheetFormula right) => Binary("-", right, 1, parenthesizeRightOnEqual: true);

    /// <summary>
    /// 建立乘法公式。
    /// </summary>
    /// <param name="right">右側運算元</param>
    /// <returns>公式片段</returns>
    public OdfSpreadsheetFormula Multiply(OdfSpreadsheetFormula right) => Binary("*", right, 2);

    /// <summary>
    /// 建立乘法公式。
    /// </summary>
    /// <param name="right">右側數值</param>
    /// <returns>公式片段</returns>
    public OdfSpreadsheetFormula Multiply(double right) => Multiply(Formula.Number(right));

    /// <summary>
    /// 建立除法公式。
    /// </summary>
    /// <param name="right">右側運算元</param>
    /// <returns>公式片段</returns>
    public OdfSpreadsheetFormula Divide(OdfSpreadsheetFormula right) => Binary("/", right, 2, parenthesizeRightOnEqual: true);

    /// <summary>
    /// 傳回包含 <c>of:=</c> 前綴的公式字串。
    /// </summary>
    /// <returns>可直接寫入 <c>table:formula</c> 的公式</returns>
    public override string ToString() => "of:=" + Body;

    /// <summary>
    /// 將公式片段隱式轉換成 <c>table:formula</c> 字串。
    /// </summary>
    /// <param name="formula">公式片段</param>
    public static implicit operator string(OdfSpreadsheetFormula formula) => formula.ToString();

    /// <summary>
    /// 判斷目前公式是否等於另一個公式。
    /// </summary>
    /// <param name="other">另一個公式</param>
    /// <returns>若公式主體相同則為 <see langword="true"/></returns>
    public bool Equals(OdfSpreadsheetFormula other) => string.Equals(Body, other.Body, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfSpreadsheetFormula other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Body ?? string.Empty);

    /// <summary>
    /// 判斷兩個公式是否相等。
    /// </summary>
    /// <param name="left">左側公式</param>
    /// <param name="right">右側公式</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfSpreadsheetFormula left, OdfSpreadsheetFormula right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個公式是否不相等。
    /// </summary>
    /// <param name="left">左側公式</param>
    /// <param name="right">右側公式</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfSpreadsheetFormula left, OdfSpreadsheetFormula right) => !left.Equals(right);

    private static string NormalizeAddress(string address)
    {
        string trimmed = address.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) || trimmed.Contains(".", StringComparison.Ordinal)
            ? trimmed
            : "." + trimmed;
    }

    private OdfSpreadsheetFormula Binary(string op, OdfSpreadsheetFormula right, int precedence, bool parenthesizeRightOnEqual = false)
    {
        string leftBody = ParenthesizeIfNeeded(this, precedence, false);
        string rightBody = ParenthesizeIfNeeded(right, precedence, parenthesizeRightOnEqual);
        return new(leftBody + op + rightBody, precedence);
    }

    private static string ParenthesizeIfNeeded(OdfSpreadsheetFormula formula, int parentPrecedence, bool parenthesizeOnEqual)
    {
        bool needsParentheses = formula._precedence < parentPrecedence ||
            parenthesizeOnEqual && formula._precedence == parentPrecedence;
        return needsParentheses ? "(" + formula.Body + ")" : formula.Body;
    }
}
