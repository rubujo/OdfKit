using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Provides object-binding APIs for <see cref="SpreadsheetDocument"/>.
/// 提供 <see cref="SpreadsheetDocument"/> 的物件繫結 API。
/// </summary>
public partial class SpreadsheetDocument
{
    /// <summary>
    /// Imports records into a worksheet using public readable members.
    /// 使用可讀公開成員將記錄匯入工作表。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left A1 address. / 左上角 A1 位址。</param>
    /// <param name="records">The records to import. / 要匯入的記錄。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport ImportRecords<T>(string sheetName, string startAddress, IEnumerable<T> records) =>
        WriteObjects(sheetName, OdfCellAddress.ParseExcel(startAddress), records, null);

    /// <summary>
    /// Imports records into a worksheet using typed binding options.
    /// 使用具型別繫結選項將記錄匯入工作表。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left A1 address. / 左上角 A1 位址。</param>
    /// <param name="records">The records to import. / 要匯入的記錄。</param>
    /// <param name="options">The typed binding options. / 具型別繫結選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport ImportRecords<T>(string sheetName, string startAddress, IEnumerable<T> records, OdfObjectBindingOptions? options) =>
        WriteObjects(sheetName, OdfCellAddress.ParseExcel(startAddress), records, options);

    /// <summary>
    /// Reads worksheet rows into records using an A1 range.
    /// 使用 A1 範圍將工作表資料列讀成記錄。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The A1 source range. / A1 來源範圍。</param>
    /// <returns>The materialized records. / 具體化後的記錄。</returns>
    public IReadOnlyList<T> ReadRecords<T>(string sheetName, string range) where T : new() =>
        ReadObjects<T>(sheetName, OdfCellRange.ParseExcel(range), null);

    /// <summary>
    /// Reads worksheet rows into records using typed read options.
    /// 使用具型別讀取選項將工作表資料列讀成記錄。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The A1 source range. / A1 來源範圍。</param>
    /// <param name="options">The typed read options. / 具型別讀取選項。</param>
    /// <returns>The materialized records. / 具體化後的記錄。</returns>
    public IReadOnlyList<T> ReadRecords<T>(string sheetName, string range, OdfObjectReadOptions? options) where T : new() =>
        ReadObjects<T>(sheetName, OdfCellRange.ParseExcel(range), options);

    /// <summary>
    /// Imports asynchronous records into an in-memory worksheet using readable public members.
    /// 使用可讀公開成員，將非同步記錄匯入記憶體內的工作表。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left A1 address. / 左上角 A1 位址。</param>
    /// <param name="records">The asynchronous record source. / 非同步記錄來源。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public Task<OdfObjectBindingReport> ImportRecordsAsync<T>(string sheetName, string startAddress, IAsyncEnumerable<T> records) =>
        ImportRecordsAsync(sheetName, startAddress, records, null, default);

    /// <summary>
    /// Short overload of ImportRecordsAsync that accepts sheetName, startAddress, records, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 sheetName、startAddress、records 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 ImportRecordsAsync 多載。
    /// </summary>
    public Task<OdfObjectBindingReport> ImportRecordsAsync<T>(string sheetName, string startAddress, IAsyncEnumerable<T> records, CancellationToken cancellationToken) =>
        ImportRecordsAsync(sheetName, startAddress, records, null, cancellationToken);

    /// <summary>
    /// Imports records from an asynchronous source into a worksheet using typed binding options.
    /// 從非同步來源匯入記錄至工作表，使用具型別繫結選項。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left A1 address. / 左上角 A1 位址。</param>
    /// <param name="records">The asynchronous record source. / 非同步記錄來源。</param>
    /// <param name="options">The typed binding options. / 具型別繫結選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="records"/> is null. / 當 <paramref name="records"/> 為 null 時擲出。</exception>
    public async Task<OdfObjectBindingReport> ImportRecordsAsync<T>(
        string sheetName,
        string startAddress,
        IAsyncEnumerable<T> records,
        OdfObjectBindingOptions? options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(records, nameof(records));
        cancellationToken.ThrowIfCancellationRequested();

        var buffered = new List<T>();
        await foreach (T record in records.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            buffered.Add(record);
        }

        return WriteObjects(sheetName, OdfCellAddress.ParseExcel(startAddress), buffered, options);
    }

    /// <summary>
    /// Reads an A1 worksheet range into an asynchronous record sequence.
    /// 將 A1 工作表範圍讀取為非同步記錄序列。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The A1 source range. / A1 來源範圍。</param>
    /// <returns>The materialized records, yielded asynchronously. / 具體化後的記錄，以非同步方式產生。</returns>
    public IAsyncEnumerable<T> ReadRecordsAsync<T>(string sheetName, string range) where T : new() =>
        ReadRecordsAsync<T>(sheetName, range, null, default);

    /// <summary>
    /// Short overload of ReadRecordsAsync that accepts sheetName, range, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 sheetName、range 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 ReadRecordsAsync 多載。
    /// </summary>
    public IAsyncEnumerable<T> ReadRecordsAsync<T>(string sheetName, string range, CancellationToken cancellationToken) where T : new() =>
        ReadRecordsAsync<T>(sheetName, range, null, cancellationToken);

    /// <summary>
    /// Reads worksheet rows into records asynchronously using typed read options.
    /// 使用具型別讀取選項非同步將工作表資料列讀成記錄。
    /// </summary>
    /// <typeparam name="T">The record type. / 記錄型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The A1 source range. / A1 來源範圍。</param>
    /// <param name="options">The typed read options. / 具型別讀取選項。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The materialized records, yielded asynchronously. / 具體化後的記錄，以非同步方式產生。</returns>
    public async IAsyncEnumerable<T> ReadRecordsAsync<T>(
        string sheetName,
        string range,
        OdfObjectReadOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken) where T : new()
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<T> records = ReadRecords<T>(sheetName, range, options);
        foreach (T record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfObjectBindingReport WriteObjects<T>(string sheetName, OdfCellAddress startAddress, IEnumerable<T> items) => WriteObjects(sheetName, startAddress, items, null);

    /// <summary>
    /// Writes public readable object properties into the specified worksheet.
    /// 將物件的可讀公開屬性寫入指定工作表。
    /// </summary>
    /// <typeparam name="T">The object type to write. / 要寫入的物件型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="startAddress">The top-left target cell. / 左上角目標儲存格。</param>
    /// <param name="items">The object sequence to write. / 要寫入的物件序列。</param>
    /// <param name="options">The object binding options. / 物件繫結選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport WriteObjects<T>(string sheetName, OdfCellAddress startAddress, IEnumerable<T> items, OdfObjectBindingOptions? options) =>
        RequireSheet(sheetName).WriteObjects(startAddress, items, options);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfObjectBindingReport AppendObjects<T>(string sheetName, IEnumerable<T> items) => AppendObjects(sheetName, items, null);


    /// <summary>
    /// Appends public readable object properties after the used range of the specified worksheet.
    /// 將物件的可讀公開屬性附加到指定工作表已使用範圍之後。
    /// </summary>
    /// <typeparam name="T">The object type to append. / 要附加的物件型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="items">The object sequence to append. / 要附加的物件序列。</param>
    /// <param name="options">The object binding options. / 物件繫結選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport AppendObjects<T>(string sheetName, IEnumerable<T> items, OdfObjectBindingOptions? options) =>
        RequireSheet(sheetName).AppendObjects(items, options);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public IReadOnlyList<T> ReadObjects<T>(string sheetName, OdfCellRange range) where T : new() => ReadObjects<T>(sheetName, range, null);


    /// <summary>
    /// Reads rows from the specified worksheet into objects using the header row as the property map.
    /// 使用標題列作為屬性對應，將指定工作表資料列讀成物件。
    /// </summary>
    /// <typeparam name="T">The object type to create. / 要建立的物件型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="options">The object read options. / 物件讀取選項。</param>
    /// <returns>The materialized object list. / 具體化後的物件清單。</returns>
    public IReadOnlyList<T> ReadObjects<T>(string sheetName, OdfCellRange range, OdfObjectReadOptions? options) where T : new() =>
        RequireSheet(sheetName).ReadObjects<T>(range, options);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfObjectBindingValidationReport ValidateObjectBinding<T>(string sheetName, OdfCellRange range) where T : new() => ValidateObjectBinding<T>(sheetName, range, null);


    /// <summary>
    /// Validates whether a worksheet range can be bound to the specified object type.
    /// 驗證工作表範圍是否可繫結至指定物件型別。
    /// </summary>
    /// <typeparam name="T">The object type to validate. / 要驗證的物件型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The source cell range. / 來源儲存格範圍。</param>
    /// <param name="options">The validation options. / 驗證選項。</param>
    /// <returns>The validation report. / 驗證報告。</returns>
    public OdfObjectBindingValidationReport ValidateObjectBinding<T>(string sheetName, OdfCellRange range, OdfObjectReadOptions? options) where T : new() =>
        RequireSheet(sheetName).ValidateObjectBinding<T>(range, options);


    /// <summary>
    /// Updates existing object-bound rows by key without inserting new rows.
    /// 依 key 更新既有物件繫結資料列且不新增資料列。
    /// </summary>
    /// <typeparam name="T">The object type to update. / 要更新的物件型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The target table range. / 目標資料表範圍。</param>
    /// <param name="items">The object sequence to update. / 要更新的物件序列。</param>
    /// <param name="options">The update options. / 更新選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport UpdateObjects<T>(
        string sheetName,
        OdfCellRange range,
        IEnumerable<T> items,
        OdfObjectUpdateOptions options) =>
        RequireSheet(sheetName).UpdateObjects(range, items, options);

    /// <summary>
    /// Updates object-bound rows by key and inserts rows for missing keys.
    /// 依 key 更新物件繫結資料列，並針對缺少的 key 新增資料列。
    /// </summary>
    /// <typeparam name="T">The object type to upsert. / 要 upsert 的物件型別。</typeparam>
    /// <param name="sheetName">The worksheet name. / 工作表名稱。</param>
    /// <param name="range">The target table range. / 目標資料表範圍。</param>
    /// <param name="items">The object sequence to upsert. / 要 upsert 的物件序列。</param>
    /// <param name="options">The update options. / 更新選項。</param>
    /// <returns>The object binding report. / 物件繫結報告。</returns>
    public OdfObjectBindingReport UpsertObjects<T>(
        string sheetName,
        OdfCellRange range,
        IEnumerable<T> items,
        OdfObjectUpdateOptions options) =>
        RequireSheet(sheetName).UpsertObjects(range, items, options);
}
