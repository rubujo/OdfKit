namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void WriteVirtualEntry(string name, byte[] content, string mediaType)
    {
        name = SanitizeEntryName(name);
        _entries[name] = new OdfPackageEntry(name, content);
        _manifest[name] = mediaType;
        if (!_entryOrder.Contains(name))
            _entryOrder.Add(name);
    }

    #endregion
}
