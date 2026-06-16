using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Xml;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 <c>META-INF/manifest.xml</c> 解析器（內部協作者）。
/// </summary>
internal static class OdfManifestLoader
{
    /// <summary>
    /// 從 manifest 串流解析清單、加密中繼資料與版本資訊，並寫入指定的載入內容。
    /// </summary>
    /// <param name="stream">manifest.xml 內容串流</param>
    /// <param name="context">載入內容（含封裝項目字典與輸出集合）</param>
    internal static void Parse(Stream stream, OdfManifestLoadContext context)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = context.LoadOptions.MaxXmlCharactersInDocument > 0
                ? context.LoadOptions.MaxXmlCharactersInDocument
                : 0
        };

        OdfPackageEntry? currentEntry = null;
        OdfEncryptionInfo? currentEncryptionInfo = null;

        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                ParseElement(reader, context, ref currentEntry, ref currentEncryptionInfo);
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                ParseEndElement(reader, ref currentEntry, ref currentEncryptionInfo);
            }
        }
    }

    /// <summary>
    /// 判斷 manifest <c>full-path</c> 是否為安全、可正規化的相對路徑。
    /// </summary>
    internal static bool IsSafeManifestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("\\") ||
            path.Contains(":") ||
            path.Contains("//"))
        {
            return false;
        }

        string normalized = path.EndsWith("/", StringComparison.Ordinal)
            ? path.Substring(0, path.Length - 1)
            : path;

        if (normalized.Length == 0)
            return false;

        string[] parts = normalized.Split('/');
        foreach (string part in parts)
        {
            if (part.Length == 0 || part is "." or "..")
                return false;
        }

        return true;
    }

    private static void ParseElement(
        XmlReader reader,
        OdfManifestLoadContext context,
        ref OdfPackageEntry? currentEntry,
        ref OdfEncryptionInfo? currentEncryptionInfo)
    {
        if (reader.LocalName == "manifest" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            string? version = reader.GetAttribute("version", OdfNamespaces.Manifest) ?? reader.GetAttribute("version");
            context.RootInfo = new OdfManifestRootInfo(reader.NamespaceURI, reader.LocalName, version);
            if (version is not null)
            {
                context.DetectedVersion = version switch
                {
                    "1.0" => OdfVersion.Odf10,
                    "1.1" => OdfVersion.Odf11,
                    "1.2" => OdfVersion.Odf12,
                    "1.3" => OdfVersion.Odf13,
                    "1.4" => OdfVersion.Odf14,
                    _ => OdfVersion.Odf14
                };
            }

            return;
        }

        if (reader.LocalName == "file-entry" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            ParseFileEntry(reader, context, ref currentEntry, ref currentEncryptionInfo);
            return;
        }

        if (reader.LocalName == "encryption-data" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEntry is not null)
        {
            currentEncryptionInfo = new OdfEncryptionInfo();
            string? checksumType = reader.GetAttribute("checksum-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("checksum-type");
            string? checksumStr = reader.GetAttribute("checksum", OdfNamespaces.Manifest) ?? reader.GetAttribute("checksum");

            if (!string.IsNullOrEmpty(checksumType))
                currentEncryptionInfo.ChecksumType = checksumType;
            if (!string.IsNullOrEmpty(checksumStr))
                currentEncryptionInfo.Checksum = Convert.FromBase64String(checksumStr);

            currentEntry.EncryptionInfo = currentEncryptionInfo;

            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                if (reader.LocalName is not "checksum-type" and not "checksum")
                    currentEncryptionInfo.ExtensionProperties[reader.LocalName] = reader.Value;
            }

            reader.MoveToElement();
            return;
        }

        if (currentEncryptionInfo is null)
            return;

        if (reader.LocalName == "algorithm" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            string? algorithmName = reader.GetAttribute("algorithm-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("algorithm-name");
            string? ivStr = reader.GetAttribute("initialisation-vector", OdfNamespaces.Manifest) ?? reader.GetAttribute("initialisation-vector");

            if (!string.IsNullOrEmpty(algorithmName))
                currentEncryptionInfo.AlgorithmName = algorithmName;
            if (!string.IsNullOrEmpty(ivStr))
                currentEncryptionInfo.InitialisationVector = Convert.FromBase64String(ivStr);
            return;
        }

        if (reader.LocalName == "key-derivation" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            string? derivationName = reader.GetAttribute("key-derivation-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-derivation-name");
            string? keySizeStr = reader.GetAttribute("key-size", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-size");
            string? iterationCountStr = reader.GetAttribute("iteration-count", OdfNamespaces.Manifest) ?? reader.GetAttribute("iteration-count");
            string? saltStr = reader.GetAttribute("salt", OdfNamespaces.Manifest) ?? reader.GetAttribute("salt");

            if (!string.IsNullOrEmpty(derivationName))
                currentEncryptionInfo.KeyDerivationName = derivationName;
            if (int.TryParse(keySizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int keySize))
                currentEncryptionInfo.KeySize = keySize;
            if (int.TryParse(iterationCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iterationCount))
            {
                if (iterationCount > 50000)
                {
                    throw new CryptographicException(
                        $"PBKDF2 iteration count {iterationCount} exceeds the maximum limit of 50000.");
                }

                currentEncryptionInfo.IterationCount = iterationCount;
            }

            if (!string.IsNullOrEmpty(saltStr))
                currentEncryptionInfo.Salt = Convert.FromBase64String(saltStr);

            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                if (reader.LocalName is not "key-derivation-name" and not "key-size" and not "iteration-count" and not "salt")
                    currentEncryptionInfo.ExtensionProperties[reader.LocalName] = reader.Value;
            }

            reader.MoveToElement();
            return;
        }

        if (reader.LocalName == "encrypted-key" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            var encryptedKey = new OdfOpenPgpEncryptedKeyInfo
            {
                KeyId = reader.GetAttribute("key-id", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-id") ?? string.Empty,
                Recipient = reader.GetAttribute("recipient", OdfNamespaces.Manifest) ?? reader.GetAttribute("recipient"),
                AlgorithmName = reader.GetAttribute("algorithm-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("algorithm-name") ?? string.Empty
            };

            for (int i = 0; i < reader.AttributeCount; i++)
            {
                reader.MoveToAttribute(i);
                if (reader.NamespaceURI == OdfNamespaces.Manifest &&
                    reader.LocalName is not "key-id" and not "recipient" and not "algorithm-name")
                {
                    encryptedKey.ExtensionProperties[reader.LocalName] = reader.Value;
                }
            }

            reader.MoveToElement();

            if (reader.IsEmptyElement)
            {
                currentEncryptionInfo.OpenPgpEncryptedKeys.Add(encryptedKey);
                return;
            }

            string keyPacket = reader.ReadElementContentAsString();
            if (!string.IsNullOrWhiteSpace(keyPacket))
                encryptedKey.KeyPacket = Convert.FromBase64String(keyPacket.Trim());
            currentEncryptionInfo.OpenPgpEncryptedKeys.Add(encryptedKey);
            return;
        }

        if (reader.LocalName == "start-key-generation" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            string? startKeyGenName = reader.GetAttribute("start-key-generation-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("start-key-generation-name");
            string? startKeySizeStr = reader.GetAttribute("key-size", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-size");

            if (!string.IsNullOrEmpty(startKeyGenName))
                currentEncryptionInfo.StartKeyGenerationName = startKeyGenName;
            if (int.TryParse(startKeySizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int startKeySize))
                currentEncryptionInfo.StartKeySize = startKeySize;
        }
    }

    private static void ParseFileEntry(
        XmlReader reader,
        OdfManifestLoadContext context,
        ref OdfPackageEntry? currentEntry,
        ref OdfEncryptionInfo? currentEncryptionInfo)
    {
        string? path = reader.GetAttribute("full-path", OdfNamespaces.Manifest) ?? reader.GetAttribute("full-path");
        string? mediaType = reader.GetAttribute("media-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("media-type");

        var issue = new OdfManifestFileEntryIssue();
        bool hasIssue = false;

        if (path is null)
        {
            issue.MissingFullPath = true;
            hasIssue = true;
        }
        else
        {
            issue.FullPath = path;
            if (path != "/" && !IsSafeManifestPath(path))
            {
                issue.InvalidFullPath = true;
                hasIssue = true;
            }
        }

        if (mediaType is null)
        {
            issue.MissingMediaType = true;
            hasIssue = true;
        }

        if (hasIssue)
            context.FileEntryIssues.Add(issue);

        if (path is not null)
        {
            string normPath = NormalizeManifestPath(path);

            if (context.Manifest.ContainsKey(normPath))
                context.DuplicatePaths.Add(normPath);
            if (mediaType is not null)
                context.Manifest[normPath] = mediaType;

            if (normPath != "/" && context.Entries.TryGetValue(normPath, out OdfPackageEntry? entry))
                currentEntry = entry;
            else
                currentEntry = null;
        }
        else
        {
            currentEntry = null;
        }

        currentEncryptionInfo = null;
    }

    private static string NormalizeManifestPath(string path)
    {
        if (path == "/")
            return "/";

        if (!IsSafeManifestPath(path))
            return path;

        try
        {
            return OdfPackage.SanitizeEntryName(path);
        }
        catch (SecurityException)
        {
            return path;
        }
    }

    private static void ParseEndElement(
        XmlReader reader,
        ref OdfPackageEntry? currentEntry,
        ref OdfEncryptionInfo? currentEncryptionInfo)
    {
        if (reader.LocalName == "file-entry" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            currentEntry = null;
            currentEncryptionInfo = null;
        }
        else if (reader.LocalName == "encryption-data" && reader.NamespaceURI == OdfNamespaces.Manifest)
        {
            currentEncryptionInfo = null;
        }
    }
}

/// <summary>
/// manifest 載入時的可變狀態容器，供 <see cref="OdfManifestLoader"/> 寫入解析結果。
/// </summary>
internal sealed class OdfManifestLoadContext
{
    /// <summary>
    /// 封裝內的項目字典（可變，用於附加加密中繼資料）。
    /// </summary>
    public Dictionary<string, OdfPackageEntry> Entries { get; set; } = null!;

    /// <summary>
    /// 載入選項。
    /// </summary>
    public OdfLoadOptions LoadOptions { get; set; } = null!;

    /// <summary>
    /// 輸出：full-path 至 media-type 的對應。
    /// </summary>
    public Dictionary<string, string> Manifest { get; set; } = null!;

    /// <summary>
    /// 輸出：重複的 manifest 路徑。
    /// </summary>
    public List<string> DuplicatePaths { get; set; } = null!;

    /// <summary>
    /// 輸出：file-entry 結構問題清單。
    /// </summary>
    public List<OdfManifestFileEntryIssue> FileEntryIssues { get; set; } = null!;

    /// <summary>
    /// 輸出：manifest 根節點資訊。
    /// </summary>
    public OdfManifestRootInfo? RootInfo { get; set; }

    /// <summary>
    /// 輸出：自 manifest 屬性推斷的 ODF 版本（僅在存在 version 屬性時設定）。
    /// </summary>
    public OdfVersion? DetectedVersion { get; set; }
}
