namespace OdfKit.Text;

/// <summary>
/// Represents odf user field declaration info.
/// 表示文字文件中一個使用者欄位（範本變數）宣告的摘要資訊。
/// </summary>
/// <param name="name">The name or identifier. / 欄位名稱</param>
/// <param name="valueType">The text or value. / 值類型（例如 <c>string</c>、<c>float</c>、<c>boolean</c>、<c>date</c>、<c>time</c>）</param>
/// <param name="value">The text or value. / 欄位目前的值原文；若未設定則為 <see langword="null"/></param>
public sealed class OdfUserFieldDeclarationInfo(string name, string valueType, string? value)
{
    /// <summary>
    /// Gets name.
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets value type.
    /// 取得值類型。
    /// </summary>
    public string ValueType { get; } = valueType;

    /// <summary>
    /// Gets value.
    /// 取得欄位目前的值原文。
    /// </summary>
    public string? Value { get; } = value;
}
