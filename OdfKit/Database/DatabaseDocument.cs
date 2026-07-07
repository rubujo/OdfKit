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
    /// <param name="path">The ODB document path. / ODB 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="DatabaseDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="DatabaseDocument"/>。</returns>
    public new static async Task<DatabaseDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureDatabaseDocument(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a high-level ODB database document from the specified stream.
    /// 從指定資料流載入高階 ODB 資料庫文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODB document content. / 包含 ODB 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="DatabaseDocument"/> instance. / 載入完成的 <see cref="DatabaseDocument"/> 執行個體。</returns>
    public new static DatabaseDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureDatabaseDocument(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODB database document from the specified stream.
    /// 非同步從指定資料流載入高階 ODB 資料庫文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODB document content. / 包含 ODB 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="DatabaseDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="DatabaseDocument"/>。</returns>
    public new static async Task<DatabaseDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
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
