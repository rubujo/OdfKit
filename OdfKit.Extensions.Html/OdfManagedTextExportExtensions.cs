using System;
using System.IO;
using System.Text;
using OdfKit.Drawing;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 提供 TextDocument managed 匯出的高階擴充方法。
/// </summary>
public static class OdfManagedTextExportExtensions
{
    /// <summary>
    /// 將文字文件匯出為 HTML 字串。
    /// </summary>
    public static string ToHtml(this TextDocument document, OdfHtmlExportOptions? options = null) =>
        OdfHtmlExporter.Export(document, options);

    /// <summary>
    /// 將文字文件匯出為 Markdown 字串。
    /// </summary>
    public static string ToMarkdown(this TextDocument document, OdfMarkdownExportOptions? options = null) =>
        OdfMarkdownExporter.Export(document, options);

    /// <summary>
    /// 將文字文件匯出為 RTF 字串。
    /// </summary>
    public static string ToRtf(this TextDocument document, OdfRtfExportOptions? options = null) =>
        OdfRtfExporter.Export(document, options);

    /// <summary>
    /// 將 Markdown 字串匯入為文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocument(this string markdown, OdfMarkdownImportOptions? options = null) =>
        OdfMarkdownImporter.Import(markdown, options);

    /// <summary>
    /// 從 Markdown reader 匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocument(this TextReader reader, OdfMarkdownImportOptions? options = null) =>
        OdfMarkdownImporter.Import(reader, options);

    /// <summary>
    /// 從 Markdown 檔案匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocument(this FileInfo file, OdfMarkdownImportOptions? options = null)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        return OdfMarkdownImporter.Load(file.FullName, options);
    }

    /// <summary>
    /// 從 Markdown 檔案匯入文字文件。
    /// </summary>
    public static TextDocument LoadMarkdownAsOdt(string path, OdfMarkdownImportOptions? options = null)
        => OdfMarkdownImporter.Load(path, options);

    /// <summary>
    /// 將 RTF 字串匯入為文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocumentFromRtf(this string rtf) =>
        OdfRtfImporter.Import(rtf);

    /// <summary>
    /// 從 RTF reader 匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocumentFromRtf(this TextReader reader) =>
        OdfRtfImporter.Import(reader);

    /// <summary>
    /// 從 RTF 檔案匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocumentFromRtf(this FileInfo file)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        return OdfRtfImporter.Load(file.FullName);
    }

    /// <summary>
    /// 從 RTF 檔案匯入文字文件。
    /// </summary>
    public static TextDocument LoadRtfAsOdt(string path)
        => OdfRtfImporter.Load(path);

    /// <summary>
    /// 將繪圖文件匯出為 SVG 字串。
    /// </summary>
    public static string ToSvg(this DrawingDocument document, OdfSvgExportOptions? options = null) =>
        OdfSvgExporter.Export(document, options);

    /// <summary>
    /// 將文字文件匯出為 HTML 檔案。
    /// </summary>
    public static void SaveAsHtml(this TextDocument document, string path, OdfHtmlExportOptions? options = null)
    {
        WriteText(path, document.ToHtml(options));
    }

    /// <summary>
    /// 將文字文件匯出為 Markdown 檔案。
    /// </summary>
    public static void SaveAsMarkdown(this TextDocument document, string path, OdfMarkdownExportOptions? options = null)
    {
        WriteText(path, document.ToMarkdown(options));
    }

    /// <summary>
    /// 將文字文件匯出為 RTF 檔案。
    /// </summary>
    public static void SaveAsRtf(this TextDocument document, string path, OdfRtfExportOptions? options = null)
    {
        WriteText(path, document.ToRtf(options));
    }

    /// <summary>
    /// 將繪圖文件匯出為 SVG 檔案。
    /// </summary>
    public static void SaveAsSvg(this DrawingDocument document, string path, OdfSvgExportOptions? options = null)
    {
        OdfSvgExporter.Save(document, path, options);
    }

    private static void WriteText(string path, string content)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, Encoding.UTF8);
    }
}
