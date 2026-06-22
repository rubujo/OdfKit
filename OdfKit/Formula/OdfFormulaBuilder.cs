namespace OdfKit.Formula;

/// <summary>
/// 提供 <see cref="OdfFormulaDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class OdfFormulaBuilder
{
    private readonly OdfFormulaDocument _document;

    internal OdfFormulaBuilder(OdfFormulaDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 以完整 MathML XML 設定公式內容。
    /// </summary>
    /// <param name="mathMlXml">格式正確的 MathML XML</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfFormulaBuilder WithMathML(string mathMlXml)
    {
        _document.SetMathMl(mathMlXml);
        return this;
    }

    /// <summary>
    /// 以一組 MathML token 建立公式 row。
    /// </summary>
    /// <param name="tokens">要寫入的 token 集合</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfFormulaBuilder WithTokens(params OdfMathToken[] tokens)
    {
        _document.SetMathRow(tokens);
        return this;
    }

    /// <summary>
    /// 建立簡單的識別名稱等式（<c>left = right</c>）。
    /// </summary>
    /// <param name="leftIdentifier">等號左側識別名稱</param>
    /// <param name="rightIdentifier">等號右側識別名稱</param>
    /// <returns>目前 builder 執行個體</returns>
    public OdfFormulaBuilder WithIdentifierEquation(string leftIdentifier, string rightIdentifier)
    {
        _document.SetIdentifierEquation(leftIdentifier, rightIdentifier);
        return this;
    }

    /// <summary>
    /// 建立並傳回公式文件。
    /// </summary>
    /// <returns>建立完成的公式文件</returns>
    public OdfFormulaDocument Build()
    {
        return _document;
    }
}
