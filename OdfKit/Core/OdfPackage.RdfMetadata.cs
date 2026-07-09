namespace OdfKit.Core;
/// <summary>
/// Adds RDF metadata synchronization helpers for ODF packages.
/// 提供 ODF 封裝的 RDF 中繼資料同步輔助方法。
/// </summary>

public sealed partial class OdfPackage
{
    /// <summary>
    /// Performs sync rdf metadata with entries.
    /// 將 <see cref="RdfMetadata"/> 中的 <c>pkg:hasPart</c> 與 <c>pkg:mimeType</c> 與目前封裝專案同步。
    /// </summary>
    /// <returns>新增或更新的 triple 數量</returns>
    public int SyncRdfMetadataWithEntries() =>
        RdfMetadata.SyncWithPackageEntries(_entries.Keys, _manifest);
}
