namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    // RDF metadata 載入／儲存已遷移至 OdfPackageRdfMetadataEngine。

    internal void SaveManifestToEntries() => OdfPackageManifestWriter.WriteManifest(SaveCollaborators);
}
