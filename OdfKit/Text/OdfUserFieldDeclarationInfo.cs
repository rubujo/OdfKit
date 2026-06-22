namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中一個使用者欄位（範本變數）宣告的摘要資訊。
/// </summary>
/// <param name="name">欄位名稱</param>
/// <param name="valueType">值類型（例如 <c>string</c>、<c>float</c>、<c>boolean</c>、<c>date</c>、<c>time</c>）</param>
/// <param name="value">欄位目前的值原文；若未設定則為 <see langword="null"/></param>
public sealed class OdfUserFieldDeclarationInfo(string name, string valueType, string? value)
{
    /// <summary>
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 取得值類型。
    /// </summary>
    public string ValueType { get; } = valueType;

    /// <summary>
    /// 取得欄位目前的值原文。
    /// </summary>
    public string? Value { get; } = value;
}
