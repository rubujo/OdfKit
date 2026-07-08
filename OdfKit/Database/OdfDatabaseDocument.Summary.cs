namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    /// <summary>
    /// Gets a practical summary of the database document.
    /// 取得資料庫文件的實務摘要。
    /// </summary>
    /// <returns>The database summary. / 資料庫摘要。</returns>
    public OdfDatabaseSummaryInfo GetSummary() =>
        new(
            ConnectionHref,
            GetTables().Count,
            GetQueries().Count,
            GetForms().Count,
            GetReports().Count,
            GetDataSourceSettings().Count);
}
