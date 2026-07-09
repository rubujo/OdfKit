using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;
/// <summary>
/// Adds asynchronous encrypted save and load helpers for ODF documents.
/// 提供 ODF 文件的非同步加密儲存與載入輔助方法。
/// </summary>

public abstract partial class OdfDocument
{
    /// <summary>
    /// Asynchronously saves the document to its original destination with password encryption.
    /// 非同步以密碼加密並將文件儲存至原始目的地。
    /// </summary>
    /// <param name="password">The password used to encrypt the document. / 用於加密文件的密碼。</param>
    /// <param name="algorithm">The encryption algorithm to use. / 要使用的加密演算法。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous save operation. / 代表非同步儲存作業的工作。</returns>
    public Task SaveEncryptedAsync(
        string password,
        OdfEncryptionAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        OdfSaveOptions options = OdfPackage.CreateEncryptedSaveOptions(password, algorithm);
        return SaveAsync(options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously saves the document to the specified path with password encryption.
    /// 非同步以密碼加密並將文件儲存至指定路徑。
    /// </summary>
    /// <param name="path">The destination file path. / 目的地檔案路徑。</param>
    /// <param name="password">The password used to encrypt the document. / 用於加密文件的密碼。</param>
    /// <param name="algorithm">The encryption algorithm to use. / 要使用的加密演算法。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous save operation. / 代表非同步儲存作業的工作。</returns>
    public Task SaveEncryptedAsync(
        string path,
        string password,
        OdfEncryptionAlgorithm algorithm,
        CancellationToken cancellationToken)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        OdfSaveOptions options = OdfPackage.CreateEncryptedSaveOptions(password, algorithm);
        return SaveAsync(path, options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously saves the document to the specified stream with password encryption.
    /// 非同步以密碼加密並將文件儲存至指定資料流。
    /// </summary>
    /// <param name="destinationStream">The destination stream. / 目的地資料流。</param>
    /// <param name="password">The password used to encrypt the document. / 用於加密文件的密碼。</param>
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

        OdfSaveOptions options = OdfPackage.CreateEncryptedSaveOptions(password, algorithm);
        return SaveToStreamAsync(destinationStream, options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously loads and decrypts an ODF document from the specified path.
    /// 非同步從指定路徑載入並解密 ODF 文件。
    /// </summary>
    /// <param name="path">The ODF document path. / ODF 文件路徑。</param>
    /// <param name="password">The password used to decrypt the document. / 用於解密文件的密碼。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the decrypted document. / 工作結果為已解密文件。</returns>
    public static Task<OdfDocument> LoadEncryptedAsync(
        string path,
        string password,
        CancellationToken cancellationToken)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        return LoadAsync(path, OdfPackage.CreateEncryptedLoadOptions(password), cancellationToken);
    }

    /// <summary>
    /// Asynchronously loads and decrypts an ODF document from the specified stream.
    /// 非同步從指定資料流載入並解密 ODF 文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODF document. / 包含 ODF 文件的資料流。</param>
    /// <param name="password">The password used to decrypt the document. / 用於解密文件的密碼。</param>
    /// <param name="fileName">The optional file name used to assist format detection. / 用於輔助格式偵測的選用檔案名稱。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the decrypted document. / 工作結果為已解密文件。</returns>
    public static Task<OdfDocument> LoadEncryptedAsync(
        Stream stream,
        string password,
        string? fileName,
        CancellationToken cancellationToken)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        return LoadAsync(stream, OdfPackage.CreateEncryptedLoadOptions(password), fileName, cancellationToken);
    }
}
