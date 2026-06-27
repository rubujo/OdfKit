using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    /// <summary>
    /// 非同步以密碼加密並儲存文件至原始封裝目的地。
    /// </summary>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法；預設為 AES-256</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步儲存作業的工作</returns>
    public Task SaveEncryptedAsync(
        string password,
        OdfEncryptionAlgorithm algorithm = OdfEncryptionAlgorithm.Aes256,
        CancellationToken cancellationToken = default)
    {
        OdfSaveOptions options = OdfPackage.CreateEncryptedSaveOptions(password, algorithm);
        return SaveAsync(options, cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼加密並儲存文件至指定路徑。
    /// </summary>
    /// <param name="path">目標檔案路徑</param>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法；預設為 AES-256</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步儲存作業的工作</returns>
    public Task SaveEncryptedAsync(
        string path,
        string password,
        OdfEncryptionAlgorithm algorithm = OdfEncryptionAlgorithm.Aes256,
        CancellationToken cancellationToken = default)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        OdfSaveOptions options = OdfPackage.CreateEncryptedSaveOptions(password, algorithm);
        return SaveAsync(path, options, cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼加密並儲存文件至指定資料流。
    /// </summary>
    /// <param name="destinationStream">目標資料流</param>
    /// <param name="password">加密密碼</param>
    /// <param name="algorithm">加密演算法；預設為 AES-256</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步儲存作業的工作</returns>
    public Task SaveEncryptedAsync(
        Stream destinationStream,
        string password,
        OdfEncryptionAlgorithm algorithm = OdfEncryptionAlgorithm.Aes256,
        CancellationToken cancellationToken = default)
    {
        if (destinationStream is null)
            throw new ArgumentNullException(nameof(destinationStream));

        OdfSaveOptions options = OdfPackage.CreateEncryptedSaveOptions(password, algorithm);
        return SaveToStreamAsync(destinationStream, options, cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼解密並載入指定路徑的 ODF 文件。
    /// </summary>
    /// <param name="path">ODF 文件路徑</param>
    /// <param name="password">解密密碼</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為已解密文件</returns>
    public static Task<OdfDocument> LoadEncryptedAsync(
        string path,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        return LoadAsync(path, OdfPackage.CreateEncryptedLoadOptions(password), cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼解密並載入指定資料流的 ODF 文件。
    /// </summary>
    /// <param name="stream">ODF 文件資料流</param>
    /// <param name="password">解密密碼</param>
    /// <param name="fileName">選用檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為已解密文件</returns>
    public static Task<OdfDocument> LoadEncryptedAsync(
        Stream stream,
        string password,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        return LoadAsync(stream, OdfPackage.CreateEncryptedLoadOptions(password), fileName, cancellationToken);
    }
}
