namespace OdfKit.Text;

/// <summary>
/// 表示從文字文件中反向提取出的範本欄位資料。
/// </summary>
public sealed class OdfExtractedFieldInfo
{
    /// <summary>
    /// 初始化 <see cref="OdfExtractedFieldInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">欄位名稱</param>
    /// <param name="value">欄位值</param>
    /// <param name="source">欄位來源</param>
    public OdfExtractedFieldInfo(string name, string value, OdfExtractedFieldSource source)
    {
        Name = name;
        Value = value;
        Source = source;
    }

    /// <summary>
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 取得欄位值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得欄位來源。
    /// </summary>
    public OdfExtractedFieldSource Source { get; }
}
