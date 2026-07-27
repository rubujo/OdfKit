using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OdfKit.Spreadsheet;

internal static class OdfSpreadsheetFormulaReferenceShifter
{
    private static readonly Regex s_cellReferenceRegex = new(
        @"(?<sheet>(?:'[^']+'|[A-Za-z_][A-Za-z0-9_ ]*)\.)?(?<columnAbs>\$?)(?<column>[A-Za-z]+)(?<rowAbs>\$?)(?<row>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string ShiftRelativeRows(string formula, int rowOffset)
    {
        if (string.IsNullOrEmpty(formula) || rowOffset == 0)
        {
            return formula;
        }

        var builder = new StringBuilder(formula.Length);
        int scanIndex = 0;
        while (scanIndex < formula.Length)
        {
            int open = formula.IndexOf('[', scanIndex);
            if (open < 0)
            {
                builder.Append(formula, scanIndex, formula.Length - scanIndex);
                break;
            }

            int close = formula.IndexOf(']', open + 1);
            if (close < 0)
            {
                builder.Append(formula, scanIndex, formula.Length - scanIndex);
                break;
            }

            builder.Append(formula, scanIndex, open - scanIndex);
            string referenceBody = formula.Substring(open + 1, close - open - 1);
            builder.Append('[')
                .Append(ShiftReferenceBody(referenceBody, rowOffset))
                .Append(']');
            scanIndex = close + 1;
        }

        return builder.ToString();
    }

    private static string ShiftReferenceBody(string body, int rowOffset)
    {
        if (global::OdfKit.Internal.OdfStringHelper.StartsWith(body, '$') ||
            body.Contains("://", System.StringComparison.Ordinal))
        {
            return body;
        }

        return s_cellReferenceRegex.Replace(body, match =>
        {
            if (match.Groups["rowAbs"].Value == "$")
            {
                return match.Value;
            }

            if (!int.TryParse(match.Groups["row"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowNumber))
            {
                return match.Value;
            }

            int shifted = rowNumber + rowOffset;
            if (shifted < 1)
            {
                return match.Value;
            }

            return match.Groups["sheet"].Value +
                match.Groups["columnAbs"].Value +
                match.Groups["column"].Value +
                match.Groups["rowAbs"].Value +
                shifted.ToString(CultureInfo.InvariantCulture);
        });
    }
}
