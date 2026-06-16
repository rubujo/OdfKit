using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using WP = DocumentFormat.OpenXml.Wordprocessing;

namespace OdfKit.Conversion;

/// <summary>
/// 將 DOCX 格式轉換為 <see cref="TextDocument"/> 的轉換器。
/// </summary>
public static class DocxToOdtConverter
{
    /// <summary>
    /// 從 DOCX 資料流讀取並建立對應的 ODT 文字文件。
    /// </summary>
    /// <param name="docxStream">DOCX 來源資料流。</param>
    /// <returns>轉換後的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="docxStream"/> 為 null 時擲出。</exception>
    /// <exception cref="InvalidDataException">當 DOCX 缺少主要文件本文時擲出。</exception>
    public static TextDocument Convert(Stream docxStream)
    {
        if (docxStream is null)
        {
            throw new ArgumentNullException(nameof(docxStream));
        }

        using var wordDocument = WordprocessingDocument.Open(docxStream, false);
        MainDocumentPart mainPart = wordDocument.MainDocumentPart
            ?? throw new InvalidDataException("DOCX 文件缺少主要文件部分。");
        WP.Document document = mainPart.Document
            ?? throw new InvalidDataException("DOCX 文件缺少 word/document.xml。");
        WP.Body body = document.Body
            ?? throw new InvalidDataException("DOCX 文件缺少 word/document.xml 本文。");

        TextDocument odtDocument = TextDocument.Create();
        ConvertHeaderFooter(mainPart, odtDocument);
        foreach (var child in body.ChildElements)
        {
            if (child is WP.Paragraph paragraph)
            {
                ConvertParagraph(mainPart, paragraph, odtDocument);
            }
            else if (child is WP.Table table)
            {
                ConvertTable(table, odtDocument);
            }
        }

        return odtDocument;
    }

    private static void ConvertHeaderFooter(MainDocumentPart mainPart, TextDocument odtDocument)
    {
        string headerText = string.Concat(mainPart.HeaderParts.Select(part => part.Header?.InnerText ?? string.Empty));
        string footerText = string.Concat(mainPart.FooterParts.Select(part => part.Footer?.InnerText ?? string.Empty));
        if (string.IsNullOrEmpty(headerText) && string.IsNullOrEmpty(footerText))
        {
            return;
        }

        OdfPageSetup setup = odtDocument.GetDefaultPageSetup();
        if (!string.IsNullOrEmpty(headerText))
        {
            setup.HeaderText = headerText;
        }

        if (!string.IsNullOrEmpty(footerText))
        {
            setup.FooterText = footerText;
        }
    }

    private static void ConvertParagraph(MainDocumentPart mainPart, WP.Paragraph paragraph, TextDocument odtDocument)
    {
        int headingLevel = GetHeadingLevel(paragraph);
        OdfParagraph odtParagraph = headingLevel > 0
            ? odtDocument.AddHeading(string.Empty, headingLevel)
            : odtDocument.AddParagraph();
        ApplyParagraphProperties(paragraph.ParagraphProperties, odtParagraph);

        foreach (var child in paragraph.ChildElements)
        {
            if (child is WP.Run run)
            {
                ConvertRun(mainPart, run, odtDocument, odtParagraph);
            }
            else if (child is WP.InsertedRun insertedRun)
            {
                AppendInsertedRun(odtDocument, odtParagraph.Node, insertedRun);
            }
            else if (child is WP.DeletedRun deletedRun)
            {
                AppendDeletedRun(odtDocument, odtParagraph.Node, deletedRun);
            }
        }

        if (!odtParagraph.Node.Children.Any())
        {
            AppendText(odtParagraph.Node, paragraph.InnerText ?? string.Empty);
        }
    }

    private static void ApplyParagraphProperties(WP.ParagraphProperties? properties, OdfParagraph odtParagraph)
    {
        if (properties?.Justification?.Val is null)
        {
            return;
        }

        WP.JustificationValues value = properties.Justification.Val.Value;
        if (value == WP.JustificationValues.Center)
        {
            odtParagraph.HorizontalAlignment = "center";
        }
        else if (value == WP.JustificationValues.Right)
        {
            odtParagraph.HorizontalAlignment = "end";
        }
        else if (value == WP.JustificationValues.Both)
        {
            odtParagraph.HorizontalAlignment = "justify";
        }
        else
        {
            odtParagraph.HorizontalAlignment = "start";
        }
    }

    private static void AppendInsertedRun(TextDocument odtDocument, OdfNode paragraphNode, WP.InsertedRun insertedRun)
    {
        string text = string.Concat(insertedRun.Descendants<WP.Text>().Select(node => node.Text));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        string changeId = odtDocument.AddTrackedChange(
            "insertion",
            insertedRun.Author?.Value ?? "Author",
            insertedRun.Date?.Value ?? DateTime.UtcNow);
        AppendChangeStart(paragraphNode, changeId);
        AppendText(paragraphNode, text);
        AppendChangeEnd(paragraphNode, changeId);
    }

    private static void AppendDeletedRun(TextDocument odtDocument, OdfNode paragraphNode, WP.DeletedRun deletedRun)
    {
        string text = string.Concat(deletedRun.Descendants<WP.DeletedText>().Select(node => node.Text));
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var deletedParagraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        AppendText(deletedParagraph, text);

        string changeId = odtDocument.AddTrackedChange(
            "deletion",
            deletedRun.Author?.Value ?? "Author",
            deletedRun.Date?.Value ?? DateTime.UtcNow,
            deletedParagraph);
        AppendChangeStart(paragraphNode, changeId);
        AppendChangeEnd(paragraphNode, changeId);
    }

    private static void ConvertRun(MainDocumentPart mainPart, WP.Run run, TextDocument odtDocument, OdfParagraph odtParagraph)
    {
        AppendRunText(odtDocument, odtParagraph, ExtractRunText(run), run.RunProperties);

        foreach (var drawing in run.Descendants<WP.Drawing>())
        {
            AppendDrawing(mainPart, drawing, odtDocument, odtParagraph);
        }
    }

    private static void AppendDrawing(MainDocumentPart mainPart, WP.Drawing drawing, TextDocument odtDocument, OdfParagraph odtParagraph)
    {
        A.Blip? blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        string? relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrEmpty(relationshipId))
        {
            return;
        }

        if (mainPart.GetPartById(relationshipId!) is not ImagePart imagePart)
        {
            return;
        }

        byte[] imageBytes;
        using (Stream stream = imagePart.GetStream(FileMode.Open, FileAccess.Read))
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            imageBytes = memory.ToArray();
        }

        if (imageBytes.Length == 0)
        {
            return;
        }

        string preferredName = Path.GetFileName(imagePart.Uri.ToString());
        string packagePath = new OdfMediaManager(odtDocument.Package).AddImage(imageBytes, preferredName);
        (OdfLength width, OdfLength height) = GetDrawingSize(drawing);
        odtDocument.AddImage(odtParagraph, packagePath, width, height, preferredName);
    }

    private static (OdfLength Width, OdfLength Height) GetDrawingSize(WP.Drawing drawing)
    {
        DW.Extent? extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
        if (extent?.Cx is not null && extent.Cy is not null)
        {
            return (EmuToCentimeters(extent.Cx.Value), EmuToCentimeters(extent.Cy.Value));
        }

        A.Extents? pictureExtent = drawing.Descendants<A.Extents>().FirstOrDefault();
        if (pictureExtent?.Cx is not null && pictureExtent.Cy is not null)
        {
            return (EmuToCentimeters(pictureExtent.Cx.Value), EmuToCentimeters(pictureExtent.Cy.Value));
        }

        return (OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(2));
    }

    private static OdfLength EmuToCentimeters(long emu)
    {
        return OdfLength.FromCentimeters(emu / 360000d);
    }

    private static string ExtractRunText(WP.Run run)
    {
        return string.Concat(run.Descendants<WP.Text>().Select(node => node.Text));
    }

    private static void AppendRunText(TextDocument odtDocument, OdfParagraph odtParagraph, string text, WP.RunProperties? properties)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (properties is null || !HasRunFormatting(properties))
        {
            AppendText(odtParagraph.Node, text);
            return;
        }

        OdfTextRun run = odtParagraph.AddTextRun(text);
        if (properties.Bold is not null)
        {
            run.IsBold = properties.Bold.Val is null || properties.Bold.Val.Value;
        }

        if (properties.Italic is not null)
        {
            run.IsItalic = properties.Italic.Val is null || properties.Italic.Val.Value;
        }

        if (properties.Underline?.Val is not null && properties.Underline.Val.Value != WP.UnderlineValues.None)
        {
            run.IsUnderline = true;
        }

        if (properties.Color?.Val?.Value is { Length: > 0 } color && !string.Equals(color, "auto", StringComparison.OrdinalIgnoreCase))
        {
            run.Color = "#" + color;
        }

        if (properties.FontSize?.Val?.Value is { Length: > 0 } halfPoints &&
            double.TryParse(halfPoints, out double sizeValue))
        {
            run.FontSize = (sizeValue / 2d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "pt";
        }
    }

    private static bool HasRunFormatting(WP.RunProperties properties)
    {
        return properties.Bold is not null ||
            properties.Italic is not null ||
            properties.Underline is not null ||
            properties.Color is not null ||
            properties.FontSize is not null;
    }

    private static void AppendText(OdfNode parent, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        parent.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
    }

    private static void AppendChangeStart(OdfNode paragraphNode, string changeId)
    {
        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        paragraphNode.AppendChild(startNode);
    }

    private static void AppendChangeEnd(OdfNode paragraphNode, string changeId)
    {
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        paragraphNode.AppendChild(endNode);
    }

    private static void ConvertTable(WP.Table wordTable, TextDocument odtDocument)
    {
        var rows = wordTable.Elements<WP.TableRow>().ToList();
        int rowCount = rows.Count;
        int columnCount = rows.Count == 0
            ? 0
            : rows.Max(row => row.Elements<WP.TableCell>().Count());

        if (rowCount == 0 || columnCount == 0)
        {
            return;
        }

        OdfTable odtTable = odtDocument.AddTable(rowCount, columnCount);
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var cells = rows[rowIndex].Elements<WP.TableCell>().ToList();
            for (int columnIndex = 0; columnIndex < cells.Count; columnIndex++)
            {
                string cellText = string.Join("\n", cells[columnIndex]
                    .Elements<WP.Paragraph>()
                    .Select(paragraph => paragraph.InnerText)
                    .Where(text => !string.IsNullOrEmpty(text)));

                if (!string.IsNullOrEmpty(cellText))
                {
                    odtTable.GetCell(rowIndex, columnIndex).AddParagraph(cellText);
                }
            }
        }
    }

    private static int GetHeadingLevel(WP.Paragraph paragraph)
    {
        string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrWhiteSpace(styleId))
        {
            return 0;
        }

        string normalized = styleId!.Replace(" ", string.Empty);
        if (!normalized.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string suffix = normalized.Substring("Heading".Length);
        if (!int.TryParse(suffix, out int level))
        {
            return 0;
        }

        if (level < 1)
        {
            return 1;
        }

        return level > 6 ? 6 : level;
    }
}
