using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 <see cref="OdtStreamReader"/> 的讀取結果（節點類型與文字），同時驗證同步（<c>Read</c>）與非同步
/// （<c>ReadAsync</c>）兩條路徑。每個案例的預期值依 ODF 語意推導，而不是複製實作的輸出。
/// </summary>
/// <remarks>
/// 文字中的空白以底線表示、換行與定位字元以跳脫字元表示，讓預期值看得出差異。涵蓋的修正：
/// 兩個行內元素之間只有單一空白時，空白不可被丟掉；清單項目與表格儲存格內的多個段落以換行分隔；
/// 巢狀清單與巢狀表格的內層項目各自成為獨立節點，不再併入外層節點；段落內的註解
/// （<c>office:annotation</c>）與註腳（<c>text:note</c>）內文不屬於段落文字。
/// </remarks>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class OdtStreamReaderBehaviorTests
{
    private const string Head =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\">" +
        "<office:body><office:text>";

    private const string Tail = "</office:text></office:body></office:document-content>";

    private static readonly (string Name, string Xml, string Expected)[] s_cases =
    [
        ("two-spans-one-space", "<text:p><text:span>Hello</text:span> <text:span>world</text:span></text:p>", "Paragraph(Hello_world)"),
        ("span-space-inside-text", "<text:p><text:span>Hello</text:span> there <text:span>world</text:span></text:p>", "Paragraph(Hello_there_world)"),
        ("space-only-paragraph", "<text:p> </text:p>", "Paragraph(_)"),
        ("list-item-two-paragraphs", "<text:list><text:list-item><text:p>A</text:p><text:p>B</text:p></text:list-item></text:list>", @"ListItem(A\nB)"),
        ("table-cell-two-paragraphs", "<table:table><table:table-row><table:table-cell><text:p>A</text:p><text:p>B</text:p></table:table-cell></table:table-row></table:table>", @"TableCell(A\nB)"),
        ("list-item-pretty-printed", "<text:list>\n <text:list-item>\n  <text:p>A</text:p>\n  <text:p>B</text:p>\n </text:list-item>\n</text:list>", @"ListItem(A\nB)"),
        ("list-item-with-heading", "<text:list><text:list-item><text:h text:outline-level=\"1\">T</text:h><text:p>body</text:p></text:list-item></text:list>", @"ListItem(T\nbody)"),
        ("empty-paragraph-inside-list-item", "<text:list><text:list-item><text:p>A</text:p><text:p/><text:p>C</text:p></text:list-item></text:list>", @"ListItem(A\n\nC)"),
        ("text-s-tab-linebreak", "<text:p>A<text:s text:c=\"3\"/>B<text:tab/>C<text:line-break/>D</text:p>", @"Paragraph(A___B\tC\nD)"),
        ("text-s-default-count", "<text:p>A<text:s/>B</text:p>", "Paragraph(A_B)"),
        ("hyperlink-and-nested-span", "<text:p>go <text:a><text:span>link</text:span></text:a>!</text:p>", "Paragraph(go_link!)"),
        ("heading-level", "<text:h text:outline-level=\"2\">Title</text:h>", "Heading(Title)"),
        ("annotation-in-paragraph", "<text:p>A<office:annotation><text:p>note</text:p></office:annotation>B</text:p>", "Paragraph(AB)"),
        ("annotation-end-in-paragraph", "<text:p>A<office:annotation-end/>B</text:p>", "Paragraph(AB)"),
        ("footnote-in-paragraph", "<text:p>A<text:note><text:note-body><text:p>fn</text:p></text:note-body></text:note>B</text:p>", "Paragraph(AB)"),
        ("empty-paragraph", "<text:p/>", "Paragraph()"),
        ("paragraphs-sequence", "<text:p>A</text:p><text:p>B</text:p><text:p>C</text:p>", "Paragraph(A) Paragraph(B) Paragraph(C)"),
        ("whitespace-between-paragraphs", "<text:p>A</text:p>\n  <text:p>B</text:p>", "Paragraph(A) Paragraph(B)"),
        ("leading-trailing-spaces-in-text", "<text:p> A </text:p>", "Paragraph(_A_)"),
        ("paragraph-in-frame", "<text:p>before</text:p><draw:frame><draw:text-box><text:p>inside</text:p></draw:text-box></draw:frame>", "Paragraph(before) Paragraph(inside)"),
        ("frame-inside-paragraph", "<text:p>x<draw:frame><draw:text-box><text:p>y</text:p></draw:text-box></draw:frame>z</text:p>", "Paragraph(xyz)"),
        ("nested-list", "<text:list><text:list-item><text:p>outer</text:p><text:list><text:list-item><text:p>inner</text:p></text:list-item></text:list></text:list-item></text:list>", "ListItem(outer) ListItem(inner)"),
        ("content-after-nested-list", "<text:list><text:list-item><text:p>a</text:p><text:list><text:list-item><text:p>b</text:p></text:list-item></text:list><text:p>c</text:p></text:list-item></text:list>", "ListItem(a) ListItem(b) Paragraph(c)"),
        ("nested-table-in-cell", "<table:table><table:table-row><table:table-cell><text:p>outer</text:p><table:table><table:table-row><table:table-cell><text:p>inner</text:p></table:table-cell></table:table-row></table:table></table:table-cell></table:table-row></table:table>", "TableCell(outer) TableCell(inner)"),
        ("list-inside-cell", "<table:table><table:table-row><table:table-cell><text:p>intro</text:p><text:list><text:list-item><text:p>one</text:p></text:list-item></text:list></table:table-cell></table:table-row></table:table>", "TableCell(intro) ListItem(one)"),
        ("two-cells-in-row", "<table:table><table:table-row><table:table-cell><text:p>a</text:p></table:table-cell><table:table-cell><text:p>b</text:p></table:table-cell></table:table-row></table:table>", "TableCell(a) TableCell(b)"),
        ("s-over-limit", "<text:p>A<text:s text:c=\"2000000000\"/>B</text:p>", "EXCEPTION InvalidDataException"),
    ];

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach ((string name, _, _) in s_cases)
        {
            data.Add(name);
        }

        return data;
    }

    private static MemoryStream CreateOdt(string bodyXml)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("content.xml");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(Head + bodyXml + Tail);
        }

        stream.Position = 0;
        return stream;
    }

    private static string Escape(string text) => text.Replace("\n", "\\n").Replace("\t", "\\t").Replace(" ", "_");

    private static string ReadSync(string bodyXml)
    {
        try
        {
            using MemoryStream stream = CreateOdt(bodyXml);
            using var reader = new OdtStreamReader(stream);
            var builder = new StringBuilder();
            while (reader.Read())
            {
                builder.Append(reader.NodeType).Append('(').Append(Escape(reader.Text)).Append(") ");
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception exception)
        {
            return "EXCEPTION " + exception.GetType().Name;
        }
    }

    private static async Task<string> ReadAsyncPathAsync(string bodyXml)
    {
        try
        {
            using MemoryStream stream = CreateOdt(bodyXml);
            using var reader = new OdtStreamReader(stream);
            var builder = new StringBuilder();
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                builder.Append(reader.NodeType).Append('(').Append(Escape(reader.Text)).Append(") ");
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception exception)
        {
            return "EXCEPTION " + exception.GetType().Name;
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void SyncPathProducesTheExpectedNodes(string name)
    {
        (_, string xml, string expected) = Array.Find(s_cases, candidate => candidate.Name == name);

        Assert.Equal(expected, ReadSync(xml));
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task AsyncPathProducesTheExpectedNodes(string name)
    {
        (_, string xml, string expected) = Array.Find(s_cases, candidate => candidate.Name == name);

        Assert.Equal(expected, await ReadAsyncPathAsync(xml));
    }

    [Fact]
    public async Task WriterOutputRoundTripsThroughBothReadPaths()
    {
        const int paragraphCount = 2_000;
        var output = new MemoryStream();
        using (var writer = new OdtStreamWriter(output))
        {
            writer.AddHeading("Title", 1);
            for (int index = 0; index < paragraphCount; index++)
            {
                writer.AddParagraph("Paragraph " + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 備註");
            }

            writer.BeginList();
            writer.AddListItem("one");
            writer.AddListItem("two");
            writer.EndList();
        }

        // 寫入器釋放時會一併關閉輸出串流；MemoryStream 關閉後仍可取得位元組。
        byte[] bytes = output.ToArray();

        var syncTexts = new System.Collections.Generic.List<string>();
        using (var reader = new OdtStreamReader(new MemoryStream(bytes)))
        {
            while (reader.Read())
            {
                syncTexts.Add(reader.Text);
            }
        }

        var asyncTexts = new System.Collections.Generic.List<string>();
        using (var reader = new OdtStreamReader(new MemoryStream(bytes)))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                asyncTexts.Add(reader.Text);
            }
        }

        Assert.Equal(paragraphCount + 3, syncTexts.Count);
        Assert.Equal(syncTexts, asyncTexts);
        Assert.Equal("Title", syncTexts[0]);
        Assert.Equal("Paragraph 0 備註", syncTexts[1]);
        Assert.Equal("Paragraph 1999 備註", syncTexts[2000]);
        Assert.Equal("one", syncTexts[2001]);
        Assert.Equal("two", syncTexts[2002]);
    }
}
