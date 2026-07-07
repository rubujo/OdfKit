namespace OdfKit.Core;
/// <summary>
/// Adds metadata writing helpers used by package save operations.
/// 提供封裝儲存作業使用的中繼資料寫入輔助方法。
/// </summary>

public sealed partial class OdfPackage
{
    // RDF metadata 載入／儲存已遷移至 OdfPackageRdfMetadataEngine。

    internal void SaveManifestToEntries() => OdfPackageManifestWriter.WriteManifest(SaveCollaborators);
}
