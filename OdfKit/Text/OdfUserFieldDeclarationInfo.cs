namespace OdfKit.Text;

/// <summary>
/// Represents summary information for a user field (template variable) declaration in a text document.
/// 表示文字文件中一個使用者欄位（範本變數）宣告的摘要資訊。
/// </summary>
/// <param name="name">The field name. / 欄位名稱。</param>
/// <param name="valueType">The value type (e.g. <c>string</c>, <c>float</c>, <c>boolean</c>, <c>date</c>, <c>time</c>). / 值類型（例如 <c>string</c>、<c>float</c>、<c>boolean</c>、<c>date</c>、<c>time</c>）。</param>
/// <param name="value">The raw text of the field's current value; <see langword="null"/> if not set. / 欄位目前的值原文；若未設定則為 <see langword="null"/>。</param>
public sealed class OdfUserFieldDeclarationInfo(string name, string valueType, string? value)
{
    /// <summary>
    /// Gets the field name.
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the value type.
    /// 取得值類型。
    /// </summary>
    public string ValueType { get; } = valueType;

    /// <summary>
    /// Gets the raw text of the field's current value.
    /// 取得欄位目前的值原文。
    /// </summary>
    public string? Value { get; } = value;
}
