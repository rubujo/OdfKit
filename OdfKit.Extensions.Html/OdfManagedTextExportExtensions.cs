using System;
using System.IO;
using System.Text;
using OdfKit.Drawing;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// Adds high-level import and export extension methods for ODF text documents.
/// 提供 TextDocument managed 匯出的高階擴充方法。
/// </summary>
public static class OdfManagedTextExportExtensions
{
    /// <summary>
    /// Converts the text document to HTML.
    /// 將文字文件匯出為 HTML 字串。
    /// </summary>
    public static string ToHtml(this TextDocument document, OdfHtmlExportOptions? options = null) =>
        OdfHtmlExporter.Export(document, options);

    /// <summary>
    /// Converts the text document to Markdown.
    /// 將文字文件匯出為 Markdown 字串。
    /// </summary>
    public static string ToMarkdown(this TextDocument document, OdfMarkdownExportOptions? options = null) =>
        OdfMarkdownExporter.Export(document, options);

    /// <summary>
    /// Converts the text document to RTF.
    /// 將文字文件匯出為 RTF 字串。
    /// </summary>
    public static string ToRtf(this TextDocument document, OdfRtfExportOptions? options = null) =>
        OdfRtfExporter.Export(document, options);

    /// <summary>
    /// Imports Markdown text into an ODT text document.
    /// 將 Markdown 字串匯入為文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocument(this string markdown, OdfMarkdownImportOptions? options = null) =>
        OdfMarkdownImporter.Import(markdown, options);

    /// <summary>
    /// Imports Markdown content into an ODT text document.
    /// 從 Markdown reader 匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocument(this TextReader reader, OdfMarkdownImportOptions? options = null) =>
        OdfMarkdownImporter.Import(reader, options);

    /// <summary>
    /// Imports Markdown content into an ODT text document.
    /// 從 Markdown 檔案匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocument(this FileInfo file, OdfMarkdownImportOptions? options = null)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        return OdfMarkdownImporter.Load(file.FullName, options);
    }

    /// <summary>
    /// Loads a Markdown file as an ODT text document.
    /// 從 Markdown 檔案匯入文字文件。
    /// </summary>
    public static TextDocument LoadMarkdownAsOdt(string path, OdfMarkdownImportOptions? options = null)
        => OdfMarkdownImporter.Load(path, options);

    /// <summary>
    /// Imports RTF text into an ODT text document.
    /// 將 RTF 字串匯入為文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocumentFromRtf(this string rtf) =>
        OdfRtfImporter.Import(rtf);

    /// <summary>
    /// Imports RTF content into an ODT text document.
    /// 從 RTF reader 匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocumentFromRtf(this TextReader reader) =>
        OdfRtfImporter.Import(reader);

    /// <summary>
    /// Imports RTF content into an ODT text document.
    /// 從 RTF 檔案匯入文字文件。
    /// </summary>
    public static TextDocument ToOdtTextDocumentFromRtf(this FileInfo file)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        return OdfRtfImporter.Load(file.FullName);
    }

    /// <summary>
    /// Loads an RTF file as an ODT text document.
    /// 從 RTF 檔案匯入文字文件。
    /// </summary>
    public static TextDocument LoadRtfAsOdt(string path)
        => OdfRtfImporter.Load(path);

    /// <summary>
    /// Converts the drawing page to SVG.
    /// 將繪圖文件匯出為 SVG 字串。
    /// </summary>
    public static string ToSvg(this DrawingDocument document, OdfSvgExportOptions? options = null) =>
        OdfSvgExporter.Export(document, options);

    /// <summary>
    /// Saves the text document as an HTML file.
    /// 將文字文件匯出為 HTML 檔案。
    /// </summary>
    public static void SaveAsHtml(this TextDocument document, string path, OdfHtmlExportOptions? options = null)
    {
        WriteText(path, document.ToHtml(options));
    }

    /// <summary>
    /// Saves the text document as a Markdown file.
    /// 將文字文件匯出為 Markdown 檔案。
    /// </summary>
    public static void SaveAsMarkdown(this TextDocument document, string path, OdfMarkdownExportOptions? options = null)
    {
        WriteText(path, document.ToMarkdown(options));
    }

    /// <summary>
    /// Saves the text document as an RTF file.
    /// 將文字文件匯出為 RTF 檔案。
    /// </summary>
    public static void SaveAsRtf(this TextDocument document, string path, OdfRtfExportOptions? options = null)
    {
        WriteText(path, document.ToRtf(options));
    }

    /// <summary>
    /// Saves the drawing document as an SVG file.
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
