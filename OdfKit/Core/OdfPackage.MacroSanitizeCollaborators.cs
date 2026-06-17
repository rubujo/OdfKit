using System.Collections.Generic;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 供 <see cref="OdfPackageMacroSanitizer"/> 使用的內部巨集淨化協作存取器。
    /// </summary>
    internal OdfPackageMacroSanitizeCollaborators MacroSanitizeCollaborators => new(this);

    /// <summary>
    /// 封裝巨集淨化管線的內部協作存取器。
    /// </summary>
    internal readonly struct OdfPackageMacroSanitizeCollaborators
    {
        private readonly OdfPackage _package;

        internal OdfPackageMacroSanitizeCollaborators(OdfPackage package) => _package = package;

        internal Dictionary<string, OdfPackageEntry> Entries => _package._entries;

        internal Dictionary<string, string> Manifest => _package._manifest;

        internal OdfLoadOptions LoadOptions => _package._loadOptions;

        internal OdfSaveOptions SaveOptions => _package._saveOptions;

        internal void RemoveOutdatedSignatures()
            => OdfPackageSignaturePurgeEngine.RemoveOutdatedSignatures(Entries, Manifest);
    }
}
