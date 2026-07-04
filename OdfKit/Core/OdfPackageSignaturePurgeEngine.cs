using System.Collections.Generic;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝失效數位簽章清除引擎（內部協作者）。
/// </summary>
internal static class OdfPackageSignaturePurgeEngine
{
    /// <summary>
    /// 封裝內容變更後移除已失效的 documentsignatures.xml。
    /// </summary>
    internal static void RemoveOutdatedSignatures(
        Dictionary<string, OdfPackageEntry> entries,
        Dictionary<string, string> manifest)
    {
        if (!entries.ContainsKey(OdfSignerConstants.SignaturePath))
            return;

        entries.Remove(OdfSignerConstants.SignaturePath);
        manifest.Remove(OdfSignerConstants.SignaturePath);
        OdfKitDiagnostics.Info("Outdated digital signatures removed due to package edit.");
    }
}
