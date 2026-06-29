namespace OdfKit.Formula;

/// <summary>
/// Provides a fluent builder API for <see cref="OdfFormulaDocument"/>.
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
    /// Sets formula content from complete MathML XML.
    /// 以完整 MathML XML 設定公式內容。
    /// </summary>
    /// <param name="mathMlXml">The well-formed MathML XML. / 格式正確的 MathML XML。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfFormulaBuilder WithMathML(string mathMlXml)
    {
        _document.SetMathMl(mathMlXml);
        return this;
    }

    /// <summary>
    /// Creates the formula row from a set of MathML tokens.
    /// 以一組 MathML token 建立公式 row。
    /// </summary>
    /// <param name="tokens">The token collection to write. / 要寫入的 token 集合。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfFormulaBuilder WithTokens(params OdfMathToken[] tokens)
    {
        _document.SetMathRow(tokens);
        return this;
    }

    /// <summary>
    /// Creates a simple identifier equation (<c>left = right</c>).
    /// 建立簡單的識別名稱等式（<c>left = right</c>）。
    /// </summary>
    /// <param name="leftIdentifier">The identifier on the left side of the equals sign. / 等號左側識別名稱。</param>
    /// <param name="rightIdentifier">The identifier on the right side of the equals sign. / 等號右側識別名稱。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfFormulaBuilder WithIdentifierEquation(string leftIdentifier, string rightIdentifier)
    {
        _document.SetIdentifierEquation(leftIdentifier, rightIdentifier);
        return this;
    }

    /// <summary>
    /// Builds and returns the formula document.
    /// 建立並傳回公式文件。
    /// </summary>
    /// <returns>The built formula document. / 建立完成的公式文件。</returns>
    public OdfFormulaDocument Build()
    {
        return _document;
    }
}
