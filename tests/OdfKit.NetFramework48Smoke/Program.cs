using OdfKit.Collaboration;
using OdfKit.Conversion;
using OdfKit.Core;
using OdfKit.Drawing;
using OdfKit.Export;
using OdfKit.Extensions.Imaging;
using OdfKit.Extensions.Rdf;
using OdfKit.Extensions.Rendering;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.NetFramework48Smoke;

internal static class Program
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "OdfKitNet48_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyText(Path.Combine(root, "smoke.odt"));
            VerifySpreadsheet(Path.Combine(root, "smoke.ods"));
            VerifyPresentation(Path.Combine(root, "smoke.odp"));
            VerifyDrawing(Path.Combine(root, "smoke.odg"));
            VerifyExtensions();
            Console.WriteLine("OdfKit net48 smoke passed on CLR " + Environment.Version + ".");
            return 0;
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyText(string path)
    {
        using (TextDocument document = TextDocument.Create())
        {
            document.AddParagraph("OdfKit-net48-ODT");
            document.Save(path);
        }

        using TextDocument loaded = TextDocument.Load(path);
        Require(
            loaded.Body.Paragraphs.Items.Any(paragraph => paragraph.TextContent.Contains("OdfKit-net48-ODT")),
            "ODT round-trip failed.");
    }

    private static void VerifySpreadsheet(string path)
    {
        using (SpreadsheetDocument document = SpreadsheetDocument.Create())
        {
            OdfTableSheet sheet = document.Worksheets.Add("Data");
            sheet.Cells[0, 0].CellValue = "OdfKit-net48-ODS";
            document.Save(path);
        }

        using SpreadsheetDocument loaded = SpreadsheetDocument.Load(path);
        Require(
            Convert.ToString(loaded.Worksheets["Data"].Cells[0, 0].CellValue) == "OdfKit-net48-ODS",
            "ODS round-trip failed.");
    }

    private static void VerifyPresentation(string path)
    {
        using (PresentationDocument document = PresentationDocument.Create())
        {
            OdfSlide slide = document.Slides.Add("Slide1");
            slide.AddTextBox(
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(6),
                OdfLength.FromCentimeters(2),
                "OdfKit-net48-ODP");
            document.Save(path);
        }

        using PresentationDocument loaded = PresentationDocument.Load(path);
        Require(loaded.Slides.Count == 1, "ODP round-trip failed.");
    }

    private static void VerifyDrawing(string path)
    {
        using (DrawingDocument document = DrawingDocument.Create())
        {
            OdfDrawPage page = document.Pages.Add("Page1");
            page.AddTextBox(
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(1),
                OdfLength.FromCentimeters(6),
                OdfLength.FromCentimeters(2),
                "OdfKit-net48-ODG");
            document.Save(path);
        }

        using DrawingDocument loaded = DrawingDocument.Load(path);
        Require(loaded.Pages.Count == 1, "ODG round-trip failed.");
    }

    private static void VerifyExtensions()
    {
        _ = new OdfHtmlExportOptions();
        _ = typeof(OdfToXlsxConverter);
        _ = new OdfPdfRenderer();
        _ = typeof(LocalProcessBackend);
        _ = OdfRdfGraphUris.ResolveSubjectUri("content.xml");
        _ = new OdtOperationCompatibilityOptions();

        OdfLength measured = OdfTextMeasurer.MeasureWidth("OdfKit", "Arial", 12);
        Require(measured.ToCentimeters() > 0, "Imaging native runtime smoke failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
