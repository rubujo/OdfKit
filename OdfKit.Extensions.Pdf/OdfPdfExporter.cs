using System;
using System.IO;
using System.Runtime.InteropServices;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using PdfSharp.Fonts;

#if NETSTANDARD2_0
using ArgumentNullException = OdfKit.Export.Shim.ArgumentNullException;
#endif

using OdfKit.Compliance;
namespace OdfKit.Export;

/// <summary>
/// Exports ODF documents to PDF.
/// 將 TextDocument 匯出為 PDF 的工具類別。
/// </summary>
public static class OdfPdfExporter
{
    static OdfPdfExporter()
    {
        try
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new OdfPdfFontResolver();
            }
        }
        catch
        {
            // 若已註冊則忽略。
        }
    }

    /// <summary>
    /// Exports the specified ODF document to PDF.
    /// 將 ODT 文字文件轉換並寫入 PDF 資料流。
    /// </summary>
    /// <param name="document">The source or target object. / 來源文字文件</param>
    /// <param name="pdfStream">The source or target object. / 要寫入 PDF 的目標資料流</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一必要參數為 null 時拋出</exception>
    public static void Export(TextDocument document, Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pdfStream);

        var migraDoc = BuildMigraDoc(document);
        var renderer = new PdfDocumentRenderer { Document = migraDoc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(pdfStream);
    }

    private static Document BuildMigraDoc(TextDocument odfDoc)
    {
        var doc = new Document();

        // 依作業系統決定預設的中文字型名稱，以防止中文在導出 PDF 時因為 Arial 字型不支援而顯示為方塊字
        // 在 Windows 平台上使用標楷體（DFKai-SB），因為它是標準 .ttf 檔，能避開 PDFsharp 無法直接解析 .ttc（如微軟正黑體）的 NullReferenceException 限制
        string defaultChineseFont = "DFKai-SB";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            defaultChineseFont = "PingFang TC";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            defaultChineseFont = "Noto Sans CJK TC";
        }

        // 套用中文字型至預設樣式
        if (doc.Styles["Normal"] is Style normalStyle)
        {
            normalStyle.Font.Name = defaultChineseFont;
        }
        if (doc.Styles["Heading1"] is Style h1Style)
        {
            h1Style.Font.Name = defaultChineseFont;
        }
        if (doc.Styles["Heading2"] is Style h2Style)
        {
            h2Style.Font.Name = defaultChineseFont;
        }
        if (doc.Styles["Heading3"] is Style h3Style)
        {
            h3Style.Font.Name = defaultChineseFont;
        }

        var section = doc.AddSection();

        foreach (var node in odfDoc.BodyTextRoot.Children)
        {
            if (node.NamespaceUri != OdfNamespaces.Text)
                continue;

            switch (node.LocalName)
            {
                case "h":
                    {
                        int level = int.TryParse(node.GetAttribute("outline-level", OdfNamespaces.Text), out int l) ? l : 1;
                        string styleName = level == 1 ? StyleNames.Heading1
                            : level == 2 ? StyleNames.Heading2
                            : StyleNames.Heading3;
                        var para = section.AddParagraph(string.Empty, styleName);
                        ConvertParagraphContent(node, para, defaultChineseFont);
                        break;
                    }
                case "p":
                    {
                        var para = section.AddParagraph();
                        ConvertParagraphContent(node, para, defaultChineseFont);
                        break;
                    }
                case "list":
                    {
                        foreach (var item in node.Children)
                        {
                            if (item.LocalName == "list-item")
                            {
                                var para = section.AddParagraph();
                                para.Format.LeftIndent = "1cm";
                                para.Format.FirstLineIndent = "-0.5cm";
                                ConvertParagraphContent(item, para, defaultChineseFont);
                            }
                        }
                        break;
                    }
            }
        }

        return doc;
    }

    private static void ConvertParagraphContent(OdfNode odfPara, Paragraph migraPara, string defaultFont)
    {
        bool hasChildren = false;
        foreach (var child in odfPara.Children)
        {
            hasChildren = true;
            ProcessChildNode(child, migraPara, defaultFont);
        }
        if (!hasChildren)
        {
            AddSegmentedText(migraPara, odfPara.TextContent ?? string.Empty, defaultFont);
        }
    }

    private static void ProcessChildNode(OdfNode child, Paragraph parent, string defaultFont)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            AddSegmentedText(parent, child.TextContent ?? string.Empty, defaultFont);
            return;
        }

        if (child.NodeType == OdfNodeType.Element)
        {
            if (child.NamespaceUri != OdfNamespaces.Text)
                return;

            switch (child.LocalName)
            {
                case "span":
                    var fmt = parent.AddFormattedText();
                    foreach (var grandChild in child.Children)
                    {
                        ProcessChildNode(grandChild, fmt, defaultFont);
                    }
                    break;

                case "s":
                    int count = 1;
                    string? cStr = child.GetAttribute("c", OdfNamespaces.Text);
                    if (!string.IsNullOrEmpty(cStr) && int.TryParse(cStr, out int c) && c > 0)
                    {
                        count = Math.Min(c, 4096); // 防禦性上限：PDF 中不需超過 4096 個連續空白
                    }
                    parent.AddText(new string(' ', count));
                    break;

                case "tab":
                    parent.AddTab();
                    break;

                case "line-break":
                    parent.AddLineBreak();
                    break;
            }
        }
    }

    private static void ProcessChildNode(OdfNode child, FormattedText parent, string defaultFont)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            AddSegmentedText(parent, child.TextContent ?? string.Empty, defaultFont);
            return;
        }

        if (child.NodeType == OdfNodeType.Element)
        {
            if (child.NamespaceUri != OdfNamespaces.Text)
                return;

            switch (child.LocalName)
            {
                case "span":
                    var fmt = parent.AddFormattedText();
                    foreach (var grandChild in child.Children)
                    {
                        ProcessChildNode(grandChild, fmt, defaultFont);
                    }
                    break;

                case "s":
                    int count = 1;
                    string? cStr = child.GetAttribute("c", OdfNamespaces.Text);
                    if (!string.IsNullOrEmpty(cStr) && int.TryParse(cStr, out int c) && c > 0)
                    {
                        count = Math.Min(c, 4096); // 防禦性上限：PDF 中不需超過 4096 個連續空白
                    }
                    parent.AddText(new string(' ', count));
                    break;

                case "tab":
                    parent.AddTab();
                    break;

                case "line-break":
                    parent.AddLineBreak();
                    break;
            }
        }
    }

    private static void AddSegmentedText(Paragraph paragraph, string text, string defaultFont)
    {
        var segments = OdfFontSegmenter.SegmentText(text, defaultFont);
        foreach (var (segText, fontName) in segments)
        {
            if (fontName == defaultFont)
            {
                paragraph.AddText(segText);
            }
            else
            {
                OdfFontResolver.WarnIfUnresolvable(fontName, "CNS 11643 高位字面文字 PDF 匯出");
                var run = paragraph.AddFormattedText();
                run.Font.Name = fontName;
                run.AddText(segText);
            }
        }
    }

    private static void AddSegmentedText(FormattedText formattedText, string text, string defaultFont)
    {
        var segments = OdfFontSegmenter.SegmentText(text, defaultFont);
        foreach (var (segText, fontName) in segments)
        {
            if (fontName == defaultFont)
            {
                formattedText.AddText(segText);
            }
            else
            {
                OdfFontResolver.WarnIfUnresolvable(fontName, "CNS 11643 高位字面文字 PDF 匯出");
                var run = formattedText.AddFormattedText();
                run.Font.Name = fontName;
                run.AddText(segText);
            }
        }
    }
}

internal sealed class OdfPdfFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string? resolved = Styles.OdfFontResolver.ResolveFontFallback(familyName, IsUsablePdfFont);
        if (resolved is not null)
        {
            return new FontResolverInfo(resolved, isBold, isItalic);
        }

        if (familyName.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new FontResolverInfo("Courier New", isBold, isItalic);
        }
        return new FontResolverInfo("Arial", isBold, isItalic);
    }

    public byte[] GetFont(string faceName)
    {
        foreach (string candidate in Styles.OdfFontResolver.GetFontFallbackCandidates(faceName))
        {
            string? fontPath = Styles.OdfFontResolver.ResolveFontPath(candidate);
            if (fontPath is not null && File.Exists(fontPath) && !Styles.OdfFontResolver.IsTrueTypeCollection(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }

            if (fontPath is not null && Styles.OdfFontResolver.IsTrueTypeCollection(fontPath))
            {
                OdfKitDiagnostics.Warn(
                    OdfLocalizer.GetMessage("Diag_OdfPdfExporter_TrueTypeCollectionFontFallback", candidate));
            }
        }

        string[] fallbacks = { "Arial", "Courier New", "Liberation Sans", "DejaVu Sans" };
        foreach (var fb in fallbacks)
        {
            string? fontPath = Styles.OdfFontResolver.ResolveFontPath(fb);
            if (fontPath is not null && File.Exists(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }
        }

        string[] standardDirs = {
            @"C:\Windows\Fonts",
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            "/Library/Fonts"
        };
        foreach (var dir in standardDirs)
        {
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return File.ReadAllBytes(files[0]);
                }
            }
        }

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPdfExporter_UnableResolveFont", faceName));
    }

    private static bool IsUsablePdfFont(string familyName)
    {
        string? fontPath = Styles.OdfFontResolver.ResolveFontPath(familyName);
        return fontPath is not null && !Styles.OdfFontResolver.IsTrueTypeCollection(fontPath);
    }
}
