using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using MiniExcelLibs;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// A content checksum over every row of the competitive benchmark dataset, used to prove that each reader
/// produced exactly the data the generator wrote, so a fast reader cannot win by skipping or mis-reading data.
/// 跨套件對比基準資料集的內容檢查碼：涵蓋每一列資料，用來證明各讀取器讀回的內容與產生器寫入的完全一致，
/// 避免讀得快卻略過或讀錯資料。
/// </summary>
/// <param name="Rows">The number of rows read. / 讀到的列數。</param>
/// <param name="IdSum">The sum of the identifier column. / 識別碼欄位總和。</param>
/// <param name="AmountSum">The sum of the amount column. / 金額欄位總和。</param>
/// <param name="QuantitySum">The sum of the quantity column. / 數量欄位總和。</param>
/// <param name="DateMinutesSum">The sum of minutes since the base date. / 與基準日期相差分鐘數的總和。</param>
/// <param name="ActiveCount">The number of true boolean flags. / 為 true 的布林旗標數。</param>
/// <param name="ScoreSum">The sum of the score column. / 分數欄位總和。</param>
/// <param name="NameChars">The total length of the name column. / 名稱欄位的總字元數。</param>
/// <param name="CategoryChars">The total length of the category column. / 分類欄位的總字元數。</param>
/// <param name="SequenceSum">The sum of the sequence column. / 序號欄位總和。</param>
/// <param name="NotesChars">The total length of the notes column. / 備註欄位的總字元數。</param>
internal readonly record struct CompetitiveReadChecksum(
    long Rows,
    long IdSum,
    double AmountSum,
    long QuantitySum,
    long DateMinutesSum,
    long ActiveCount,
    double ScoreSum,
    long NameChars,
    long CategoryChars,
    long SequenceSum,
    long NotesChars)
{
    private const double RelativeTolerance = 1e-9;

    /// <summary>
    /// Serializes the checksum to a single machine-readable line.
    /// 將檢查碼序列化為單行機器可讀文字。
    /// </summary>
    /// <returns>The serialized checksum. / 序列化後的檢查碼。</returns>
    internal string Serialize() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Rows};{IdSum};{AmountSum:R};{QuantitySum};{DateMinutesSum};{ActiveCount};{ScoreSum:R};{NameChars};{CategoryChars};{SequenceSum};{NotesChars}");

    /// <summary>
    /// Parses a checksum produced by <see cref="Serialize"/>.
    /// 解析由 <see cref="Serialize"/> 產生的檢查碼。
    /// </summary>
    /// <param name="text">The serialized checksum. / 序列化的檢查碼。</param>
    /// <returns>The parsed checksum. / 解析後的檢查碼。</returns>
    internal static CompetitiveReadChecksum Parse(string text)
    {
        string[] p = text.Split(';');
        return new CompetitiveReadChecksum(
            long.Parse(p[0], CultureInfo.InvariantCulture),
            long.Parse(p[1], CultureInfo.InvariantCulture),
            double.Parse(p[2], CultureInfo.InvariantCulture),
            long.Parse(p[3], CultureInfo.InvariantCulture),
            long.Parse(p[4], CultureInfo.InvariantCulture),
            long.Parse(p[5], CultureInfo.InvariantCulture),
            double.Parse(p[6], CultureInfo.InvariantCulture),
            long.Parse(p[7], CultureInfo.InvariantCulture),
            long.Parse(p[8], CultureInfo.InvariantCulture),
            long.Parse(p[9], CultureInfo.InvariantCulture),
            long.Parse(p[10], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Compares two checksums; integer fields must match exactly, floating-point sums within a tiny relative tolerance.
    /// 比較兩個檢查碼：整數欄位必須完全相同，浮點數總和允許極小的相對誤差。
    /// </summary>
    /// <param name="other">The checksum to compare with. / 要比較的檢查碼。</param>
    /// <returns><see langword="true"/> when they match. / 相符時為 <see langword="true"/>。</returns>
    internal bool Matches(CompetitiveReadChecksum other) =>
        Rows == other.Rows
        && IdSum == other.IdSum
        && QuantitySum == other.QuantitySum
        && DateMinutesSum == other.DateMinutesSum
        && ActiveCount == other.ActiveCount
        && NameChars == other.NameChars
        && CategoryChars == other.CategoryChars
        && SequenceSum == other.SequenceSum
        && NotesChars == other.NotesChars
        && Close(AmountSum, other.AmountSum)
        && Close(ScoreSum, other.ScoreSum);

    private static bool Close(double left, double right) =>
        Math.Abs(left - right) <= RelativeTolerance * Math.Max(1.0, Math.Max(Math.Abs(left), Math.Abs(right)));
}

/// <summary>
/// Accumulates a <see cref="CompetitiveReadChecksum"/> one row at a time.
/// 逐列累加 <see cref="CompetitiveReadChecksum"/>。
/// </summary>
internal sealed class CompetitiveReadAccumulator
{
    private static readonly DateTime s_baseDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private long _rows;
    private long _idSum;
    private double _amountSum;
    private long _quantitySum;
    private long _dateMinutesSum;
    private long _activeCount;
    private double _scoreSum;
    private long _nameChars;
    private long _categoryChars;
    private long _sequenceSum;
    private long _notesChars;

    /// <summary>
    /// Adds one row.
    /// 加入一列。
    /// </summary>
    internal void Add(
        long id,
        string name,
        double amount,
        long quantity,
        DateTime orderDate,
        bool isActive,
        double score,
        string category,
        long sequenceNumber,
        string notes)
    {
        _rows++;
        _idSum += id;
        _amountSum += amount;
        _quantitySum += quantity;
        _dateMinutesSum += (long)Math.Round((orderDate - s_baseDate).TotalMinutes);
        if (isActive)
        {
            _activeCount++;
        }

        _scoreSum += score;
        _nameChars += name.Length;
        _categoryChars += category.Length;
        _sequenceSum += sequenceNumber;
        _notesChars += notes.Length;
    }

    /// <summary>
    /// Produces the checksum of everything added so far.
    /// 產生目前已加入內容的檢查碼。
    /// </summary>
    /// <returns>The checksum. / 檢查碼。</returns>
    internal CompetitiveReadChecksum ToChecksum() => new(
        _rows, _idSum, _amountSum, _quantitySum, _dateMinutesSum, _activeCount,
        _scoreSum, _nameChars, _categoryChars, _sequenceSum, _notesChars);
}

/// <summary>
/// Shared read implementations reused by both the BenchmarkDotNet-driven
/// <see cref="CompetitiveStreamReadBenchmarks"/> class and the standalone manual timing runner.
/// 供 BenchmarkDotNet 驅動的 <see cref="CompetitiveStreamReadBenchmarks"/> 類別與獨立手動計時執行器共用的讀取實作。
/// </summary>
/// <remarks>
/// Each reader reads the file written by the matching writer in <see cref="CompetitiveStreamWriters"/>:
/// OdfKit reads the .ods produced by <see cref="OdsStreamWriter"/>, while MiniExcel and ClosedXML both
/// read the same .xlsx produced by MiniExcel's streaming writer. This is a cross-format reference
/// comparison (ODS vs. XLSX), not a same-format contest; see docs/performance-comparison.md.
/// 每個讀取器讀取 <see cref="CompetitiveStreamWriters"/> 中對應寫入器產生的檔案：OdfKit 讀取
/// <see cref="OdsStreamWriter"/> 產生的 .ods；MiniExcel 與 ClosedXML 則讀取同一份由 MiniExcel 串流寫入器
/// 產生的 .xlsx。這是跨格式參考對比（ODS 對 XLSX），而非同格式對決；請見 docs/performance-comparison.md。
/// </remarks>
internal static class CompetitiveStreamReaders
{
    /// <summary>
    /// Computes the expected checksum directly from the deterministic generator.
    /// 直接由決定性產生器計算預期的檢查碼。
    /// </summary>
    /// <returns>The expected checksum. / 預期的檢查碼。</returns>
    internal static CompetitiveReadChecksum ComputeExpected()
    {
        var accumulator = new CompetitiveReadAccumulator();
        foreach (CompetitiveBenchmarkRow row in CompetitiveBenchmarkData.GenerateRows())
        {
            accumulator.Add(
                row.Id, row.Name, row.Amount, row.Quantity, row.OrderDate, row.IsActive,
                row.Score, row.Category, row.SequenceNumber, row.Notes);
        }

        return accumulator.ToChecksum();
    }

    /// <summary>
    /// Reads the .ods dataset with <see cref="OdsStreamReader"/> (streaming ODS reader).
    /// 以 <see cref="OdsStreamReader"/>（串流式 ODS 讀取器）讀取 .ods 資料集。
    /// </summary>
    /// <param name="path">The .ods file path. / .ods 檔案路徑。</param>
    /// <returns>The checksum of the rows read. / 讀到資料列的檢查碼。</returns>
    internal static CompetitiveReadChecksum ReadOdsStreamReader(string path)
    {
        var accumulator = new CompetitiveReadAccumulator();

        // 一百萬列 x 十欄的 content.xml 遠超過 OdsStreamReader 預設的單一 XML 文件 64 MiB 字元上限
        // （這是對不可信輸入的預設防護）。此基準資料是自己產生的可信任資料，因此只停用這一項上限
        // （0 代表不限制）；列數、欄數、repeat 與儲存格文字等其他限制維持預設，見 docs/security-limits.md。
        var options = new OdsStreamReaderOptions { MaxXmlCharactersInDocument = 0 };
        using var reader = new OdsStreamReader(path, options);
        while (reader.Read())
        {
            accumulator.Add(
                ToLong(reader.GetValue(0)),
                ToText(reader.GetValue(1)),
                ToDouble(reader.GetValue(2)),
                ToLong(reader.GetValue(3)),
                ToDate(reader.GetValue(4)),
                ToBool(reader.GetValue(5)),
                ToDouble(reader.GetValue(6)),
                ToText(reader.GetValue(7)),
                ToLong(reader.GetValue(8)),
                ToText(reader.GetValue(9)));
        }

        return accumulator.ToChecksum();
    }

    /// <summary>
    /// Reads the .xlsx dataset with MiniExcel's streaming <c>Query</c> API.
    /// 以 MiniExcel 的串流式 <c>Query</c> API 讀取 .xlsx 資料集。
    /// </summary>
    /// <param name="path">The .xlsx file path. / .xlsx 檔案路徑。</param>
    /// <returns>The checksum of the rows read. / 讀到資料列的檢查碼。</returns>
    internal static CompetitiveReadChecksum ReadMiniExcel(string path)
    {
        var accumulator = new CompetitiveReadAccumulator();
        using FileStream stream = File.OpenRead(path);

        // useHeaderRow: false，使第一列也視為資料列（寫入端同樣未輸出表頭列）。
        foreach (object item in stream.Query(useHeaderRow: false))
        {
            var row = (IDictionary<string, object?>)item;
            accumulator.Add(
                ToLong(row["A"]),
                ToText(row["B"]),
                ToDouble(row["C"]),
                ToLong(row["D"]),
                ToDate(row["E"]),
                ToBool(row["F"]),
                ToDouble(row["G"]),
                ToText(row["H"]),
                ToLong(row["I"]),
                ToText(row["J"]));
        }

        return accumulator.ToChecksum();
    }

    /// <summary>
    /// Reads the .xlsx dataset with ClosedXML's in-memory DOM loader, used here as the non-streaming
    /// (whole-workbook-in-memory) control group.
    /// 以 ClosedXML 的記憶體內 DOM 載入器讀取 .xlsx 資料集，於此作為非串流（整份活頁簿常駐記憶體）的對照組。
    /// </summary>
    /// <param name="path">The .xlsx file path. / .xlsx 檔案路徑。</param>
    /// <returns>The checksum of the rows read. / 讀到資料列的檢查碼。</returns>
    internal static CompetitiveReadChecksum ReadClosedXml(string path)
    {
        var accumulator = new CompetitiveReadAccumulator();
        using var workbook = new XLWorkbook(path);
        IXLWorksheet sheet = workbook.Worksheet(1);
        foreach (IXLRow row in sheet.RowsUsed())
        {
            accumulator.Add(
                (long)row.Cell(1).GetDouble(),
                row.Cell(2).GetString(),
                row.Cell(3).GetDouble(),
                (long)row.Cell(4).GetDouble(),
                row.Cell(5).GetDateTime(),
                row.Cell(6).GetBoolean(),
                row.Cell(7).GetDouble(),
                row.Cell(8).GetString(),
                (long)row.Cell(9).GetDouble(),
                row.Cell(10).GetString());
        }

        return accumulator.ToChecksum();
    }

    private static long ToLong(object? value) =>
        Convert.ToInt64(Math.Round(ToDouble(value)), CultureInfo.InvariantCulture);

    private static double ToDouble(object? value) => value switch
    {
        double number => number,
        null => throw new InvalidDataException("預期為數字但讀到空值。"),
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture),
    };

    private static string ToText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)
        ?? throw new InvalidDataException("預期為文字但讀到空值。");

    private static bool ToBool(object? value) => value switch
    {
        bool flag => flag,
        string text => bool.Parse(text),
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0,
    };

    private static DateTime ToDate(object? value) => value switch
    {
        DateTime date => date,
        double serial => DateTime.FromOADate(serial),
        string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        _ => throw new InvalidDataException("預期為日期但讀到無法辨識的值。"),
    };
}
