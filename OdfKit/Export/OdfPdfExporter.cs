using System;
using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using PdfSharp.Fonts;

#if NETSTANDARD2_0
using ArgumentNullException = OdfKit.Export.Shim.ArgumentNullException;
#endif

namespace OdfKit.Export;

/// <summary>
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
    /// 將 ODT 文字文件轉換並寫入 PDF 資料流。
    /// </summary>
    /// <param name="document">來源文字文件。</param>
    /// <param name="pdfStream">要寫入 PDF 的目標資料流。</param>
    /// <exception cref="ArgumentNullException">當任一必要參數為 null 時拋出。</exception>
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
        var section = doc.AddSection();

        foreach (var node in odfDoc.BodyTextRoot.Children)
        {
            if (node.NamespaceUri != OdfNamespaces.Text) continue;

            switch (node.LocalName)
            {
                case "h":
                {
                    int level = int.TryParse(node.GetAttribute("outline-level", OdfNamespaces.Text), out int l) ? l : 1;
                    string styleName = level == 1 ? StyleNames.Heading1
                        : level == 2 ? StyleNames.Heading2
                        : StyleNames.Heading3;
                    var para = section.AddParagraph(node.TextContent ?? string.Empty, styleName);
                    break;
                }
                case "p":
                {
                    var para = section.AddParagraph();
                    ConvertParagraphContent(node, para);
                    break;
                }
                case "list":
                {
                    foreach (var item in node.Children)
                    {
                        if (item.LocalName == "list-item")
                        {
                            var para = section.AddParagraph(item.TextContent ?? string.Empty);
                            para.Format.LeftIndent = "1cm";
                            para.Format.FirstLineIndent = "-0.5cm";
                        }
                    }
                    break;
                }
            }
        }

        return doc;
    }

    private static void ConvertParagraphContent(OdfNode odfPara, Paragraph migraPara)
    {
        bool hasChildren = false;
        foreach (var child in odfPara.Children)
        {
            hasChildren = true;
            ProcessChildNode(child, migraPara);
        }
        if (!hasChildren)
        {
            migraPara.AddText(odfPara.TextContent ?? string.Empty);
        }
    }

    private static void ProcessChildNode(OdfNode child, Paragraph parent)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            parent.AddText(child.TextContent ?? string.Empty);
            return;
        }

        if (child.NodeType == OdfNodeType.Element)
        {
            if (child.NamespaceUri != OdfNamespaces.Text) return;

            switch (child.LocalName)
            {
                case "span":
                    var fmt = parent.AddFormattedText();
                    foreach (var grandChild in child.Children)
                    {
                        ProcessChildNode(grandChild, fmt);
                    }
                    break;

                case "s":
                    int count = 1;
                    string? cStr = child.GetAttribute("c", OdfNamespaces.Text);
                    if (!string.IsNullOrEmpty(cStr) && int.TryParse(cStr, out int c))
                    {
                        count = c;
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

    private static void ProcessChildNode(OdfNode child, FormattedText parent)
    {
        if (child.NodeType == OdfNodeType.Text)
        {
            parent.AddText(child.TextContent ?? string.Empty);
            return;
        }

        if (child.NodeType == OdfNodeType.Element)
        {
            if (child.NamespaceUri != OdfNamespaces.Text) return;

            switch (child.LocalName)
            {
                case "span":
                    var fmt = parent.AddFormattedText();
                    foreach (var grandChild in child.Children)
                    {
                        ProcessChildNode(grandChild, fmt);
                    }
                    break;

                case "s":
                    int count = 1;
                    string? cStr = child.GetAttribute("c", OdfNamespaces.Text);
                    if (!string.IsNullOrEmpty(cStr) && int.TryParse(cStr, out int c))
                    {
                        count = c;
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
}

internal sealed class OdfPdfFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string? fontPath = Styles.OdfFontResolver.ResolveFontPath(familyName);
        if (fontPath is not null)
        {
            return new FontResolverInfo(familyName, isBold, isItalic);
        }

        if (familyName.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new FontResolverInfo("Courier New", isBold, isItalic);
        }
        return new FontResolverInfo("Arial", isBold, isItalic);
    }

    public byte[] GetFont(string faceName)
    {
        string? fontPath = Styles.OdfFontResolver.ResolveFontPath(faceName);
        if (fontPath is not null && File.Exists(fontPath))
        {
            return File.ReadAllBytes(fontPath);
        }

        string[] fallbacks = { "Arial", "Courier New", "Liberation Sans", "DejaVu Sans" };
        foreach (var fb in fallbacks)
        {
            fontPath = Styles.OdfFontResolver.ResolveFontPath(fb);
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

        throw new InvalidOperationException($"Unable to resolve font '{faceName}'.");
    }
}
