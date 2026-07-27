using OdfKit.Drawing;
using OdfKit.Core;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Exercises deterministic randomized mutation sequences across primary ODF formats.
/// 對主要 ODF 格式執行可重現的隨機修改序列。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class PrimaryFormatMutationPropertyTests
{
    /// <summary>
    /// Verifies randomized ODT paragraph mutations remain stable across repeated save and load cycles.
    /// 驗證隨機 ODT 段落修改在重複儲存與載入後保持穩定。
    /// </summary>
    [Fact]
    public void OdtRandomizedParagraphMutationsRemainStable()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(0x0D7);
        TextDocument document = TextDocument.Create();
        try
        {
            for (int operation = 0; operation < 64; operation++)
            {
                if (expected.Count == 0 || random.Next(3) != 0)
                {
                    string value = "Paragraph-" + operation;
                    document.Body.Paragraphs.Add(value);
                    expected.Add(value);
                }
                else
                {
                    string value = Pick(expected, random);
                    OdfParagraph paragraph = document.Body.Paragraphs.Find(item => item.TextContent == value)!;
                    Assert.True(document.Body.Paragraphs.Remove(paragraph));
                    expected.Remove(value);
                }

                if ((operation + 1) % 8 == 0)
                    document = Reload(document, stream => TextDocument.Load(stream, "mutation.odt"));
                Assert.Equal(expected.Order(), document.Body.Paragraphs.Select(item => item.TextContent).Order());
            }
        }
        finally
        {
            document.Dispose();
        }
    }

    /// <summary>
    /// Verifies randomized ODS worksheet mutations remain stable across repeated save and load cycles.
    /// 驗證隨機 ODS 工作表修改在重複儲存與載入後保持穩定。
    /// </summary>
    [Fact]
    public void OdsRandomizedWorksheetMutationsRemainStable()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal) { "Seed" };
        var random = new Random(0x0D5);
        SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.Worksheets.Add("Seed");
        try
        {
            for (int operation = 0; operation < 48; operation++)
            {
                if (expected.Count == 1 || random.Next(3) != 0)
                {
                    string name = "Sheet-" + operation;
                    document.Worksheets.Add(name).Cells["A1"].CellValue = operation;
                    expected.Add(name);
                }
                else
                {
                    string name = Pick(expected.Where(item => item != "Seed").ToHashSet(StringComparer.Ordinal), random);
                    Assert.True(document.Worksheets.Remove(document.Worksheets.Find(name)!));
                    expected.Remove(name);
                }

                if ((operation + 1) % 8 == 0)
                    document = Reload(document, stream => SpreadsheetDocument.Load(stream, "mutation.ods"));
                Assert.Equal(expected.Order(), document.Worksheets.Select(item => item.Name).Order());
            }
        }
        finally
        {
            document.Dispose();
        }
    }

    /// <summary>
    /// Verifies randomized ODP slide mutations remain stable across repeated save and load cycles.
    /// 驗證隨機 ODP 投影片修改在重複儲存與載入後保持穩定。
    /// </summary>
    [Fact]
    public void OdpRandomizedSlideMutationsRemainStable()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(0x0D9);
        PresentationDocument document = PresentationDocument.Create();
        try
        {
            for (int operation = 0; operation < 48; operation++)
            {
                if (expected.Count == 0 || random.Next(3) != 0)
                {
                    string name = "Slide-" + operation;
                    document.Slides.Add(name);
                    expected.Add(name);
                }
                else
                {
                    string name = Pick(expected, random);
                    OdfSlide slide = document.Slides.Find(name)!;
                    Assert.True(document.RemoveSlide(slide));
                    expected.Remove(name);
                }

                if ((operation + 1) % 8 == 0)
                    document = Reload(document, stream => PresentationDocument.Load(stream, "mutation.odp"));
                Assert.Equal(expected.Order(), document.Slides.Select(item => item.Name).Order());
            }
        }
        finally
        {
            document.Dispose();
        }
    }

    /// <summary>
    /// Verifies randomized ODG page mutations remain stable across repeated save and load cycles.
    /// 驗證隨機 ODG 頁面修改在重複儲存與載入後保持穩定。
    /// </summary>
    [Fact]
    public void OdgRandomizedPageMutationsRemainStable()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(0x0D6);
        DrawingDocument document = DrawingDocument.Create();
        try
        {
            for (int operation = 0; operation < 48; operation++)
            {
                if (expected.Count == 0 || random.Next(3) != 0)
                {
                    string name = "Page-" + operation;
                    document.Pages.Add(name);
                    expected.Add(name);
                }
                else
                {
                    string name = Pick(expected, random);
                    Assert.True(document.Pages.Remove(document.Pages.Find(name)!));
                    expected.Remove(name);
                }

                if ((operation + 1) % 8 == 0)
                    document = Reload(document, stream => DrawingDocument.Load(stream, "mutation.odg"));
                Assert.Equal(expected.Order(), document.Pages.Select(item => item.Name).Order());
            }
        }
        finally
        {
            document.Dispose();
        }
    }

    private static string Pick(HashSet<string> values, Random random) =>
        values.ElementAt(random.Next(values.Count));

    private static T Reload<T>(T document, Func<Stream, T> loader)
        where T : OdfDocument
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        document.Dispose();
        stream.Position = 0;
        return loader(stream);
    }
}
