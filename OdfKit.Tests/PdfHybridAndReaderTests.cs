using System.Security.Cryptography;
using System.Text;
using OdfKit.Core;
using OdfKit.Export.PdfInternal;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 PDF 讀取器與混合 PDF 輔助：傳統交叉參照表、交叉參照串流與物件串流、
/// 標準安全處理常式的各種加密（RC4 40／128、AES-128、AES-256），以及增量更新不改動原檔內容。
/// 夾具由獨立的 PDF 函式庫（qpdf）產生，與本專案的寫入程式碼互相獨立。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class PdfHybridAndReaderTests
{
    private static readonly byte[] OdfPayload = Encoding.UTF8.GetBytes("PK\u0003\u0004 pretend this is a zipped ODF package — with unicode 標題 and \0 bytes\0");

    public static TheoryData<string> PlainFixtures => new()
    {
        "classic-xmp.pdf",
        "objstm-xref-stream-xmp.pdf",
    };

    public static TheoryData<string> EncryptedFixtures => new()
    {
        "encrypted-r2-rc4-40.pdf",
        "encrypted-r3-rc4-128.pdf",
        "encrypted-r4-aes-128.pdf",
        "encrypted-r6-aes-256.pdf",
    };

    private static byte[] Inject(byte[] pdf, string? password = null)
    {
        using var source = new MemoryStream(pdf);
        using var odf = new MemoryStream(OdfPayload);
        using var output = new MemoryStream();
        OdfHybridPdfHelper.InjectOdfToPdf(source, odf, output, "report.odt", password);
        return output.ToArray();
    }

    private static byte[]? Extract(byte[] pdf, string? password = null)
    {
        using var stream = new MemoryStream(pdf);
        return OdfHybridPdfHelper.ExtractOdfFromPdf(stream, password);
    }

    [Theory]
    [MemberData(nameof(PlainFixtures))]
    public void ReaderOpensClassicAndStreamCrossReferencesWithoutRebuilding(string fixture)
    {
        PdfFile file = PdfFile.Open(File.ReadAllBytes(PdfTestSupport.FindFixture(fixture)));

        Assert.False(file.Rebuilt);
        Assert.False(file.IsEncrypted);
        Assert.Single(PdfTestSupport.Pages(file));
    }

    [Theory]
    [MemberData(nameof(PlainFixtures))]
    public void InjectThenExtractRoundTripsTheOdfBytes(string fixture)
    {
        byte[] original = File.ReadAllBytes(PdfTestSupport.FindFixture(fixture));
        byte[] hybrid = Inject(original);

        Assert.Equal(OdfPayload, Extract(hybrid));
        // 增量更新：原檔位元組完整保留在最前面。
        Assert.True(hybrid.AsSpan(0, original.Length).SequenceEqual(original));
        PdfFile reopened = PdfFile.Open(hybrid);
        Assert.False(reopened.Rebuilt);
        Assert.Single(PdfTestSupport.Pages(reopened));
    }

    [Theory]
    [MemberData(nameof(PlainFixtures))]
    public void InjectionAddsAssociatedFileAndPdfaSchemaToXmp(string fixture)
    {
        byte[] hybrid = Inject(File.ReadAllBytes(PdfTestSupport.FindFixture(fixture)));
        PdfFile file = PdfFile.Open(hybrid);
        PdfDict catalog = file.ResolveDict(file.Trailer["Root"])!;

        Assert.True(catalog.ContainsKey("AF"));
        Assert.True(catalog.ContainsKey("Names"));
        var metadata = (PdfStream)file.Resolve(catalog["Metadata"])!;
        string xmp = Encoding.UTF8.GetString(file.DecodeStream(metadata)!);
        Assert.Contains("PDF/A-3 Attachment Schema", xmp, StringComparison.Ordinal);
        Assert.Contains("Fixture 標題", xmp, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondInjectionKeepsBothAttachmentsAndStaysExtractable()
    {
        byte[] first = Inject(File.ReadAllBytes(PdfTestSupport.FindFixture("classic-xmp.pdf")));
        using var source = new MemoryStream(first);
        using var odf = new MemoryStream(Encoding.UTF8.GetBytes("second"));
        using var output = new MemoryStream();
        OdfHybridPdfHelper.InjectOdfToPdf(source, odf, output, "second.ods");
        byte[] second = output.ToArray();

        PdfFile file = PdfFile.Open(second);
        PdfDict catalog = file.ResolveDict(file.Trailer["Root"])!;
        List<object?> attachments = file.ResolveArray(catalog["AF"])!;
        Assert.Equal(2, attachments.Count);
        Assert.NotNull(Extract(second));
    }

    [Theory]
    [MemberData(nameof(EncryptedFixtures))]
    public void EncryptedPdfRoundTripsWithUserAndOwnerPasswords(string fixture)
    {
        byte[] original = File.ReadAllBytes(PdfTestSupport.FindFixture(fixture));

        foreach (string password in new[] { "user", "owner" })
        {
            byte[] hybrid = Inject(original, password);

            Assert.Equal(OdfPayload, Extract(hybrid, password));
            PdfFile reopened = PdfFile.Open(hybrid, password);
            Assert.True(reopened.IsEncrypted, "加密 PDF 注入後必須維持加密（trailer 需帶 /Encrypt）。");
            Assert.Single(PdfTestSupport.Pages(reopened));
        }
    }

    [Theory]
    [MemberData(nameof(EncryptedFixtures))]
    public void EncryptedPdfRejectsMissingAndWrongPasswords(string fixture)
    {
        byte[] original = File.ReadAllBytes(PdfTestSupport.FindFixture(fixture));

        Assert.Throws<CryptographicException>(() => Extract(original));
        Assert.Throws<CryptographicException>(() => Extract(original, "wrong"));
        Assert.Throws<CryptographicException>(() => Inject(original));
        Assert.Throws<CryptographicException>(() => Inject(original, "wrong"));
    }

    [Fact]
    public void PdfWithoutEmbeddedOdfReturnsNull()
    {
        Assert.Null(Extract(File.ReadAllBytes(PdfTestSupport.FindFixture("classic-xmp.pdf"))));
    }

    [Fact]
    public void ReaderRebuildsDamagedCrossReferenceTables()
    {
        byte[] damaged = File.ReadAllBytes(PdfTestSupport.FindFixture("classic-xmp.pdf"));
        string text = Encoding.Latin1.GetString(damaged);
        int start = text.LastIndexOf("startxref", StringComparison.Ordinal);
        damaged = Encoding.Latin1.GetBytes(string.Concat(text.AsSpan(0, start), "startxref\n99999\n%%EOF\n"));

        PdfFile file = PdfFile.Open(damaged);

        Assert.True(file.Rebuilt);
        Assert.Single(PdfTestSupport.Pages(file));
    }

    [Fact]
    public void ReaderRejectsFilesThatAreNotPdf()
    {
        Assert.Throws<InvalidDataException>(() => PdfFile.Open(Encoding.ASCII.GetBytes("this is plainly not a pdf document")));
        Assert.Throws<InvalidDataException>(() => PdfFile.Open([]));
    }

    [Fact]
    public async Task MalformedInputNeverHangsOrThrowsUnexpectedExceptionTypes()
    {
        var random = new Random(20261020);
        var seeds = new List<(byte[] Data, string? Password)>();
        foreach (string fixture in new[] { "classic-xmp.pdf", "objstm-xref-stream-xmp.pdf" })
        {
            seeds.Add((File.ReadAllBytes(PdfTestSupport.FindFixture(fixture)), null));
        }

        foreach (string fixture in new[] { "encrypted-r3-rc4-128.pdf", "encrypted-r4-aes-128.pdf", "encrypted-r6-aes-256.pdf" })
        {
            seeds.Add((File.ReadAllBytes(PdfTestSupport.FindFixture(fixture)), "user"));
        }

        foreach ((byte[] data, string? password) in seeds)
        {
            for (int iteration = 0; iteration < 120; iteration++)
            {
                byte[] mutated = (byte[])data.Clone();
                switch (iteration % 3)
                {
                    case 0:
                        for (int i = 0; i < 1 + random.Next(8); i++)
                        {
                            mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
                        }

                        break;
                    case 1:
                        mutated = mutated.AsSpan(0, random.Next(mutated.Length)).ToArray();
                        break;
                    default:
                        {
                            int start = random.Next(mutated.Length);
                            int length = Math.Min(random.Next(1, 64), mutated.Length - start);
                            mutated.AsSpan(start, length).Fill((byte)random.Next(256));
                            break;
                        }
                }

                Task attempt = Task.Run(() =>
                {
                    try
                    {
                        using var stream = new MemoryStream(mutated);
                        OdfHybridPdfHelper.ExtractOdfFromPdf(stream, password);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or CryptographicException or NotSupportedException)
                    {
                        // 受控的失敗型別。
                    }
                }, TestContext.Current.CancellationToken);

                Assert.True(await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken)) == attempt,
                    $"第 {iteration} 次變異讓讀取器卡住。");
                await attempt;
            }
        }
    }

    [Fact]
    public void InjectionValidatesArguments()
    {
        byte[] pdf = File.ReadAllBytes(PdfTestSupport.FindFixture("classic-xmp.pdf"));
        using var source = new MemoryStream(pdf);
        using var odf = new MemoryStream(OdfPayload);
        using var output = new MemoryStream();

        Assert.Throws<ArgumentException>(() => OdfHybridPdfHelper.InjectOdfToPdf(source, odf, output, " "));
        Assert.Throws<ArgumentNullException>(() => OdfHybridPdfHelper.ExtractOdfFromPdf((Stream)null!));
    }
}
