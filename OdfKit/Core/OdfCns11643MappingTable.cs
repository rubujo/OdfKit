using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Parses and joins CNS 11643 code mapping tables.
/// 解析並聯結 CNS 11643 字碼對照表。
/// </summary>
/// <remarks>
/// Mapping data is available from the CNS open data service under the Open Government Data License, version 1.0; this repository does not embed that data.
/// 對照資料來源為全字庫開放資料，依政府資料開放授權條款第 1 版提供；本倉庫不內建該資料。
/// </remarks>
public static class OdfCns11643MappingTable
{
    /// <summary>
    /// Parses a CNS 11643 mapping table from a text reader.
    /// 從文字讀取器解析 CNS 11643 字碼對照表。
    /// </summary>
    /// <param name="reader">The reader containing the official tab-delimited mapping data. / 包含官方跳格分隔對照資料的讀取器。</param>
    /// <returns>A CNS-code-to-value mapping using ordinal key comparison. / 使用序數鍵值比較的 CNS 字碼至數值對應表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>. / <paramref name="reader"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="FormatException">A nonblank line does not use the expected format. / 非空白行不符合預期格式。</exception>
    public static IReadOnlyDictionary<string, int> Parse(TextReader reader)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(reader, nameof(reader));

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        int parsedEntryCount = 0;
        var lineReader = new OdfBoundedLineReader(reader);
        string? originalLine;
        while ((originalLine = lineReader.ReadLine()) is not null)
        {
            string line = originalLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(TabSeparator);
            if (fields.Length != 2)
            {
                throw InvalidLine(originalLine);
            }

            string cnsCode = fields[0].Trim();
            string valueText = fields[1].Trim();
            if (!cnsCode.Contains('-') ||
                !int.TryParse(valueText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value) ||
                value < 0)
            {
                throw InvalidLine(originalLine);
            }

            result[cnsCode] = value;
            parsedEntryCount++;
            OdfCodePointMappingTable.EnsureEntryBudget(parsedEntryCount);
        }

        return result;
    }

    /// <summary>
    /// Joins two mappings by their shared CNS 11643 codes.
    /// 依共用的 CNS 11643 字碼聯結兩份對照表。
    /// </summary>
    /// <param name="cnsToSource">The CNS-code-to-source-value mapping. / CNS 字碼至來源值的對應表。</param>
    /// <param name="cnsToTarget">The CNS-code-to-target-value mapping. / CNS 字碼至目標值的對應表。</param>
    /// <returns>A source-value-to-target-value mapping for shared CNS codes. / 共用 CNS 字碼所形成的來源值至目標值對應表。</returns>
    /// <exception cref="ArgumentNullException">Either mapping is <see langword="null"/>. / 任一對應表為 <see langword="null"/>。</exception>
    public static IReadOnlyDictionary<int, int> JoinOnCns(
        IReadOnlyDictionary<string, int> cnsToSource,
        IReadOnlyDictionary<string, int> cnsToTarget)
        => OdfCodePointMappingTable.Join(cnsToSource, cnsToTarget);

    private static readonly char[] TabSeparator = ['\t'];

    private static FormatException InvalidLine(string line) =>
        new(OdfLocalizer.GetMessage(
            "Err_OdfCnsMappingTable_InvalidLine",
            OdfCodePointMappingTable.FormatLineForMessage(line)));
}
