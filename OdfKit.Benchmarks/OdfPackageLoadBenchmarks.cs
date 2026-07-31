using BenchmarkDotNet.Attributes;
using OdfKit.Core;
using OdfKit.Text;
using System;
using System.IO;

namespace OdfKit.Benchmarks;

/// <summary>
/// ODF 封裝載入效能基準。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PackageSizeKB"/> 涵蓋數 KB 到百餘 KB，因為記憶體映射的相對效益隨封裝大小變化：
/// 小型封裝由固定建立成本主導，大型封裝才顯現避免整檔複製的優勢。只用單一尺寸量測會得出
/// 以偏概全的結論。
/// </para>
/// <para>
/// <b>歸因限制</b>：<see cref="LoadFileBcl"/> 與 <see cref="LoadFileMmf"/> 之間**不只**差在讀取機制。
/// <see cref="OdfPackage.Open(string)"/> 另外會執行交易日誌復原並以 <c>FileAccess.ReadWrite</c> 開檔，
/// 而 <see cref="LoadFileBcl"/> 走的 <see cref="OdfPackage.Open(Stream, bool)"/> 兩者皆無。實測這段差異
/// 可達 45%，足以淹沒讀取機制本身的差距。因此這組數字可用於回答「各入口的實際載入成本」，
/// 但不可用於單獨歸因記憶體映射的效益——後者需要一組只改變讀取機制的對照。
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class OdfPackageLoadBenchmarks
{
    /// <summary>
    /// 目標封裝大小（KB）。
    /// </summary>
    [Params(2, 32, 128)]
    public int PackageSizeKB { get; set; }

    private byte[] _packageBytes = null!;
    private string _tempFilePath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _packageBytes = BuildPackage(PackageSizeKB);
        _tempFilePath = Path.Combine(Path.GetTempPath(),
            $"odfkit_bench_{Guid.NewGuid():N}.odt");
        File.WriteAllBytes(_tempFilePath, _packageBytes);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
    }

    /// <summary>
    /// 記憶體來源：無檔案系統成本的下界基準。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int LoadMemoryBcl()
    {
        using var stream = new MemoryStream(_packageBytes, writable: false);
        using var package = OdfPackage.Open(stream, leaveOpen: true);
        return package.Manifest.Count;
    }

    /// <summary>
    /// 檔案來源但不觸發記憶體映射：以非 <see cref="FileStream"/> 的串流包裝，使 <c>FilePath</c> 維持 null。
    /// </summary>
    /// <remarks>
    /// 與 <see cref="LoadFileMmf"/> 的差異不只讀取機制，見型別層級 <c>remarks</c> 的歸因限制說明。
    /// </remarks>
    [Benchmark]
    public int LoadFileBcl()
    {
        using var fileStream = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Read);
        using var buffered = new BufferedStream(fileStream);
        using var package = OdfPackage.Open(buffered, leaveOpen: true);
        return package.Manifest.Count;
    }

    /// <summary>
    /// 檔案路徑載入：生產環境的主要入口，走記憶體映射。
    /// </summary>
    [Benchmark]
    public int LoadFileMmf()
    {
        using var package = OdfPackage.Open(_tempFilePath);
        return package.Manifest.Count;
    }

    /// <summary>
    /// 檔案路徑載入 + 平行預讀。
    /// </summary>
    [Benchmark]
    public int LoadFileMmfLazy()
    {
        using var package = OdfPackage.Open(
            _tempFilePath,
            new OdfLoadOptions { AllowLazyLoading = true });
        return package.Manifest.Count;
    }

    private static byte[] BuildPackage(int targetKB)
    {
        // 以段落數逼近目標壓縮後大小；段落內容含遞增序號，避免壓縮率過度失真。
        int paragraphs = Math.Max(1, targetKB * 370);

        using var ms = new MemoryStream();
        using (var document = TextDocument.Create())
        {
            for (int i = 0; i < paragraphs; i++)
                document.AddParagraph($"基準測試段落 {i}：這是用來撐出封裝體積的內容。");

            document.SaveToStream(ms);
        }

        return ms.ToArray();
    }
}
