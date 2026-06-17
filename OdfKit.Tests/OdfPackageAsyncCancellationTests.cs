using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODF 封裝非同步儲存管線的 CancellationToken 協作取消行為。
/// </summary>
public class OdfPackageAsyncCancellationTests
{
    private const string MinimalContentXml =
        """<?xml version="1.0" encoding="UTF-8"?><office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.3"><office:body><office:text><text:p>取消測試</text:p></office:text></office:body></office:document-content>""";

    /// <summary>
    /// 預先取消的語彙應使 SaveToStreamAsync 拋出 OperationCanceledException。
    /// </summary>
    [Fact]
    public async Task SaveToStreamAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var package = OdfPackage.Create(new MemoryStream());
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(MinimalContentXml), "text/xml");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var destination = new MemoryStream();
            await package.SaveToStreamAsync(destination, cancellationToken: cts.Token);
        });
    }

    /// <summary>
    /// 未取消時 SaveToStreamAsync 應成功寫入可讀取的 ODF 封裝。
    /// </summary>
    [Fact]
    public async Task SaveToStreamAsync_DefaultToken_CompletesSuccessfully()
    {
        using var package = OdfPackage.Create(new MemoryStream());
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(MinimalContentXml), "text/xml");

        await using var destination = new MemoryStream();
        await package.SaveToStreamAsync(destination, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(destination.Length > 0);
        destination.Position = 0;
        using var loaded = OdfPackage.Open(destination, leaveOpen: true);
        Assert.True(loaded.HasEntry("content.xml"));
    }
}
