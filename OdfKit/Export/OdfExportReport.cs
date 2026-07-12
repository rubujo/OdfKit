using System.Collections.Generic;

namespace OdfKit.Export;

/// <summary>
/// Identifies a managed export format.
/// 識別受管理的匯出格式。
/// </summary>
public enum OdfExportFormat
{
    /// <summary>
    /// Exports HTML.
    /// 匯出 HTML。
    /// </summary>
    Html,
    /// <summary>
    /// Exports Markdown.
    /// 匯出 Markdown。
    /// </summary>
    Markdown,
    /// <summary>
    /// Exports SVG.
    /// 匯出 SVG。
    /// </summary>
    Svg,
    /// <summary>
    /// Exports PDF.
    /// 匯出 PDF。
    /// </summary>
    Pdf
}

/// <summary>
/// Reports a managed document export operation.
/// 回報受管理的文件匯出作業。
/// </summary>
/// <param name="format">The exported format. / 匯出格式。</param>
/// <param name="backend">The backend identifier. / 後端識別值。</param>
public sealed class OdfExportReport(OdfExportFormat format, string backend)
{
    /// <summary>
    /// Gets the exported format.
    /// 取得匯出格式。
    /// </summary>
    public OdfExportFormat Format { get; } = format;

    /// <summary>
    /// Gets the backend identifier.
    /// 取得後端識別值。
    /// </summary>
    public string Backend { get; } = backend;

    /// <summary>
    /// Gets or sets the number of bytes written.
    /// 取得或設定寫入的位元組數。
    /// </summary>
    public long BytesWritten { get; set; }

    /// <summary>
    /// Gets structured non-fatal diagnostic codes.
    /// 取得結構化非致命診斷代碼。
    /// </summary>
    public IList<string> DiagnosticCodes { get; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether diagnostics were produced.
    /// 取得是否產生診斷。
    /// </summary>
    public bool HasDiagnostics => DiagnosticCodes.Count > 0;
}
