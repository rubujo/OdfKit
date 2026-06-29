using System;
using System.IO;
using OdfKit.Core;

namespace OdfKit.Compliance;

/// <summary>
/// Provides a unified public entry point for ODF document validation.
/// 提供統一的公開 ODF 文件驗證入口。
/// </summary>
public static class OdfValidator
{
    /// <summary>
    /// Validates the ODF document at the specified path.
    /// 驗證指定路徑的 ODF 文件。
    /// </summary>
    /// <param name="path">The ODF document path to validate. / 要驗證的 ODF 文件路徑。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(string path)
    {
        return Validate(path, OdfValidationOptions.Default);
    }

    /// <summary>
    /// Validates the ODF document at the specified path using the specified compliance profile.
    /// 使用指定設定檔驗證指定路徑的 ODF 文件。
    /// </summary>
    /// <param name="path">The ODF document path to validate. / 要驗證的 ODF 文件路徑。</param>
    /// <param name="profile">The compliance profile. / 相容性設定檔。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(string path, OdfComplianceProfile? profile)
    {
        return Validate(path, new OdfValidationOptions { Profile = profile });
    }

    /// <summary>
    /// Validates the ODF document at the specified path using the specified options.
    /// 使用指定選項驗證指定路徑的 ODF 文件。
    /// </summary>
    /// <param name="path">The ODF document path to validate. / 要驗證的 ODF 文件路徑。</param>
    /// <param name="options">The validation options. / 驗證選項。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(string path, OdfValidationOptions? options)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfValidationOptions.Default;
        string fileName = options.FileName ?? path;
        using FileStream stream = File.OpenRead(path);
        return Validate(stream, CopyOptions(options, fileName));
    }

    /// <summary>
    /// Validates the ODF document in the specified stream.
    /// 驗證指定串流中的 ODF 文件。
    /// </summary>
    /// <param name="stream">The ODF document stream to validate. / 要驗證的 ODF 文件串流。</param>
    /// <param name="fileName">The file name used to assist format detection. / 用於輔助格式偵測的檔案名稱。</param>
    /// <param name="profile">The compliance profile. / 相容性設定檔。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(
        Stream stream,
        string? fileName = null,
        OdfComplianceProfile? profile = null)
    {
        return Validate(stream, new OdfValidationOptions { FileName = fileName, Profile = profile });
    }

    /// <summary>
    /// Validates the ODF document in the specified stream using the specified options.
    /// 使用指定選項驗證指定串流中的 ODF 文件。
    /// </summary>
    /// <param name="stream">The ODF document stream to validate. / 要驗證的 ODF 文件串流。</param>
    /// <param name="options">The validation options. / 驗證選項。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(Stream stream, OdfValidationOptions? options)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        options ??= OdfValidationOptions.Default;
        if (IsFlatXmlFileName(options.FileName))
        {
            return OdfFlatDocumentValidator.Validate(stream, options.FileName, options.Profile);
        }

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true, options.LoadOptions);
        return package.IsFlatXml
            ? OdfFlatDocumentValidator.Validate(Rewind(stream), options.FileName, options.Profile)
            : OdfPackageValidator.Validate(package, options.Profile, options.FileName);
    }

    /// <summary>
    /// Validates an already-open ODF package.
    /// 驗證已開啟的 ODF 封裝。
    /// </summary>
    /// <param name="package">The ODF package to validate. / 要驗證的 ODF 封裝。</param>
    /// <param name="profile">The compliance profile. / 相容性設定檔。</param>
    /// <param name="fileName">The file name used to assist format detection. / 用於輔助格式偵測的檔案名稱。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(
        OdfPackage package,
        OdfComplianceProfile? profile = null,
        string? fileName = null)
    {
        return Validate(package, new OdfValidationOptions { FileName = fileName, Profile = profile });
    }

    /// <summary>
    /// Validates an already-open ODF package using the specified options.
    /// 使用指定選項驗證已開啟的 ODF 封裝。
    /// </summary>
    /// <param name="package">The ODF package to validate. / 要驗證的 ODF 封裝。</param>
    /// <param name="options">The validation options. / 驗證選項。</param>
    /// <returns>The validation report. / 驗證結果報告。</returns>
    public static OdfValidationReport Validate(OdfPackage package, OdfValidationOptions? options)
    {
        if (package is null)
            throw new ArgumentNullException(nameof(package));

        options ??= OdfValidationOptions.Default;
        return OdfPackageValidator.Validate(package, options.Profile, options.FileName);
    }

    private static bool IsFlatXmlFileName(string? fileName)
    {
        return OdfDocumentKindDetector.TryGetFormatByFileName(fileName, out OdfFormatInfo? format) &&
            format!.IsFlatXml;
    }

    private static Stream Rewind(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return stream;
    }

    private static OdfValidationOptions CopyOptions(OdfValidationOptions options, string? fileName)
    {
        return new OdfValidationOptions
        {
            FileName = fileName,
            LoadOptions = options.LoadOptions,
            Profile = options.Profile
        };
    }
}
