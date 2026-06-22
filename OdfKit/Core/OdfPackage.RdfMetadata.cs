namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 將 <see cref="RdfMetadata"/> 中的 <c>pkg:hasPart</c> 與 <c>pkg:mimeType</c> 與目前封裝專案同步。
    /// </summary>
    /// <returns>新增或更新的 triple 數量</returns>
    public int SyncRdfMetadataWithEntries() =>
        RdfMetadata.SyncWithPackageEntries(_entries.Keys, _manifest);
}
