using System.Collections.Generic;
using System.Globalization;

namespace OdfKit.Benchmarks;

/// <summary>
/// A single deterministic benchmark row containing mixed string, numeric, and date column types.
/// 一列決定性基準測試資料，包含混合的字串、數字與日期欄位型別。
/// </summary>
/// <param name="Id">The row identifier. / 列識別碼。</param>
/// <param name="Name">The row display name. / 列顯示名稱。</param>
/// <param name="Amount">A decimal-like monetary value. / 類似小數金額的數值欄位。</param>
/// <param name="Quantity">An integer quantity value. / 整數數量欄位。</param>
/// <param name="OrderDate">A date/time value. / 日期時間欄位。</param>
/// <param name="IsActive">A boolean flag value. / 布林旗標欄位。</param>
/// <param name="Score">A floating-point score value. / 浮點數分數欄位。</param>
/// <param name="Category">A short repeating string category. / 短字串分類欄位。</param>
/// <param name="SequenceNumber">A large integer sequence value. / 大整數序號欄位。</param>
/// <param name="Notes">A free-form Traditional Chinese text column. / 自由格式正體中文文字欄位。</param>
internal sealed record CompetitiveBenchmarkRow(
    long Id,
    string Name,
    double Amount,
    int Quantity,
    System.DateTime OrderDate,
    bool IsActive,
    double Score,
    string Category,
    long SequenceNumber,
    string Notes);

/// <summary>
/// Deterministic row generator shared by all competitive stream-write benchmark scenarios.
/// 供所有跨套件串流寫入基準情境共用的決定性資料列產生器。
/// </summary>
internal static class CompetitiveBenchmarkData
{
    /// <summary>
    /// The row count used by the competitive benchmark scenario (1,000,000 rows).
    /// 跨套件對比基準情境使用的列數（一百萬列）。
    /// </summary>
    internal const int RowCount = 1_000_000;

    /// <summary>
    /// The column count used by the competitive benchmark scenario (10 columns).
    /// 跨套件對比基準情境使用的欄數（十欄）。
    /// </summary>
    internal const int ColumnCount = 10;

    private const int Seed = 20260709;

    private static readonly string[] s_categories = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon"];

    /// <summary>
    /// Generates <see cref="RowCount"/> deterministic rows using a fixed seed, in a lazily
    /// evaluated (streaming-friendly) sequence.
    /// 以固定種子產生 <see cref="RowCount"/> 列決定性資料，並以延遲求值（適合串流）的序列傳回。
    /// </summary>
    /// <returns>A lazily evaluated sequence of benchmark rows. / 延遲求值的基準測試資料列序列。</returns>
    internal static IEnumerable<CompetitiveBenchmarkRow> GenerateRows()
    {
        var random = new System.Random(Seed);
        var baseDate = new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        for (int i = 0; i < RowCount; i++)
        {
            yield return new CompetitiveBenchmarkRow(
                Id: i,
                Name: string.Create(CultureInfo.InvariantCulture, $"Item-{i:D7}"),
                Amount: System.Math.Round(random.NextDouble() * 10_000, 2),
                Quantity: random.Next(1, 500),
                OrderDate: baseDate.AddMinutes(i),
                IsActive: i % 3 == 0,
                Score: System.Math.Round(random.NextDouble() * 100, 4),
                Category: s_categories[i % s_categories.Length],
                SequenceNumber: i * 7L,
                Notes: string.Create(CultureInfo.InvariantCulture, $"備註 {i}"));
        }
    }
}
