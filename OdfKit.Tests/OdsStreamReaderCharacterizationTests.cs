using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 <see cref="OdsStreamReader"/> 的「讀取結果」，讓內部實作（例如減少配置的最佳化）可以被驗證為行為不變。每個案例同時記錄同步（<c>Read</c>）與非同步（<c>ReadAsync</c>）兩條路徑的結果，
/// 逐格比對 <see cref="OdsCellValue.Kind"/>、<see cref="OdsCellValue.Value"/>、<see cref="OdsCellValue.DisplayText"/>、
/// <see cref="OdsCellValue.RawValueType"/>、<see cref="OdsCellValue.Formula"/> 與 <see cref="OdsCellValue.Currency"/>。
/// </summary>
/// <remarks>
/// 這些案例原本記錄三個已知缺陷的「現況」，現已修正並更新為正確的預期值：
/// 多段落儲存格會遺失每隔一個的段落；段落內含 <c>text:span</c>、<c>text:s</c> 或 <c>text:tab</c> 會擲出
/// <see cref="System.Xml.XmlException"/>；第一列為空列時同步路徑讀不到任何列（非同步路徑讀得到）。
/// 仍維持原行為的已知特性：巢狀表格與儲存格層級註解（<c>office:annotation</c> 作為儲存格的直接子節點）的
/// 段落文字會混入儲存格文字；連續的空白列只回傳一個空列。
/// </remarks>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class OdsStreamReaderCharacterizationTests
{
    private const string Head =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\">" +
        "<office:body><office:spreadsheet><table:table table:name=\"S\">";

    private const string Tail = "</table:table></office:spreadsheet></office:body></office:document-content>";

    // （案例名稱, 列的 XML, 同步路徑預期, 非同步路徑預期）
    private static readonly (string Name, string Xml, string Sync, string Async)[] s_cases =
    [
        (@"two-paragraphs", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A</text:p><text:p>B</text:p></table:table-cell></table:table-row>",
            @"[String:A\nB text=A\nB raw=string f=null cur=null]",
            @"[String:A\nB text=A\nB raw=string f=null cur=null]"),
        (@"three-paragraphs", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A</text:p><text:p>B</text:p><text:p>C</text:p></table:table-cell></table:table-row>",
            @"[String:A\nB\nC text=A\nB\nC raw=string f=null cur=null]",
            @"[String:A\nB\nC text=A\nB\nC raw=string f=null cur=null]"),
        (@"span-in-p", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:span>B</text:span>C</text:p></table:table-cell></table:table-row>",
            @"[String:ABC text=ABC raw=string f=null cur=null]",
            @"[String:ABC text=ABC raw=string f=null cur=null]"),
        (@"space-tab-in-p", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:s/>B<text:tab/>C</text:p></table:table-cell></table:table-row>",
            @"[String:A B\tC text=A B\tC raw=string f=null cur=null]",
            @"[String:A B\tC text=A B\tC raw=string f=null cur=null]"),
        (@"float-with-text", @"<table:table-row><table:table-cell office:value-type=""float"" office:value=""1.5""><text:p>1.50</text:p></table:table-cell></table:table-row>",
            @"[Number:1.5 text=1.50 raw=float f=null cur=null]",
            @"[Number:1.5 text=1.50 raw=float f=null cur=null]"),
        (@"empty-p", @"<table:table-row><table:table-cell office:value-type=""string""><text:p/></table:table-cell></table:table-row>",
            @"[String: text= raw=string f=null cur=null]",
            @"[String: text= raw=string f=null cur=null]"),
        (@"string-value-attr", @"<table:table-row><table:table-cell office:value-type=""string"" office:string-value=""SV""><text:p>shown</text:p></table:table-cell></table:table-row>",
            @"[String:SV text=shown raw=string f=null cur=null]",
            @"[String:SV text=shown raw=string f=null cur=null]"),
        (@"repeated-cols", @"<table:table-row><table:table-cell office:value-type=""float"" office:value=""7"" table:number-columns-repeated=""3""><text:p>7</text:p></table:table-cell><table:table-cell/></table:table-row>",
            @"[Number:7 text=7 raw=float f=null cur=null | Number:7 text=7 raw=float f=null cur=null | Number:7 text=7 raw=float f=null cur=null]",
            @"[Number:7 text=7 raw=float f=null cur=null | Number:7 text=7 raw=float f=null cur=null | Number:7 text=7 raw=float f=null cur=null]"),
        (@"covered", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>X</text:p></table:table-cell><table:covered-table-cell/><table:table-cell office:value-type=""string""><text:p>Y</text:p></table:table-cell></table:table-row>",
            @"[String:X text=X raw=string f=null cur=null | Empty:null text=null raw=null f=null cur=null | String:Y text=Y raw=string f=null cur=null]",
            @"[String:X text=X raw=string f=null cur=null | Empty:null text=null raw=null f=null cur=null | String:Y text=Y raw=string f=null cur=null]"),
        (@"repeated-rows", @"<table:table-row table:number-rows-repeated=""3""><table:table-cell office:value-type=""float"" office:value=""1""><text:p>1</text:p></table:table-cell></table:table-row>",
            @"[Number:1 text=1 raw=float f=null cur=null] [Number:1 text=1 raw=float f=null cur=null] [Number:1 text=1 raw=float f=null cur=null]",
            @"[Number:1 text=1 raw=float f=null cur=null] [Number:1 text=1 raw=float f=null cur=null] [Number:1 text=1 raw=float f=null cur=null]"),
        (@"trailing-empty-rows", @"<table:table-row><table:table-cell office:value-type=""float"" office:value=""1""><text:p>1</text:p></table:table-cell></table:table-row><table:table-row table:number-rows-repeated=""1048570""><table:table-cell/></table:table-row>",
            @"[Number:1 text=1 raw=float f=null cur=null] []",
            @"[Number:1 text=1 raw=float f=null cur=null] []"),
        (@"empty-row-element-first", @"<table:table-row/><table:table-row><table:table-cell office:value-type=""float"" office:value=""2""><text:p>2</text:p></table:table-cell></table:table-row>",
            @"[] [Number:2 text=2 raw=float f=null cur=null]",
            @"[] [Number:2 text=2 raw=float f=null cur=null]"),
        (@"empty-row-element-middle", @"<table:table-row><table:table-cell office:value-type=""float"" office:value=""1""><text:p>1</text:p></table:table-cell></table:table-row><table:table-row/><table:table-row><table:table-cell office:value-type=""float"" office:value=""3""><text:p>3</text:p></table:table-cell></table:table-row>",
            @"[Number:1 text=1 raw=float f=null cur=null] [] [Number:3 text=3 raw=float f=null cur=null]",
            @"[Number:1 text=1 raw=float f=null cur=null] [] [Number:3 text=3 raw=float f=null cur=null]"),
        (@"whitespace-between", @"<table:table-row>
 <table:table-cell office:value-type=""string"">
  <text:p>A</text:p>
  <text:p>B</text:p>
 </table:table-cell>
</table:table-row>",
            @"[String:A\nB text=A\nB raw=string f=null cur=null]",
            @"[String:A\nB text=A\nB raw=string f=null cur=null]"),
        (@"header-rows-group", @"<table:table-header-rows><table:table-row><table:table-cell office:value-type=""string""><text:p>H</text:p></table:table-cell></table:table-row></table:table-header-rows><table:table-row><table:table-cell office:value-type=""string""><text:p>D</text:p></table:table-cell></table:table-row>",
            @"[String:H text=H raw=string f=null cur=null] [String:D text=D raw=string f=null cur=null]",
            @"[String:H text=H raw=string f=null cur=null] [String:D text=D raw=string f=null cur=null]"),
        (@"bool-date-time", @"<table:table-row><table:table-cell office:value-type=""boolean"" office:boolean-value=""true""><text:p>TRUE</text:p></table:table-cell><table:table-cell office:value-type=""date"" office:date-value=""2024-05-06T07:08:09""><text:p>d</text:p></table:table-cell><table:table-cell office:value-type=""time"" office:time-value=""PT01H02M03S""><text:p>t</text:p></table:table-cell></table:table-row>",
            @"[Boolean:True text=TRUE raw=boolean f=null cur=null | Date:2024-05-06T07:08:09 text=d raw=date f=null cur=null | Time:01:02:03 text=t raw=time f=null cur=null]",
            @"[Boolean:True text=TRUE raw=boolean f=null cur=null | Date:2024-05-06T07:08:09 text=d raw=date f=null cur=null | Time:01:02:03 text=t raw=time f=null cur=null]"),
        (@"percentage-currency", @"<table:table-row><table:table-cell office:value-type=""percentage"" office:value=""0.25""><text:p>25%</text:p></table:table-cell><table:table-cell office:value-type=""currency"" office:currency=""TWD"" office:value=""99.5""><text:p>NT$99.50</text:p></table:table-cell></table:table-row>",
            @"[Percentage:0.25 text=25% raw=percentage f=null cur=null | Currency:99.5 text=NT$99.50 raw=currency f=null cur=TWD]",
            @"[Percentage:0.25 text=25% raw=percentage f=null cur=null | Currency:99.5 text=NT$99.50 raw=currency f=null cur=TWD]"),
        (@"formula", @"<table:table-row><table:table-cell table:formula=""of:=1+1"" office:value-type=""float"" office:value=""2""><text:p>2</text:p></table:table-cell></table:table-row>",
            @"[Number:2 text=2 raw=float f=of:=1+1 cur=null]",
            @"[Number:2 text=2 raw=float f=of:=1+1 cur=null]"),
        (@"unknown-type-with-text", @"<table:table-row><table:table-cell office:value-type=""weird""><text:p>W</text:p></table:table-cell><table:table-cell><text:p>no-type</text:p></table:table-cell></table:table-row>",
            @"[Unknown:W text=W raw=weird f=null cur=null | Unknown:no-type text=no-type raw=null f=null cur=null]",
            @"[Unknown:W text=W raw=weird f=null cur=null | Unknown:no-type text=no-type raw=null f=null cur=null]"),
        (@"bad-number-bad-bool", @"<table:table-row><table:table-cell office:value-type=""float"" office:value=""abc""><text:p>x</text:p></table:table-cell><table:table-cell office:value-type=""boolean"" office:boolean-value=""maybe""><text:p>y</text:p></table:table-cell></table:table-row>",
            @"[Number:null text=x raw=float f=null cur=null | Boolean:null text=y raw=boolean f=null cur=null]",
            @"[Number:null text=x raw=float f=null cur=null | Boolean:null text=y raw=boolean f=null cur=null]"),
        (@"nested-table-in-cell", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>P</text:p><table:table table:name=""inner""><table:table-row><table:table-cell office:value-type=""string""><text:p>INNER</text:p></table:table-cell></table:table-row></table:table></table:table-cell><table:table-cell office:value-type=""string""><text:p>after</text:p></table:table-cell></table:table-row>",
            @"[String:P\nINNER text=P\nINNER raw=string f=null cur=null | String:after text=after raw=string f=null cur=null]",
            @"[String:P\nINNER text=P\nINNER raw=string f=null cur=null | String:after text=after raw=string f=null cur=null]"),
        (@"only-empty-cells-first-row", @"<table:table-row><table:table-cell/><table:table-cell/></table:table-row><table:table-row><table:table-cell office:value-type=""float"" office:value=""5""><text:p>5</text:p></table:table-cell></table:table-row>",
            @"[] [Number:5 text=5 raw=float f=null cur=null]",
            @"[] [Number:5 text=5 raw=float f=null cur=null]"),
        (@"leading-empty-cell-then-value", @"<table:table-row><table:table-cell/><table:table-cell office:value-type=""float"" office:value=""9""><text:p>9</text:p></table:table-cell></table:table-row>",
            @"[Empty:null text=null raw=null f=null cur=null | Number:9 text=9 raw=float f=null cur=null]",
            @"[Empty:null text=null raw=null f=null cur=null | Number:9 text=9 raw=float f=null cur=null]"),
        (@"unicode-and-entities", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>備註 &amp; &lt;x&gt; é</text:p></table:table-cell></table:table-row>",
            @"[String:備註 & <x> é text=備註 & <x> é raw=string f=null cur=null]",
            @"[String:備註 & <x> é text=備註 & <x> é raw=string f=null cur=null]"),
        (@"cell-with-comment-annotation", @"<table:table-row><table:table-cell office:value-type=""string""><office:annotation><text:p>note</text:p></office:annotation><text:p>body</text:p></table:table-cell></table:table-row>",
            @"[String:note\nbody text=note\nbody raw=string f=null cur=null]",
            @"[String:note\nbody text=note\nbody raw=string f=null cur=null]"),
        (@"cell-with-draw-frame", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>t</text:p></table:table-cell><table:table-cell office:value-type=""string""><text:p>u</text:p></table:table-cell></table:table-row>",
            @"[String:t text=t raw=string f=null cur=null | String:u text=u raw=string f=null cur=null]",
            @"[String:t text=t raw=string f=null cur=null | String:u text=u raw=string f=null cur=null]"),
        (@"text-s-with-count", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:s text:c=""3""/>B</text:p></table:table-cell></table:table-row>",
            @"[String:A   B text=A   B raw=string f=null cur=null]",
            @"[String:A   B text=A   B raw=string f=null cur=null]"),
        (@"text-s-default-count", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:s/>B</text:p></table:table-cell></table:table-row>",
            @"[String:A B text=A B raw=string f=null cur=null]",
            @"[String:A B text=A B raw=string f=null cur=null]"),
        (@"line-break", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:line-break/>B</text:p></table:table-cell></table:table-row>",
            @"[String:A\nB text=A\nB raw=string f=null cur=null]",
            @"[String:A\nB text=A\nB raw=string f=null cur=null]"),
        (@"nested-span-and-link", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>go <text:a><text:span>link</text:span></text:a>!</text:p></table:table-cell></table:table-row>",
            @"[String:go link! text=go link! raw=string f=null cur=null]",
            @"[String:go link! text=go link! raw=string f=null cur=null]"),
        (@"span-then-paragraph", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:span>1</text:span></text:p><text:p>B</text:p></table:table-cell></table:table-row>",
            @"[String:A1\nB text=A1\nB raw=string f=null cur=null]",
            @"[String:A1\nB text=A1\nB raw=string f=null cur=null]"),
        (@"whitespace-between-spans", @"<table:table-row><table:table-cell office:value-type=""string""><text:p><text:span>a</text:span> <text:span>b</text:span></text:p></table:table-cell></table:table-row>",
            @"[String:a b text=a b raw=string f=null cur=null]",
            @"[String:a b text=a b raw=string f=null cur=null]"),
        (@"empty-paragraph-between", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A</text:p><text:p/><text:p>C</text:p></table:table-cell></table:table-row>",
            @"[String:A\n\nC text=A\n\nC raw=string f=null cur=null]",
            @"[String:A\n\nC text=A\n\nC raw=string f=null cur=null]"),
        (@"annotation-inside-paragraph-is-skipped", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<office:annotation><text:p>note</text:p></office:annotation>B</text:p></table:table-cell></table:table-row>",
            @"[String:AB text=AB raw=string f=null cur=null]",
            @"[String:AB text=AB raw=string f=null cur=null]"),
        (@"footnote-inside-paragraph-is-skipped", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:note><text:note-body><text:p>fn</text:p></text:note-body></text:note>B</text:p></table:table-cell></table:table-row>",
            @"[String:AB text=AB raw=string f=null cur=null]",
            @"[String:AB text=AB raw=string f=null cur=null]"),
        (@"text-s-over-limit", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:s text:c=""2000000000""/>B</text:p></table:table-cell></table:table-row>",
            @"EXCEPTION InvalidDataException",
            @"EXCEPTION InvalidDataException"),
        (@"text-s-overflowing-count", @"<table:table-row><table:table-cell office:value-type=""string""><text:p>A<text:s text:c=""99999999999999""/>B</text:p></table:table-cell></table:table-row>",
            @"EXCEPTION InvalidDataException",
            @"EXCEPTION InvalidDataException"),
        (@"leading-repeated-empty-rows", @"<table:table-row table:number-rows-repeated=""3""><table:table-cell/></table:table-row><table:table-row><table:table-cell office:value-type=""float"" office:value=""2""><text:p>2</text:p></table:table-cell></table:table-row>",
            @"[] [Number:2 text=2 raw=float f=null cur=null]",
            @"[] [Number:2 text=2 raw=float f=null cur=null]"),
    ];

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach ((string name, _, _, _) in s_cases)
        {
            data.Add(name);
        }

        return data;
    }

    private static MemoryStream CreateOds(string rowsXml)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("content.xml");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(Head + rowsXml + Tail);
        }

        stream.Position = 0;
        return stream;
    }

    private static string Escape(string text) => text.Replace("\n", "\\n").Replace("\t", "\\t");

    private static string Describe(OdsStreamReader reader)
    {
        var builder = new StringBuilder("[");
        for (int column = 0; column < reader.FieldCount; column++)
        {
            OdsCellValue cell = reader.GetCell(column);
            if (column > 0)
            {
                builder.Append(" | ");
            }

            string value = cell.Value switch
            {
                null => "null",
                DateTime date => date.ToString("s", CultureInfo.InvariantCulture),
                _ => Escape(Convert.ToString(cell.Value, CultureInfo.InvariantCulture)!),
            };
            builder.Append(cell.Kind).Append(':').Append(value);
            builder.Append(" text=").Append(cell.DisplayText is null ? "null" : Escape(cell.DisplayText));
            builder.Append(" raw=").Append(cell.RawValueType ?? "null");
            builder.Append(" f=").Append(cell.Formula ?? "null");
            builder.Append(" cur=").Append(cell.Currency ?? "null");
        }

        return builder.Append(']').ToString();
    }

    private static string ReadSync(string rowsXml)
    {
        try
        {
            using MemoryStream stream = CreateOds(rowsXml);
            using var reader = new OdsStreamReader(stream);
            var builder = new StringBuilder();
            while (reader.Read())
            {
                builder.Append(Describe(reader)).Append(' ');
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception exception)
        {
            return "EXCEPTION " + exception.GetType().Name;
        }
    }

    private static async Task<string> ReadAsyncPathAsync(string rowsXml)
    {
        try
        {
            using MemoryStream stream = CreateOds(rowsXml);
            using var reader = new OdsStreamReader(stream);
            var builder = new StringBuilder();
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                builder.Append(Describe(reader)).Append(' ');
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
    public void SyncPath_ProducesTheRecordedResult(string name)
    {
        (_, string xml, string expected, _) = Array.Find(s_cases, candidate => candidate.Name == name);

        Assert.Equal(expected, ReadSync(xml));
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task AsyncPath_ProducesTheRecordedResult(string name)
    {
        (_, string xml, _, string expected) = Array.Find(s_cases, candidate => candidate.Name == name);

        Assert.Equal(expected, await ReadAsyncPathAsync(xml));
    }

    [Fact]
    public async Task WriterOutput_RoundTripsThroughBothReadPaths()
    {
        const int rowCount = 3_000;
        var output = new MemoryStream();
        using (var writer = new OdsStreamWriter(output))
        {
            writer.WriteStartSheet("Data");
            for (int row = 0; row < rowCount; row++)
            {
                writer.WriteStartRow();
                writer.WriteCell((double)row);
                writer.WriteCell("Item-" + row.ToString(CultureInfo.InvariantCulture));
                writer.WriteCell(row * 0.25);
                writer.WriteCell(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(row));
                writer.WriteCell(row % 3 == 0);
                writer.WriteCell("備註 " + row.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndRow();
            }

            writer.WriteEndSheet();
        }

        // 寫入器釋放時會一併關閉輸出串流；MemoryStream 關閉後仍可取得位元組。
        byte[] bytes = output.ToArray();

        var syncRows = new List<string>();
        using (var reader = new OdsStreamReader(new MemoryStream(bytes)))
        {
            while (reader.Read())
            {
                syncRows.Add(Describe(reader));
            }
        }

        var asyncRows = new List<string>();
        using (var reader = new OdsStreamReader(new MemoryStream(bytes)))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                asyncRows.Add(Describe(reader));
            }
        }

        Assert.Equal(rowCount, syncRows.Count);
        Assert.Equal(syncRows, asyncRows);
        Assert.Contains("Number:2999", syncRows[2999], StringComparison.Ordinal);
        Assert.Contains("String:Item-7 ", syncRows[7] + " ", StringComparison.Ordinal);
        Assert.Contains("String:備註 2999", syncRows[2999], StringComparison.Ordinal);
    }
}
