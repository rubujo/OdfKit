using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Formula;

/// <summary>
/// Represents a high-level ODF formula document.
/// 代表高階 ODF 公式文件（Formula Document）的類別。
/// </summary>
public class FormulaDocument : OdfFormulaDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public FormulaDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public FormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new high-level formula document.
    /// 建立新的高階公式文件。
    /// </summary>
    /// <returns>A new <see cref="FormulaDocument"/> instance. / 新的 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Create()
    {
        return (FormulaDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Formula);
    }

    /// <summary>
    /// Creates a new high-level formula document fluent builder.
    /// 建立新的高階公式文件 Fluent builder。
    /// </summary>
    /// <returns>A new <see cref="FormulaDocumentBuilder"/> instance. / 新的 <see cref="FormulaDocumentBuilder"/> 執行個體。</returns>
    public new static FormulaDocumentBuilder Builder()
    {
        return new FormulaDocumentBuilder(Create());
    }

    /// <summary>
    /// Creates a new high-level formula document from the specified MathML XML.
    /// 根據指定 MathML XML 建立新的高階公式文件。
    /// </summary>
    /// <param name="mathml">The well-formed MathML XML. / 格式正確的 MathML XML。</param>
    /// <returns>The created high-level <see cref="FormulaDocument"/> instance. / 建立完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="mathml"/> is <see langword="null"/>. / 當 <paramref name="mathml"/> 為 <see langword="null"/> 時擲出。</exception>
    public static FormulaDocument Create(string mathml)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(mathml, nameof(mathml));

        FormulaDocument doc = Create();
        doc.SetMathML(mathml);
        return doc;
    }

    /// <summary>
    /// Creates and loads a high-level formula document from the specified LaTeX formula string.
    /// 從指定的 LaTeX 公式字串建立並載入高階公式文件。
    /// </summary>
    /// <param name="latex">The LaTeX formula string. / LaTeX 公式字串。</param>
    /// <returns>The <see cref="FormulaDocument"/> instance loaded with the LaTeX formula. / 已載入 LaTeX 公式的 <see cref="FormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="latex"/> is <see langword="null"/>. / 當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the LaTeX formula syntax is invalid. / 當 LaTeX 公式語法錯誤時擲出。</exception>
    public new static FormulaDocument FromLatex(string latex)
    {
        FormulaDocument doc = Create();
        doc.LoadFromLatex(latex);
        return doc;
    }

    /// <summary>
    /// Creates and loads a high-level formula document by using an <see cref="OdfMathBuilder"/> composition delegate.
    /// 使用 <see cref="OdfMathBuilder"/> 組合委派建立並載入高階公式文件。
    /// </summary>
    /// <param name="build">The delegate used to compose the MathML token tree. / 用於組合 MathML token 樹狀結構的委派。</param>
    /// <returns>The <see cref="FormulaDocument"/> instance loaded with the composed result. / 已載入組合結果的 <see cref="FormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="build"/> is <see langword="null"/>. / 當 <paramref name="build"/> 為 <see langword="null"/> 時擲出。</exception>
    public new static FormulaDocument FromBuilder(Action<OdfMathBuilder> build)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(build, nameof(build));

        var mathBuilder = new OdfMathBuilder();
        build(mathBuilder);
        OdfMathToken root = mathBuilder.Build();

        FormulaDocument doc = Create();
        doc.SetMathRow(root);
        return doc;
    }

    /// <summary>
    /// Loads a high-level formula document from the specified file path.
    /// 從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <param name="path">The ODF formula document path. / ODF 公式文件路徑。</param>
    /// <returns>The loaded high-level <see cref="FormulaDocument"/> instance. / 載入完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Load(string path)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads a high-level formula document from the specified file path.
    /// 非同步從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded high-level <see cref="FormulaDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="FormulaDocument"/>。</returns>
    public new static Task<FormulaDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts path and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public new static async Task<FormulaDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a high-level formula document from the specified stream.
    /// 從指定資料流載入高階公式文件。
    /// </summary>
    /// <returns>The loaded high-level <see cref="FormulaDocument"/> instance. / 載入完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Full overload of Load that accepts stream and fileName.
    /// Load 完整多載：接受 stream 與 fileName。
    /// </summary>
    public new static FormulaDocument Load(Stream stream, string? fileName)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level formula document from the specified stream.
    /// 非同步從指定資料流載入高階公式文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded high-level <see cref="FormulaDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="FormulaDocument"/>。</returns>
    public new static Task<FormulaDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public new static Task<FormulaDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public new static Task<FormulaDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream, fileName, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、fileName 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public new static async Task<FormulaDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new high-level formula document from the specified formula template document.
    /// 從指定的公式範本文件建立新的高階公式文件。
    /// </summary>
    /// <param name="template">The formula template document. / 公式範本文件。</param>
    /// <returns>The created <see cref="FormulaDocument"/> instance. / 建立完成的 <see cref="FormulaDocument"/> 執行個體。</returns>
    public static FormulaDocument CreateFromTemplate(FormulaTemplateDocument template) =>
        (FormulaDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Formula, "application/vnd.oasis.opendocument.formula");

    /// <summary>
    /// Creates an equivalent ODF (ZIP package) formula document from an FDF flat XML formula document, preserving the same content.
    /// 從 FDF 扁平 XML 公式文件建立等價的 ODF（ZIP 封裝）公式文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FDF flat XML formula document. / 來源 FDF 扁平 XML 公式文件。</param>
    /// <returns>The created <see cref="FormulaDocument"/> instance. / 建立完成的 <see cref="FormulaDocument"/> 執行個體。</returns>
    public static FormulaDocument CreateFromFlatDocument(FlatFormulaDocument document) =>
        (FormulaDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Formula, targetIsFlatXml: false);

    private static FormulaDocument EnsureFormula(OdfDocument document)
    {
        if (document is FormulaDocument formula && document.DocumentKind == OdfDocumentKind.Formula)
        {
            return formula;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaDocument_SpecifiedOdfFileHigher"));
    }

    /// <summary>
    /// Sets the MathML XML string.
    /// 設定 MathML 的 XML 字串。
    /// </summary>
    /// <param name="mathml">The well-formed MathML XML. / 格式正確的 MathML XML。</param>
    public void SetMathML(string mathml)
    {
        MathMlXml = mathml;
    }
}
