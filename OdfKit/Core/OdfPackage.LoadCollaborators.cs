using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using OdfKit.Compliance;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    /// <summary>
    /// 供 <see cref="OdfPackageLoader"/> 使用的內部載入協作存取器。
    /// </summary>
    internal OdfPackageLoadCollaborators LoadCollaborators => new(this);

    /// <summary>
    /// 封裝載入管線的內部協作存取器。
    /// </summary>
    internal readonly struct OdfPackageLoadCollaborators
    {
        private readonly OdfPackage _package;

        internal OdfPackageLoadCollaborators(OdfPackage package) => _package = package;

        internal Stream? UnderlyingStream
        {
            get => _package._underlyingStream;
            set => _package._underlyingStream = value;
        }

        internal bool LeaveOpen => _package._leaveOpen;

        internal OdfLoadOptions LoadOptions => _package._loadOptions;

        internal bool IsFlatXml
        {
            get => _package._isFlatXml;
            set => _package._isFlatXml = value;
        }

        internal ZipArchive? Archive
        {
            get => _package._archive;
            set => _package._archive = value;
        }

        internal Dictionary<string, OdfPackageEntry> Entries => _package._entries;

        internal List<string> EntryOrder => _package._entryOrder;

        internal List<string> DuplicateEntryNames => _package._duplicateEntryNames;

        internal string? MimeType
        {
            get => _package._mimetype;
            set => _package._mimetype = value;
        }

        internal void LoadManifest() => _package.LoadManifest();

        internal void LoadRdfMetadata() => _package.LoadRdfMetadata();

        internal Dictionary<string, string> Manifest => _package._manifest;

        internal OdfSaveOptions SaveOptions => _package._saveOptions;

        internal OdfVersion Version
        {
            get => _package._version;
            set => _package._version = value;
        }

        internal void WriteVirtualEntry(string name, byte[] content, string mediaType) =>
            _package.WriteVirtualEntry(name, content, mediaType);

        internal void InitializeFlatXml(byte[] signature, int signatureLength) =>
            OdfPackageFlatXmlLoader.Initialize(this, signature, signatureLength);

        internal static int ReadStreamPrefix(Stream stream, byte[] buffer, int offset, int count)
            => ReadAll(stream, buffer, offset, count);
    }
}
