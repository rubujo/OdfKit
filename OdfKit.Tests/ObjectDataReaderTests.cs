using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 <see cref="ObjectDataReader{T}"/> 與 <see cref="OdsStreamWriter.WriteDataAsync{T}(IEnumerable{T}, bool, CancellationToken)"/> 系列多載的行為。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
public class ObjectDataReaderTests
{
    private sealed class Widget
    {
        public string? Name { get; set; }

        public double Amount { get; set; }

        public bool Active { get; set; }

        public DateTime Created { get; set; }
    }

    private sealed class NoPublicProperties
    {
        private readonly int _value = 1;

        internal int Value => _value;
    }

    private static readonly Widget[] SampleWidgets =
    [
        new Widget { Name = "Alice", Amount = 3.5d, Active = true, Created = new DateTime(2026, 6, 26, 8, 30, 0, DateTimeKind.Utc) },
        new Widget { Name = "Bob", Amount = 7d, Active = false, Created = new DateTime(2026, 6, 27, 9, 0, 0, DateTimeKind.Utc) },
    ];

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (T item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    /// <summary>
    /// 驗證從同步來源建立時，欄位名稱、序數與逐列取值皆正確。
    /// </summary>
    [Fact]
    public void FromEnumerable_MapsPropertiesToColumns()
    {
        using var reader = new ObjectDataReader<Widget>(SampleWidgets);

        Assert.Equal(4, reader.FieldCount);
        Assert.Equal("Name", reader.GetName(0));
        Assert.Equal(0, reader.GetOrdinal("Name"));
        Assert.Equal(-1, reader.GetOrdinal("NoSuchColumn"));

        Assert.True(reader.Read());
        Assert.Equal("Alice", reader.GetValue(reader.GetOrdinal("Name")));
        Assert.Equal(3.5d, reader.GetDouble(reader.GetOrdinal("Amount")));
        Assert.True(reader.GetBoolean(reader.GetOrdinal("Active")));
        Assert.False(reader.IsDBNull(reader.GetOrdinal("Name")));

        Assert.True(reader.Read());
        Assert.Equal("Bob", reader.GetValue(reader.GetOrdinal("Name")));

        Assert.False(reader.Read());
    }

    /// <summary>
    /// 驗證屬性值為 <see langword="null"/> 時對應 <see cref="System.Data.Common.DbDataReader.IsDBNull(int)"/> 為 true。
    /// </summary>
    [Fact]
    public void FromEnumerable_NullPropertyValueIsDbNull()
    {
        Widget[] withNull = [new Widget { Name = null, Amount = 1d, Active = true, Created = DateTime.UtcNow }];
        using var reader = new ObjectDataReader<Widget>(withNull);

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(reader.GetOrdinal("Name")));
    }

    /// <summary>
    /// 驗證從非同步來源建立時，<see cref="System.Data.Common.DbDataReader.ReadAsync(CancellationToken)"/> 可正確逐列前進。
    /// </summary>
    [Fact]
    public async Task FromAsyncEnumerable_ReadAsyncAdvancesRows()
    {
        using var reader = new ObjectDataReader<Widget>(ToAsyncEnumerable(SampleWidgets, TestContext.Current.CancellationToken));

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Alice", reader.GetValue(reader.GetOrdinal("Name")));

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Bob", reader.GetValue(reader.GetOrdinal("Name")));

        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 驗證來源為 <see langword="null"/> 時擲出 <see cref="ArgumentNullException"/>。
    /// </summary>
    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ObjectDataReader<Widget>((IEnumerable<Widget>)null!));
        Assert.Throws<ArgumentNullException>(() => new ObjectDataReader<Widget>((IAsyncEnumerable<Widget>)null!));
    }

    /// <summary>
    /// 驗證元素型別沒有任何可讀公開屬性時擲出在地化的 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void Constructor_TypeWithoutReadableProperties_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ObjectDataReader<NoPublicProperties>([]));
        Assert.Contains("NoPublicProperties", ex.Message);
    }

    /// <summary>
    /// 驗證釋放後再存取擲出 <see cref="ObjectDisposedException"/>。
    /// </summary>
    [Fact]
    public void Dispose_ThenAccess_ThrowsObjectDisposedException()
    {
        var reader = new ObjectDataReader<Widget>(SampleWidgets);
        reader.Read();
        reader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.Read());
    }

    /// <summary>
    /// 驗證 <see cref="OdsStreamWriter.WriteDataAsync{T}(IEnumerable{T}, bool, CancellationToken)"/> 可將任意物件序列寫入
    /// ODS 並可透過 <see cref="OdsStreamReader"/> 讀回。
    /// </summary>
    [Fact]
    public async Task WriteDataAsync_FromEnumerable_RoundTripsThroughOdsStreamReader()
    {
        await using var stream = new MemoryStream();
        await using (var writer = new OdsStreamWriter(stream))
        {
            writer.WriteStartSheet("Widgets");
            await writer.WriteDataAsync(SampleWidgets, includeColumnNames: true, TestContext.Current.CancellationToken);
            writer.WriteEndSheet();
        }

        stream.Position = 0;
        using var reader = new OdsStreamReader(stream);
        Assert.True(reader.Read());
        Assert.Equal("Name", reader.GetValue(0));
        Assert.Equal("Amount", reader.GetValue(1));

        Assert.True(reader.Read());
        Assert.Equal("Alice", reader.GetValue(0));
        Assert.Equal(3.5d, reader.GetValue(1));
    }

    /// <summary>
    /// 驗證 <see cref="OdsStreamWriter.WriteDataAsync{T}(IAsyncEnumerable{T}, bool, CancellationToken)"/> 可串流寫入非同步物件序列。
    /// </summary>
    [Fact]
    public async Task WriteDataAsync_FromAsyncEnumerable_RoundTripsThroughOdsStreamReader()
    {
        await using var stream = new MemoryStream();
        await using (var writer = new OdsStreamWriter(stream))
        {
            writer.WriteStartSheet("Widgets");
            await writer.WriteDataAsync(
                ToAsyncEnumerable(SampleWidgets, TestContext.Current.CancellationToken),
                includeColumnNames: true,
                TestContext.Current.CancellationToken);
            writer.WriteEndSheet();
        }

        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry("content.xml")!;
        using var entryStream = entry.Open();
        using var streamReader = new StreamReader(entryStream);
        string contentXml = streamReader.ReadToEnd();

        Assert.Contains("Alice", contentXml);
        Assert.Contains("Bob", contentXml);
        Assert.Contains("office:boolean-value=\"true\"", contentXml);
    }
}
