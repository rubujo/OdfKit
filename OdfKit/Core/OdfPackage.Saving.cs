using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
namespace OdfKit.Core;
/// <summary>
/// Adds save and stream serialization APIs for ODF packages.
/// 提供 ODF 封裝的儲存與資料流序列化 API。
/// </summary>

public sealed partial class OdfPackage
{
    #region Saving and Atomic Save

    /// <summary>
    /// Performs the Save operation.
    /// 將所有變更儲存回原來的檔案或資料流中。
    /// </summary>
    public void Save() => Save((OdfSaveOptions?)null);

    /// <summary>
    /// Full overload of Save that accepts options.
    /// Save 完整多載：接受 options。
    /// </summary>
    public void Save(OdfSaveOptions? options)
    {
        if (_mode == OdfPackageMode.Read)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackage_CannotSaveReadOnly_2"));

        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            OdfPackageSaver.SaveToUnderlyingStream(this, includeRdfMetadata: true);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// Saves async.
    /// 將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <returns>代表非同步作業的工作</returns>
    public Task SaveAsync() => SaveAsync(null, default);

    /// <summary>
    /// Short overload of SaveAsync that accepts cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 SaveAsync 多載。
    /// </summary>
    public Task SaveAsync(CancellationToken cancellationToken) => SaveAsync(null, cancellationToken);

    /// <summary>
    /// Short overload of SaveAsync that accepts options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 options；其餘可選參數使用預設值並轉呼叫最長 SaveAsync 多載。
    /// </summary>
    public Task SaveAsync(OdfSaveOptions? options) => SaveAsync(options, default);

    /// <summary>
    /// Saves async.
    /// 使用指定儲存選項，將所有變更儲存回原來的檔案或資料流中（非同步）。
    /// </summary>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 寫入與串流 I/O 期間協作檢查取消語彙。
    /// </remarks>
    public async Task SaveAsync(OdfSaveOptions? options, CancellationToken cancellationToken)
    {
        if (_mode == OdfPackageMode.Read)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackage_CannotSaveReadOnly_2"));

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            await OdfPackageSaver.SaveToUnderlyingStreamAsync(this, includeRdfMetadata: true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// Saves to stream.
    /// 將封裝序列化儲存至指定的目的地資料流。
    /// </summary>
    public void SaveToStream(Stream destinationStream) => SaveToStream(destinationStream, null);

    /// <summary>
    /// Full overload of SaveToStream that accepts destinationStream and options.
    /// SaveToStream 完整多載：接受 destinationStream 與 options。
    /// </summary>
    public void SaveToStream(Stream destinationStream, OdfSaveOptions? options)
    {
        _lock.Wait();
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            OdfPackageSaver.SaveToStream(this, destinationStream, includeRdfMetadata: true);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    /// <summary>
    /// Performs the Save operation.
    /// 將封裝序列化儲存至指定的位元組緩衝區寫入器。
    /// </summary>
    /// <remarks>
    /// 此入口會將 ZIP 或 Flat XML 輸出直接寫入 <paramref name="destination"/>，適合與 ASP.NET Core、
    /// pipelines 或自訂零拷貝緩衝區整合，避免呼叫端必須先建立中介 <see cref="MemoryStream"/>。
    /// </remarks>
    public void Save(IBufferWriter<byte> destination) => Save(destination, null);

    /// <summary>
    /// Full overload of Save that accepts destination and options.
    /// Save 完整多載：接受 destination 與 options。
    /// </summary>
    public void Save(IBufferWriter<byte> destination, OdfSaveOptions? options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(destination, nameof(destination));

        using var stream = new OdfBufferWriterStream(destination);
        SaveToStream(stream, options);
    }

    /// <summary>
    /// Saves to stream async.
    /// 將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <returns>代表非同步作業的工作</returns>
    public Task SaveToStreamAsync(Stream destinationStream) => SaveToStreamAsync(destinationStream, null, default);

    /// <summary>
    /// Short overload of SaveToStreamAsync that accepts destinationStream and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 destinationStream 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 SaveToStreamAsync 多載。
    /// </summary>
    public Task SaveToStreamAsync(Stream destinationStream, CancellationToken cancellationToken) => SaveToStreamAsync(destinationStream, null, cancellationToken);

    /// <summary>
    /// Short overload of SaveToStreamAsync that accepts destinationStream and options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 destinationStream 與 options；其餘可選參數使用預設值並轉呼叫最長 SaveToStreamAsync 多載。
    /// </summary>
    public Task SaveToStreamAsync(Stream destinationStream, OdfSaveOptions? options) => SaveToStreamAsync(destinationStream, options, default);

    /// <summary>
    /// Saves to stream async.
    /// 使用指定儲存選項，將封裝序列化儲存至指定的目的地資料流（非同步）。
    /// </summary>
    /// <param name="destinationStream">目標目的地資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的工作</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 寫入與串流 I/O 期間協作檢查取消語彙。
    /// </remarks>
    public async Task SaveToStreamAsync(
        Stream destinationStream,
        OdfSaveOptions? options,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        OdfSaveOptions previousOptions = UseSaveOptions(options);
        try
        {
            await OdfPackageSaver.SaveToStreamAsync(this, destinationStream, includeRdfMetadata: true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _saveOptions = previousOptions;
            _lock.Release();
        }
    }

    private OdfSaveOptions UseSaveOptions(OdfSaveOptions? options)
    {
        OdfSaveOptions previousOptions = _saveOptions;
        if (options is not null)
            _saveOptions = options;

        return previousOptions;
    }

    #endregion
}
