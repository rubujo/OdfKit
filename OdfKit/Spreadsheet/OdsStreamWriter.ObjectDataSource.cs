using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the OdsStreamWriter API.
/// 提供 OdsStreamWriter API。
/// </summary>

public partial class OdsStreamWriter
{
    /// <summary>
    /// Writes an arbitrary object sequence to the current worksheet row by row with low memory usage, mapping each readable public instance property of <typeparamref name="T"/> to a column via <see cref="ObjectDataReader{T}"/>. This lets query projections (for example an Entity Framework Core <c>IQueryable&lt;T&gt;</c> after <c>.Select(...)</c>) be exported without materializing the whole sequence in memory.
    /// 以低記憶體方式將任意物件序列逐列寫入目前工作表，透過 <see cref="ObjectDataReader{T}"/> 把 <typeparamref name="T"/> 的每個可讀公開執行個體屬性對應到一個資料行。這讓查詢投影（例如 Entity Framework Core <c>IQueryable&lt;T&gt;</c> 經 <c>.Select(...)</c> 後的結果）可以匯出而不必先整個載入記憶體。
    /// </summary>
    /// <typeparam name="T">The element type whose readable public instance properties become worksheet columns. / 元素型別，其可讀公開執行個體屬性將成為工作表資料行。</typeparam>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>. / 當 <paramref name="source"/> 為 <see langword="null"/> 時擲出。</exception>
    public Task WriteDataAsync<T>(IEnumerable<T> source) => WriteDataAsync(source, false, default);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public Task WriteDataAsync<T>(IEnumerable<T> source, bool includeColumnNames) => WriteDataAsync(source, includeColumnNames, default);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public async Task WriteDataAsync<T>(
        IEnumerable<T> source,
        bool includeColumnNames,
        CancellationToken cancellationToken)
    {
        using var reader = new ObjectDataReader<T>(source);
        await WriteDataAsync(reader, includeColumnNames, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an asynchronous object sequence to the current worksheet row by row with low memory usage, mapping each readable public instance property of <typeparamref name="T"/> to a column via <see cref="ObjectDataReader{T}"/>. Pass an Entity Framework Core query's <c>AsAsyncEnumerable()</c> result here to stream rows from the database directly into the worksheet.
    /// 以低記憶體方式將非同步物件序列逐列寫入目前工作表，透過 <see cref="ObjectDataReader{T}"/> 把 <typeparamref name="T"/> 的每個可讀公開執行個體屬性對應到一個資料行。可傳入 Entity Framework Core 查詢的 <c>AsAsyncEnumerable()</c> 結果，將資料庫查詢結果直接串流寫入工作表。
    /// </summary>
    /// <typeparam name="T">The element type whose readable public instance properties become worksheet columns. / 元素型別，其可讀公開執行個體屬性將成為工作表資料行。</typeparam>
    /// <returns>A task that represents the asynchronous write operation. / 代表非同步寫入作業的工作。</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>. / 當 <paramref name="source"/> 為 <see langword="null"/> 時擲出。</exception>
    public Task WriteDataAsync<T>(IAsyncEnumerable<T> source) => WriteDataAsync(source, false, default);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public Task WriteDataAsync<T>(IAsyncEnumerable<T> source, bool includeColumnNames) => WriteDataAsync(source, includeColumnNames, default);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public async Task WriteDataAsync<T>(
        IAsyncEnumerable<T> source,
        bool includeColumnNames,
        CancellationToken cancellationToken)
    {
        using var reader = new ObjectDataReader<T>(source);
        await WriteDataAsync(reader, includeColumnNames, cancellationToken).ConfigureAwait(false);
    }
}
