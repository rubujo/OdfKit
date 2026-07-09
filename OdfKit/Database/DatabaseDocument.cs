using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Database;

/// <summary>
/// Represents a high-level ODF database document.
/// 表示高階 ODF 資料庫文件（Database Document）的類別。
/// </summary>
public class DatabaseDocument : OdfDatabaseDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="DatabaseDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public DatabaseDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new high-level ODB database document.
    /// 建立新的高階 ODB 資料庫文件。
    /// </summary>
    /// <returns>A new <see cref="DatabaseDocument"/> instance. / 新的 <see cref="DatabaseDocument"/> 執行個體。</returns>
    public new static DatabaseDocument Create()
    {
        return (DatabaseDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Database);
    }

    /// <summary>
    /// Loads a high-level ODB database document from the specified path.
    /// 從指定路徑載入高階 ODB 資料庫文件。
    /// </summary>
    /// <param name="path">The ODB document path. / ODB 文件路徑。</param>
    /// <returns>The loaded <see cref="DatabaseDocument"/> instance. / 載入完成的 <see cref="DatabaseDocument"/> 執行個體。</returns>
    public new static DatabaseDocument Load(string path)
    {
        return EnsureDatabaseDocument(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODB database document from the specified path.
    /// 非同步從指定路徑載入高階 ODB 資料庫文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="DatabaseDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="DatabaseDocument"/>。</returns>
    public new static Task<DatabaseDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts path and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public new static async Task<DatabaseDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        EnsureDatabaseDocument(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a high-level ODB database document from the specified stream.
    /// 從指定資料流載入高階 ODB 資料庫文件。
    /// </summary>
    /// <returns>The loaded <see cref="DatabaseDocument"/> instance. / 載入完成的 <see cref="DatabaseDocument"/> 執行個體。</returns>
    public new static DatabaseDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Full overload of Load that accepts stream and fileName.
    /// Load 完整多載：接受 stream 與 fileName。
    /// </summary>
    public new static DatabaseDocument Load(Stream stream, string? fileName)
    {
        return EnsureDatabaseDocument(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODB database document from the specified stream.
    /// 非同步從指定資料流載入高階 ODB 資料庫文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="DatabaseDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="DatabaseDocument"/>。</returns>
    public new static Task<DatabaseDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public new static Task<DatabaseDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public new static Task<DatabaseDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream, fileName, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、fileName 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public new static async Task<DatabaseDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        EnsureDatabaseDocument(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static DatabaseDocument EnsureDatabaseDocument(OdfDocument document)
    {
        if (document is DatabaseDocument database && document.DocumentKind == OdfDocumentKind.Database)
        {
            return database;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_SpecifiedOdfFileOdb"));
    }
}
