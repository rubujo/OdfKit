using System.IO;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void LoadManifest()
    {
        if (!_entries.TryGetValue("META-INF/manifest.xml", out OdfPackageEntry? manifestEntry))
        {
            if (_loadOptions.ValidateMimeType)
            {
                throw new InvalidDataException("Invalid ODF package: 'META-INF/manifest.xml' is missing.");
            }

            return;
        }

        var context = new OdfManifestLoadContext
        {
            Entries = _entries,
            LoadOptions = _loadOptions,
            Manifest = _manifest,
            DuplicatePaths = _duplicateManifestPaths,
            FileEntryIssues = _manifestFileEntryIssues
        };

        using Stream stream = manifestEntry.OpenReader();
        OdfManifestLoader.Parse(stream, context);

        _manifestRootInfo = context.RootInfo;
        if (context.DetectedVersion.HasValue)
            _version = context.DetectedVersion.Value;
    }

    #endregion
}
