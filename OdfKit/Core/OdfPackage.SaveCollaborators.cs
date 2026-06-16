using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 供 <see cref="OdfPackageSaver"/> 使用的內部儲存協作存取器。
    /// </summary>
    internal OdfPackageSaveCollaborators SaveCollaborators => new(this);

    /// <summary>
    /// 封裝儲存管線的內部協作存取器。
    /// </summary>
    internal readonly struct OdfPackageSaveCollaborators
    {
        private readonly OdfPackage _package;

        internal OdfPackageSaveCollaborators(OdfPackage package) => _package = package;

        internal OdfSaveOptions SaveOptions => _package._saveOptions;

        internal OdfLoadOptions LoadOptions => _package._loadOptions;

        internal bool IsFlatXml => _package._isFlatXml;

        internal Stream? UnderlyingStream => _package._underlyingStream;

        internal Dictionary<string, OdfPackageEntry> Entries => _package._entries;

        internal Dictionary<string, string> Manifest => _package._manifest;

        internal string? MimeType => _package._mimetype;

        internal bool HasActiveEncryption =>
            _package._saveOptions.Password != null || _package._saveOptions.CryptographyProvider != null;

        internal void ProcessSaveHooks() => _package.ProcessSaveHooks();

        internal void SaveRdfMetadata() => _package.SaveRdfMetadataToEntries();

        internal void SaveManifest() => _package.SaveManifestToEntries();

        internal void WriteToArchive(Stream target) => OdfPackageArchiveWriter.WriteToArchive(this, target);

        internal long EstimateArchiveSize()
        {
            long estimated = 0;
            foreach (OdfPackageEntry entry in _package._entries.Values)
                estimated += entry.GetEstimatedSize();
            return estimated;
        }
    }
}
