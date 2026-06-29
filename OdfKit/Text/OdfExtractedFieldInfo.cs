namespace OdfKit.Text;

/// <summary>
/// Represents template field data extracted back out of a text document.
/// 表示從文字文件中反向提取出的範本欄位資料。
/// </summary>
public sealed class OdfExtractedFieldInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfExtractedFieldInfo"/> class.
    /// 初始化 <see cref="OdfExtractedFieldInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The field name. / 欄位名稱。</param>
    /// <param name="value">The field value. / 欄位值。</param>
    /// <param name="source">The field source. / 欄位來源。</param>
    public OdfExtractedFieldInfo(string name, string value, OdfExtractedFieldSource source)
    {
        Name = name;
        Value = value;
        Source = source;
    }

    /// <summary>
    /// Gets the field name.
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the field value.
    /// 取得欄位值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the field source.
    /// 取得欄位來源。
    /// </summary>
    public OdfExtractedFieldSource Source { get; }
}
