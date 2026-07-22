using OdfKit.Core;

namespace OdfKit.Extensions.Scripting;

/// <summary>
/// Provides opt-in scripting management entry points for ODF documents and packages.
/// 提供 ODF 文件與封裝容器的選用指令碼管理進入點。
/// </summary>
public static class OdfScriptingExtensions
{
    /// <summary>
    /// Creates a scripting manager for an ODF package.
    /// 為 ODF 封裝容器建立指令碼管理器。
    /// </summary>
    /// <param name="package">The package to manage. / 要管理的 ODF 封裝容器。</param>
    /// <returns>A scripting manager that does not execute script content. / 不會執行指令碼內容的管理器。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="package"/> is <see langword="null"/>. / 當 <paramref name="package"/> 為 <see langword="null"/> 時擲出。</exception>
    public static OdfScriptManager Scripting(this OdfPackage package)
    {
        if (package is null)
        {
            throw new ArgumentNullException(
                nameof(package),
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(package)));
        }

        return new OdfScriptManager(package);
    }

    /// <summary>
    /// Creates a scripting manager for an ODF document.
    /// 為 ODF 文件建立指令碼管理器。
    /// </summary>
    /// <param name="document">The document to manage. / 要管理的 ODF 文件。</param>
    /// <returns>A scripting manager that does not execute script content. / 不會執行指令碼內容的管理器。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>. / 當 <paramref name="document"/> 為 <see langword="null"/> 時擲出。</exception>
    public static OdfScriptManager Scripting(this OdfDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(
                nameof(document),
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfScriptManager_ArgumentNull", nameof(document)));
        }

        bool supportsPackageScripts = document.DocumentKind is not (
            OdfKit.Compliance.OdfDocumentKind.FlatText or
            OdfKit.Compliance.OdfDocumentKind.FlatSpreadsheet or
            OdfKit.Compliance.OdfDocumentKind.FlatPresentation or
            OdfKit.Compliance.OdfDocumentKind.FlatGraphics or
            OdfKit.Compliance.OdfDocumentKind.FlatChart or
            OdfKit.Compliance.OdfDocumentKind.FlatFormula or
            OdfKit.Compliance.OdfDocumentKind.FlatImage);
        return new OdfScriptManager(document.Package, supportsPackageScripts);
    }
}
