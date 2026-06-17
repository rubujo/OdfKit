using System.Collections.Generic;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 供封裝項目讀寫引擎使用的內部協作存取器。
    /// </summary>
    internal OdfPackageEntryCollaborators EntryCollaborators => new(this);

    /// <summary>
    /// 封裝項目讀寫管線的內部協作存取器。
    /// </summary>
    internal readonly struct OdfPackageEntryCollaborators
    {
        private readonly OdfPackage _package;

        internal OdfPackageEntryCollaborators(OdfPackage package) => _package = package;

        internal Dictionary<string, OdfPackageEntry> Entries => _package._entries;

        internal Dictionary<string, string> Manifest => _package._manifest;

        internal List<string> EntryOrder => _package._entryOrder;

        internal void SetMimeTypeValue(string mimetype) => _package._mimetype = mimetype;

        internal void RemoveOutdatedSignatures() => _package.RemoveOutdatedSignatures();
    }
}
