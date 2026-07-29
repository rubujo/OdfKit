using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using OdfKit.Spreadsheet;

namespace OdfKit.Benchmarks;

/// <summary>
/// Measures coordinate patching of a logically large repeated-row ODS document.
/// 量測具大量邏輯重複列之 ODS 文件的座標修補。
/// </summary>
[MemoryDiagnoser]
public class OdsSparseEditorBenchmarks
{
    private byte[] _source = [];
    private OdsCellPatch[] _patches = [];
    private OdsCellPatch[] _formulaPatches = [];

    /// <summary>
    /// Creates a compact package representing one million logical rows.
    /// 建立以精簡 XML 表示一百萬邏輯列的封裝。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(
                archive,
                "mimetype",
                "application/vnd.oasis.opendocument.spreadsheet",
                CompressionLevel.NoCompression);
            WriteEntry(
                archive,
                "content.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <office:document-content
                  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                  xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
                  xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                  <office:body><office:spreadsheet><table:table table:name="Data">
                    <table:table-row table:number-rows-repeated="1000000">
                      <table:table-cell table:number-columns-repeated="20" office:value-type="string"><text:p>old</text:p></table:table-cell>
                    </table:table-row>
                  </table:table></office:spreadsheet></office:body>
                </office:document-content>
                """,
                CompressionLevel.Optimal);
        }
        _source = stream.ToArray();
        _patches = Enumerable.Range(0, 100)
            .Select(index => new OdsCellPatch
            {
                SheetName = "Data",
                Row = index * 10_000,
                Column = index % 20,
                Text = "updated",
            })
            .ToArray();
        _formulaPatches = Enumerable.Range(0, 100)
            .Select(index => new OdsCellPatch
            {
                SheetName = "Data",
                Row = index * 10_000,
                Column = index % 20,
                Formula = "of:=1+2*3",
            })
            .ToArray();
    }

    /// <summary>
    /// Applies one hundred sparse patches.
    /// 套用一百筆稀疏修補。
    /// </summary>
    /// <returns>A task representing the benchmark operation. / 代表基準作業的工作。</returns>
    [Benchmark]
    public async Task PatchOneHundredCells()
    {
        using var source = new MemoryStream(_source, writable: false);
        using var destination = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            source,
            destination,
            _patches,
            new OdsSparseEditorOptions(),
            default).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies one hundred prevalidated formula patches.
    /// 套用一百筆預先驗證的公式修補。
    /// </summary>
    /// <returns>A task representing the benchmark operation. / 代表基準作業的工作。</returns>
    [Benchmark]
    public async Task PatchOneHundredFormulaCells()
    {
        using var source = new MemoryStream(_source, writable: false);
        using var destination = new MemoryStream();
        await OdsSparseEditor.ApplyAsync(
            source,
            destination,
            _formulaPatches,
            new OdsSparseEditorOptions(),
            default).ConfigureAwait(false);
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        string value,
        CompressionLevel compression)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, compression);
        using Stream output = entry.Open();
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        output.Write(bytes, 0, bytes.Length);
    }
}
