using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;
/// <summary>
/// Adds asynchronous encrypted save and load helpers for ODF packages.
/// 提供 ODF 封裝的非同步加密儲存與載入輔助方法。
/// </summary>

public sealed partial class OdfPackage
{
    /// <summary>
    /// Asynchronously saves the package to its original destination with password encryption.
    /// 非同步以密碼加密並將封裝儲存至原始目的地。
    /// </summary>
    /// <param name="password">The password used to encrypt the package. / 用於加密封裝的密碼。</param>
    /// <param name="algorithm">The encryption algorithm to use. / 要使用的加密演算法。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous save operation. / 代表非同步儲存作業的工作。</returns>
    public Task SaveEncryptedAsync(
        string password,
        OdfEncryptionAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        OdfSaveOptions options = CreateEncryptedSaveOptions(password, algorithm);
        return SaveAsync(options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously saves the package to the specified stream with password encryption.
    /// 非同步以密碼加密並將封裝儲存至指定資料流。
    /// </summary>
    /// <param name="destinationStream">The destination stream. / 目的地資料流。</param>
    /// <param name="password">The password used to encrypt the package. / 用於加密封裝的密碼。</param>
    /// <param name="algorithm">The encryption algorithm to use. / 要使用的加密演算法。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous save operation. / 代表非同步儲存作業的工作。</returns>
    public Task SaveEncryptedAsync(
        Stream destinationStream,
        string password,
        OdfEncryptionAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (destinationStream is null)
            throw new ArgumentNullException(nameof(destinationStream));

        OdfSaveOptions options = CreateEncryptedSaveOptions(password, algorithm);
        return SaveToStreamAsync(destinationStream, options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously loads and decrypts an ODF package from the specified path.
    /// 非同步從指定路徑載入並解密 ODF 封裝。
    /// </summary>
    /// <param name="path">The ODF package path. / ODF 封裝路徑。</param>
    /// <param name="password">The password used to decrypt the package. / 用於解密封裝的密碼。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the decrypted package. / 工作結果為已解密封裝。</returns>
    public static Task<OdfPackage> LoadEncryptedAsync(
        string path,
        string password,
        CancellationToken cancellationToken)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        return OpenAsync(path, CreateEncryptedLoadOptions(password), cancellationToken);
    }

    /// <summary>
    /// Asynchronously loads and decrypts an ODF package from the specified stream.
    /// 非同步從指定資料流載入並解密 ODF 封裝。
    /// </summary>
    /// <param name="stream">The stream containing the ODF package. / 包含 ODF 封裝的資料流。</param>
    /// <param name="password">The password used to decrypt the package. / 用於解密封裝的密碼。</param>
    /// <param name="leaveOpen">A value indicating whether disposal leaves the stream open. / 指出處置封裝時是否保持資料流開啟。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the decrypted package. / 工作結果為已解密封裝。</returns>
    public static Task<OdfPackage> LoadEncryptedAsync(
        Stream stream,
        string password,
        bool leaveOpen,
        CancellationToken cancellationToken)
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
