using System;

namespace OdfKit.Core
{
    public class OdfManifestFileEntryIssue
    {
        public string? FullPath { get; set; }
        public bool MissingFullPath { get; set; }
        public bool MissingMediaType { get; set; }
        public bool InvalidFullPath { get; set; }
    }

    public class OdfManifestRootInfo
    {
        public string NamespaceUri { get; }
        public string LocalName { get; }
        public string? Version { get; }

        public OdfManifestRootInfo(string namespaceUri, string localName, string? version)
        {
            NamespaceUri = namespaceUri;
            LocalName = localName;
            Version = version;
        }
    }
}
