namespace OdfKit.Text;

/// <summary>
/// Represents odf extracted field info.
/// 表示從文字文件中反向提取出的範本欄位資料。
/// </summary>
public sealed class OdfExtractedFieldInfo
{
    /// <summary>
    /// Provides odf extracted field info.
    /// 初始化 <see cref="OdfExtractedFieldInfo"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The name or identifier. / 欄位名稱</param>
    /// <param name="value">The text or value. / 欄位值</param>
    /// <param name="source">The stream or target object. / 欄位來源</param>
    public OdfExtractedFieldInfo(string name, string value, OdfExtractedFieldSource source)
    {
        Name = name;
        Value = value;
        Source = source;
    }

    /// <summary>
    /// Gets name.
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets value.
    /// 取得欄位值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets source.
    /// 取得欄位來源。
    /// </summary>
    public OdfExtractedFieldSource Source { get; }
}
