using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 非同步以密碼加密並儲存封裝至原始目的地。
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
        OdfSaveOptions options = CreateEncryptedSaveOptions(password, algorithm);
        return SaveAsync(options, cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼加密並儲存封裝至指定資料流。
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

        OdfSaveOptions options = CreateEncryptedSaveOptions(password, algorithm);
        return SaveToStreamAsync(destinationStream, options, cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼解密並載入指定路徑的 ODF 封裝。
    /// </summary>
    /// <param name="path">ODF 封裝路徑</param>
    /// <param name="password">解密密碼</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為已解密封裝</returns>
    public static Task<OdfPackage> LoadEncryptedAsync(
        string path,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        return OpenAsync(path, CreateEncryptedLoadOptions(password), cancellationToken);
    }

    /// <summary>
    /// 非同步以密碼解密並載入指定資料流的 ODF 封裝。
    /// </summary>
    /// <param name="stream">ODF 封裝資料流</param>
    /// <param name="password">解密密碼</param>
    /// <param name="leaveOpen">處置封裝時是否保持資料流開啟</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為已解密封裝</returns>
    public static Task<OdfPackage> LoadEncryptedAsync(
        Stream stream,
        string password,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        return OpenAsync(stream, leaveOpen, CreateEncryptedLoadOptions(password), cancellationToken);
    }

    internal static OdfSaveOptions CreateEncryptedSaveOptions(
        string password,
        OdfEncryptionAlgorithm algorithm)
    {
        if (password is null)
            throw new ArgumentNullException(nameof(password));

        return new OdfSaveOptions
        {
            Password = password,
            EncryptionAlgorithm = algorithm,
        };
    }

    internal static OdfLoadOptions CreateEncryptedLoadOptions(string password)
    {
        if (password is null)
            throw new ArgumentNullException(nameof(password));

        return new OdfLoadOptions
        {
            Password = password,
        };
    }
}
