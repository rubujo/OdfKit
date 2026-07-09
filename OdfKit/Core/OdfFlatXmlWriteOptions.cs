using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Configures flat XML package writing for <see cref="OdfDocumentFactory.WriteFlatXml(System.IO.Stream, OdfDocumentKind, OdfFlatXmlWriteOptions)"/>.
/// 設定 <see cref="OdfDocumentFactory.WriteFlatXml(System.IO.Stream, OdfDocumentKind, OdfFlatXmlWriteOptions)"/> 的 Flat XML 寫入選項。
/// </summary>
public sealed class OdfFlatXmlWriteOptions
{
    /// <summary>
    /// Gets the default flat XML write options (ODF 1.4, leave stream open).
    /// 取得預設 Flat XML 寫入選項（ODF 1.4，保持串流開啟）。
    /// </summary>
    public static OdfFlatXmlWriteOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the ODF specification version written into the document.
    /// 取得或設定寫入文件的 ODF 規格版本。
    /// </summary>
    public OdfVersion Version { get; set; } = OdfVersion.Odf14;

    /// <summary>
    /// Gets or sets whether the destination stream remains open after writing.
    /// 取得或設定寫入後是否保持目的串流開啟。
    /// </summary>
    public bool LeaveOpen { get; set; } = true;
}
