using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 稽核後續修正的迴歸測試：非陣列記憶體寫入、載入失敗的資源釋放與彙總函式溢位。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class AuditFollowUpRegressionTests
{
#if NET10_0_OR_GREATER
    /// <summary>
    /// 驗證 buffer writer 串流可直接接受非陣列支援的記憶體，並遵守預取消語意。
    /// </summary>
    [Fact]
    public async Task BufferWriterStreamWritesNonArrayMemoryWithoutAsyncBridge()
    {
        using var source = new AlignedNativeBuffer(4, 4096);
        new byte[] { 1, 2, 3, 4 }.AsSpan().CopyTo(source.GetSpan());
        var destination = new ArrayBufferWriter<byte>();
        using var stream = new OdfBufferWriterStream(destination);

        await stream.WriteAsync(source.Memory, TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, destination.WrittenSpan.ToArray());

        using var cancelled = new System.Threading.CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await stream.WriteAsync(source.Memory, cancelled.Token));
    }
#endif

    /// <summary>
    /// 驗證以無效內容開啟時，同步與非同步的 Stream 多載都會釋放整個 package，
    /// 而非僅釋放傳入的串流。
    /// </summary>
    /// <remarks>
    /// package 在載入失敗時可能已持有 SemaphoreSlim、CancellationTokenSource、已註冊 entry 與
    /// 記憶體映射。先前這兩個多載只 Dispose 串流，與 path 多載的 package.Dispose() 不一致。
    /// 此處以 leaveOpen 觀察：既然改為釋放 package，串流是否關閉必須仍由 leaveOpen 決定。
    /// </remarks>
    [Fact]
    public async Task OpenFromStreamFailureReleasesPackageAndHonoursLeaveOpen()
    {
        static MemoryStream CreateInvalidPackage()
        {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = archive.CreateEntry("content.xml", CompressionLevel.NoCompression);
                using Stream stream = entry.Open();
                stream.Write("<not-an-odf-package/>"u8);
            }

            ms.Position = 0;
            return ms;
        }

        // leaveOpen: true —— 失敗後串流必須維持開啟。
        using (MemoryStream keepOpen = CreateInvalidPackage())
        {
            Assert.ThrowsAny<Exception>(() => OdfPackage.Open(keepOpen, leaveOpen: true));

            Assert.True(keepOpen.CanRead, "leaveOpen: true 時串流不應被關閉");
        }

        // leaveOpen: false —— 失敗後串流必須被關閉。
        MemoryStream closeIt = CreateInvalidPackage();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await OdfPackage.OpenAsync(
                closeIt,
                leaveOpen: false,
                options: null,
                TestContext.Current.CancellationToken));

        Assert.False(closeIt.CanRead, "leaveOpen: false 時串流應已關閉");
    }

    /// <summary>
    /// 驗證標記重算時，損壞的選用 settings.xml 不會讓可寫出的封裝儲存失敗。
    /// </summary>
    [Fact]
    public void SaveContinuesWhenOptionalSettingsXmlCannotBeParsed()
    {
        using var source = new MemoryStream();
        using var package = OdfPackage.Create(source, leaveOpen: true, options: null);
        package.WriteEntry(
            "content.xml",
            Encoding.UTF8.GetBytes($$"""
                <office:document-content xmlns:office="{{OdfNamespaces.Office}}">
                  <office:body />
                </office:document-content>
                """),
            "text/xml");
        package.WriteEntry("settings.xml", "<broken"u8.ToArray(), "text/xml");
        using var destination = new MemoryStream();

        package.Save(
            destination,
            new OdfSaveOptions
            {
                FormulaStrategy = OdfFormulaSaveStrategy.MarkForRecalculation
            });

        Assert.True(destination.Length > 0);
    }

    /// <summary>
    /// 驗證彙總函式的溢位與數學運算子一致，回報 <c>#NUM!</c> 而非 <see cref="double.PositiveInfinity"/>。
    /// </summary>
    [Fact]
    public void AggregateOverflowReportsNumErrorLikeArithmeticOperators()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ConstantOnlyEvaluationContext();

        // 對照組：數學運算子早已在溢位時回報 #NUM!。
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("1e308+1e308", context));

        // 修正目標：彙總函式先前直接回傳 Infinity，與上一行對相同輸入結果不一致。
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("SUM(1e308;1e308)", context));
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("AVERAGE(1e308;1e308;-1e308)", context));
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("SUMIF(A1:A2;\">0\";B1:B2)", context));
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("SUMIFS(B1:B2;A1:A2;\">0\")", context));
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("VAR(1e308;-1e308)", context));
    }

    /// <summary>
    /// 只求值常數運算式的最小內容；本測試不參照任何儲存格。
    /// </summary>
    private sealed class ConstantOnlyEvaluationContext : IEvaluationContext
    {
        public OdfCellAddress CurrentCell => new(0, 0);

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[,]
        {
            { 1e308 },
            { 1e308 }
        };

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) => OdfFormulaError.Name;
    }
}
