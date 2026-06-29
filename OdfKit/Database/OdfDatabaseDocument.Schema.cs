namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    /// <summary>
    /// Gets the schema definition accessor for this database document, providing high-level table structure and relationship settings.
    /// 取得此資料庫文件的 Schema 定義存取器，提供高階的資料表結構與關聯設定。
    /// </summary>
    public OdfDatabaseSchema Schema => new(this);
}
