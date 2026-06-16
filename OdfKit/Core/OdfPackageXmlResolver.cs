using System;
using System.IO;
using System.Xml;
using OdfKit.Core;

namespace OdfKit.Core;

internal sealed class OdfPackageXmlResolver(OdfPackage package) : XmlResolver
{
    private readonly OdfPackage _package = package;

    public override System.Net.ICredentials Credentials
    {
        set { }
    }

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (absoluteUri == null)
            throw new ArgumentNullException(nameof(absoluteUri));

        if (string.Equals(absoluteUri.Scheme, "odf", StringComparison.OrdinalIgnoreCase))
        {
            string path = absoluteUri.AbsolutePath.TrimStart('/');
            if (_package.HasEntry(path))
            {
                return _package.GetEntryStream(path);
            }
        }
        else if (string.Equals(absoluteUri.Scheme, "file", StringComparison.OrdinalIgnoreCase))
        {
            string localPath = absoluteUri.LocalPath;

            // 嘗試相對於目前工作目錄解析
            string currentDir = Directory.GetCurrentDirectory();
            string? relativePath = GetRelativePath(currentDir, localPath);
            if (relativePath != null && _package.HasEntry(relativePath))
            {
                return _package.GetEntryStream(relativePath);
            }

            // 嘗試相對於 AppDomain 基底目錄解析
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                relativePath = GetRelativePath(baseDir, localPath);
                if (relativePath != null && _package.HasEntry(relativePath))
                {
                    return _package.GetEntryStream(relativePath);
                }
            }

            // 後援：檢查僅檔名是否存在於封裝中
            string fileName = Path.GetFileName(localPath);
            if (_package.HasEntry(fileName))
            {
                return _package.GetEntryStream(fileName);
            }
        }

        return null;
    }

    public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
    {
        if (baseUri == null)
        {
            return new Uri($"odf://package/{relativeUri?.TrimStart('/')}");
        }
        return new Uri(baseUri, relativeUri);
    }

    private static string? GetRelativePath(string baseDir, string fullPath)
    {
        try
        {
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !baseDir.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                baseDir += Path.DirectorySeparatorChar;
            }

            Uri baseUri = new Uri(baseDir);
            Uri fullUri = new Uri(fullPath);
            if (baseUri.Scheme != fullUri.Scheme)
                return null;

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            if (relativeUri.IsAbsoluteUri)
                return null;

            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('\\', '/');
        }
        catch
        {
            return null;
        }
    }
}
