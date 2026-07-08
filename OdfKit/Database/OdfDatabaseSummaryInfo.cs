namespace OdfKit.Database;

/// <summary>
/// Summarizes practical DatabaseDocument content for inspection workflows.
/// 彙總 DatabaseDocument 內容，供實務檢查流程使用。
/// </summary>
/// <param name="ConnectionHref">The connection resource href. / 連線資源 href。</param>
/// <param name="TableCount">The table count. / 資料表數量。</param>
/// <param name="QueryCount">The query count. / 查詢數量。</param>
/// <param name="FormCount">The form count. / 表單數量。</param>
/// <param name="ReportCount">The report count. / 報表數量。</param>
/// <param name="DataSourceSettingCount">The data source setting count. / 資料來源設定數量。</param>
public sealed record OdfDatabaseSummaryInfo(
    string? ConnectionHref,
    int TableCount,
    int QueryCount,
    int FormCount,
    int ReportCount,
    int DataSourceSettingCount);
