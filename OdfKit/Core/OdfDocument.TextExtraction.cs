using System;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// Provides high-level document text-extraction operations.
/// 提供高階文件文字擷取作業。
/// </summary>
public abstract partial class OdfDocument
{
    /// <summary>
    /// Extracts plain text from the document using default options.
    /// 使用預設選項從文件擷取純文字。
    /// </summary>
    /// <returns>The extracted plain text. / 擷取出的純文字。</returns>
    public string ExtractText() => ExtractText(OdfTextExtractionOptions.Default);

    /// <summary>
    /// Extracts plain text from the document.
    /// 從文件擷取純文字。
    /// </summary>
    /// <param name="options">The extraction options. / 文字擷取選項。</param>
    /// <returns>The extracted plain text. / 擷取出的純文字。</returns>
    public string ExtractText(OdfTextExtractionOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));

        return OdfDocumentTextExtractionEngine.Extract(ContentDom, options);
    }
}
