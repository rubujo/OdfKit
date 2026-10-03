using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Conversion;

/// <summary>
/// ODS to XLSX number formats and OpenFormula reference translation.
/// ODS → XLSX 的數字格式與 OpenFormula 參照翻譯。
/// </summary>
public static partial class OdfToXlsxConverter
{
    // ---------- OpenFormula 參照 ----------

    // OpenFormula 的儲存格參照包在方括號內：[.B2]、[.B2:.C5]、[$Sheet.B2]、['Sheet name'.B2]、[$'Sheet'.B2:.C5]。
    private static readonly Regex OpenFormulaReference = new(
        @"\[\$?(?<sheet>'(?:[^']|'')*'|[^.\]:'\[]+)?\.(?<first>\$?[A-Za-z]{1,3}\$?\d+)(?::\$?(?:'(?:[^']|'')*'|[^.\]:'\[]+)?\.(?<second>\$?[A-Za-z]{1,3}\$?\d+))?\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// 把 OpenFormula 的方括號參照改成 A1 參照（工作表前綴改用驚嘆號），並把函數參數分隔的分號改成逗號。
    /// 只處理字串常數以外的片段。
    /// </summary>
    private static string TranslateOpenFormulaSyntax(string formula)
    {
        return FormulaTranslationHelper.ReplaceOutsideStringLiterals(formula, segment =>
        {
            string replaced = OpenFormulaReference.Replace(segment, match =>
            {
                string sheet = match.Groups["sheet"].Success ? match.Groups["sheet"].Value + "!" : string.Empty;
                string reference = match.Groups["first"].Value;
                if (match.Groups["second"].Success)
                {
                    reference += ":" + match.Groups["second"].Value;
                }

                return sheet + reference;
            });
            return replaced.Replace(';', ',');
        });
    }

    // ---------- 數字格式 ----------

    private sealed class NumberFormatMap
    {
        private readonly Dictionary<string, string> _dataStyleByCellStyle = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _parentByCellStyle = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _patternByDataStyle = new(StringComparer.Ordinal);

        internal NumberFormatMap(OdfNode contentDom, OdfNode stylesDom)
        {
            foreach (OdfNode root in new[] { stylesDom, contentDom })
            {
                foreach (OdfNode section in root.Children)
                {
                    if (section.NamespaceUri != OdfNamespaces.Office
                        || (section.LocalName != "automatic-styles" && section.LocalName != "styles"))
                    {
                        continue;
                    }

                    foreach (OdfNode style in section.Children)
                    {
                        string name = style.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
                        if (name.Length == 0)
                        {
                            continue;
                        }

                        if (style.NamespaceUri == OdfNamespaces.Style && style.LocalName == "style")
                        {
                            string? dataStyle = style.GetAttribute("data-style-name", OdfNamespaces.Style);
                            if (!string.IsNullOrEmpty(dataStyle))
                            {
                                _dataStyleByCellStyle[name] = dataStyle!;
                            }

                            string? parent = style.GetAttribute("parent-style-name", OdfNamespaces.Style);
                            if (!string.IsNullOrEmpty(parent))
                            {
                                _parentByCellStyle[name] = parent!;
                            }
                        }
                        else if (style.NamespaceUri == OdfNamespaces.Number)
                        {
                            string? pattern = BuildPattern(style);
                            if (pattern is not null)
                            {
                                _patternByDataStyle[name] = pattern;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 取得儲存格樣式（含父樣式鏈）對應的 Excel 數字格式；沒有數字格式時為 null。
        /// </summary>
        internal string? GetPattern(string cellStyleName)
        {
            string? current = cellStyleName;
            for (int depth = 0; depth < 8 && current is not null; depth++)
            {
                if (_dataStyleByCellStyle.TryGetValue(current, out string? dataStyle)
                    && _patternByDataStyle.TryGetValue(dataStyle, out string? pattern))
                {
                    return pattern;
                }

                current = _parentByCellStyle.TryGetValue(current, out string? parent) ? parent : null;
            }

            return null;
        }

        private static string? BuildPattern(OdfNode style)
        {
            switch (style.LocalName)
            {
                case "date-style":
                case "time-style":
                    return BuildDateTimePattern(style);
                case "number-style":
                    return BuildNumberPattern(style, percent: false);
                case "percentage-style":
                    return BuildNumberPattern(style, percent: true);
                case "currency-style":
                    return BuildNumberPattern(style, percent: false);
                default:
                    return null;
            }
        }

        private static bool IsLong(OdfNode node) =>
            string.Equals(node.GetAttribute("style", OdfNamespaces.Number), "long", StringComparison.Ordinal);

        private static string BuildDateTimePattern(OdfNode style)
        {
            var builder = new StringBuilder();
            // 小時之後的分鐘在 Excel 以 m 表示；單獨出現的月份也是 m，用前一個欄位判斷會失準，
            // ODF 已用 number:minutes 與 number:month 區分，直接輸出對應字母即可。
            foreach (OdfNode part in style.Children)
            {
                if (part.NamespaceUri != OdfNamespaces.Number)
                {
                    continue;
                }

                switch (part.LocalName)
                {
                    case "day":
                        builder.Append(IsLong(part) ? "dd" : "d");
                        break;
                    case "month":
                        bool textual = string.Equals(part.GetAttribute("textual", OdfNamespaces.Number), "true", StringComparison.Ordinal);
                        builder.Append(textual ? (IsLong(part) ? "mmmm" : "mmm") : (IsLong(part) ? "mm" : "m"));
                        break;
                    case "year":
                        builder.Append(IsLong(part) ? "yyyy" : "yy");
                        break;
                    case "day-of-week":
                        builder.Append(IsLong(part) ? "dddd" : "ddd");
                        break;
                    case "hours":
                        builder.Append(IsLong(part) ? "hh" : "h");
                        break;
                    case "minutes":
                        builder.Append(IsLong(part) ? "mm" : "m");
                        break;
                    case "seconds":
                        builder.Append(IsLong(part) ? "ss" : "s");
                        if (int.TryParse(part.GetAttribute("decimal-places", OdfNamespaces.Number), NumberStyles.Integer, CultureInfo.InvariantCulture, out int places) && places > 0)
                        {
                            builder.Append('.').Append('0', Math.Min(places, 6));
                        }

                        break;
                    case "am-pm":
                        builder.Append("AM/PM");
                        break;
                    case "text":
                        builder.Append(QuoteLiteral(part.TextContent));
                        break;
                }
            }

            return builder.Length == 0 ? "General" : builder.ToString();
        }

        private static string? BuildNumberPattern(OdfNode style, bool percent)
        {
            var prefix = new StringBuilder();
            var suffix = new StringBuilder();
            string? numberPattern = null;
            foreach (OdfNode part in style.Children)
            {
                if (part.NamespaceUri != OdfNamespaces.Number)
                {
                    continue;
                }

                switch (part.LocalName)
                {
                    case "number":
                        numberPattern = BuildDigits(part);
                        break;
                    case "scientific-number":
                        numberPattern = BuildDigits(part) + "E+00";
                        break;
                    case "currency-symbol":
                        (numberPattern is null ? prefix : suffix).Append(QuoteLiteral(part.TextContent));
                        break;
                    case "text":
                        // 百分比樣式裡的 % 要保留成 Excel 的百分比符號（會把值乘以 100），不能加引號變成字面文字。
                        (numberPattern is null ? prefix : suffix).Append(
                            percent && part.TextContent == "%" ? "%" : QuoteLiteral(part.TextContent));
                        break;
                }
            }

            if (numberPattern is null)
            {
                return null;
            }

            if (percent && !OdfKit.Internal.OdfStringHelper.Contains(suffix.ToString(), "%", StringComparison.Ordinal))
            {
                suffix.Append('%');
            }

            return prefix + numberPattern + suffix;
        }

        private static string BuildDigits(OdfNode number)
        {
            int decimals = int.TryParse(number.GetAttribute("decimal-places", OdfNamespaces.Number), NumberStyles.Integer, CultureInfo.InvariantCulture, out int d)
                ? Math.Max(0, Math.Min(d, 15))
                : 0;
            int minimumInteger = int.TryParse(number.GetAttribute("min-integer-digits", OdfNamespaces.Number), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m)
                ? Math.Max(1, Math.Min(m, 15))
                : 1;
            bool grouping = string.Equals(number.GetAttribute("grouping", OdfNamespaces.Number), "true", StringComparison.Ordinal);

            string integerPart = grouping
                ? "#,##" + new string('0', minimumInteger)
                : new string('0', minimumInteger);

            return decimals > 0 ? integerPart + "." + new string('0', decimals) : integerPart;
        }

        // 字面文字：純分隔符號（空白、-、.、/、:、,）直接輸出，其餘以雙引號包起來。
        private static string QuoteLiteral(string? text)
        {
            string value = text ?? string.Empty;
            if (value.Length == 0)
            {
                return string.Empty;
            }

            if (value.All(character => character is ' ' or '-' or '.' or '/' or ':' or ','))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    private static readonly ConditionalWeakTable<OdfDocument, NumberFormatMap> s_numberFormats = new();

    private static string? GetExcelNumberFormat(OdfTableSheet sheet, string styleName)
    {
        OdfDocument document = sheet.Document;
        NumberFormatMap map = s_numberFormats.GetValue(document, doc => new NumberFormatMap(doc.ContentDom, doc.StylesDom));
        return map.GetPattern(styleName);
    }
}
