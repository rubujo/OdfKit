using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Formula;

/// <summary>
/// 代表高階 ODF 公式文件（Formula Document）的類別。
/// </summary>
public class FormulaDocument : OdfFormulaDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體。</param>
    public FormulaDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體。</param>
    /// <param name="subPath">封裝內的子路徑。</param>
    public FormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// 根據指定 MathML XML 建立新的高階公式文件。
    /// </summary>
    /// <param name="mathml">格式正確的 MathML XML。</param>
    /// <returns>建立完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="mathml"/> 為 <see langword="null"/> 時擲出。</exception>
    public static FormulaDocument Create(string mathml)
    {
        if (mathml is null)
        {
            throw new ArgumentNullException(nameof(mathml));
        }

        var doc = (FormulaDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Formula);
        doc.SetMathML(mathml);
        return doc;
    }

    /// <summary>
    /// 從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <param name="path">ODF 公式文件路徑。</param>
    /// <returns>載入完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Load(string path)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入高階公式文件。
    /// </summary>
    /// <param name="stream">包含 ODF 公式文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    private static FormulaDocument EnsureFormula(OdfDocument document)
    {
        if (document is FormulaDocument formula)
        {
            return formula;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是高階 ODF 公式。");
    }

    /// <summary>
    /// 取得 MathML 的 XML 字串。
    /// </summary>
    /// <returns>MathML XML 字串。</returns>
    public string GetMathML()
    {
        return MathMlXml;
    }

    /// <summary>
    /// 設定 MathML 的 XML 字串。
    /// </summary>
    /// <param name="mathml">格式正確的 MathML XML。</param>
    public void SetMathML(string mathml)
    {
        MathMlXml = mathml;
    }
}
