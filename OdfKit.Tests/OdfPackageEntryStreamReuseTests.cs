using System.IO;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 OdfPackageEntry 以 Stream 支援內容時，OpenReader() 是否可安全重複取用。
/// </summary>
public class OdfPackageEntryStreamReuseTests
{
    /// <summary>
    /// 驗證以 Stream 支援內容的項目在第一次 OpenReader() 取用並釋放後，
    /// 第二次呼叫仍可正常讀取，不會擲出 ObjectDisposedException。
    /// </summary>
    [Fact]
    public void OpenReaderCanBeCalledRepeatedlyForStreamBackedEntry()
    {
        var entry = new OdfPackageEntry("content.xml", new MemoryStream(new byte[] { 1, 2, 3 }));

        using (Stream first = entry.OpenReader())
        {
            var buffer = new byte[3];
            first.ReadExactly(buffer, 0, 3);
            Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        }

        using (Stream second = entry.OpenReader())
        {
            var buffer = new byte[3];
            second.ReadExactly(buffer, 0, 3);
            Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        }
    }

    /// <summary>
    /// 驗證 SetContent(Stream) 轉移串流所有權後，同樣可重複安全取用。
    /// </summary>
    [Fact]
    public void OpenReaderCanBeCalledRepeatedlyAfterSetContentStream()
    {
        var entry = new OdfPackageEntry("content.xml", new byte[] { 9 });
        entry.SetContent(new MemoryStream(new byte[] { 4, 5, 6 }));

        using (Stream first = entry.OpenReader())
        {
            first.ReadByte();
        }

        using (Stream second = entry.OpenReader())
        {
            Assert.Equal(4, second.ReadByte());
        }
    }
}
