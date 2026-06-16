using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Initialization & Loading

    private void LoadManifest()
    {
        if (!_entries.TryGetValue("META-INF/manifest.xml", out var manifestEntry))
        {
            if (_loadOptions.ValidateMimeType)
            {
                throw new InvalidDataException("Invalid ODF package: 'META-INF/manifest.xml' is missing.");
            }
            return;
        }

        using var stream = manifestEntry.OpenReader();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = _loadOptions.MaxXmlCharactersInDocument > 0 ? _loadOptions.MaxXmlCharactersInDocument : 0
        };

        OdfPackageEntry? currentEntry = null;
        OdfEncryptionInfo? currentEncryptionInfo = null;

        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "manifest" && reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    string? version = reader.GetAttribute("version", OdfNamespaces.Manifest) ?? reader.GetAttribute("version");
                    _manifestRootInfo = new OdfManifestRootInfo(reader.NamespaceURI, reader.LocalName, version);
                    if (version != null)
                    {
                        _version = version switch
                        {
                            "1.0" => OdfVersion.Odf10,
                            "1.1" => OdfVersion.Odf11,
                            "1.2" => OdfVersion.Odf12,
                            "1.3" => OdfVersion.Odf13,
                            "1.4" => OdfVersion.Odf14,
                            _ => OdfVersion.Odf14
                        };
                    }
                }
                else if (reader.LocalName == "file-entry" && reader.NamespaceURI == OdfNamespaces.Manifest)
                {
                    string? path = reader.GetAttribute("full-path", OdfNamespaces.Manifest) ?? reader.GetAttribute("full-path");
                    string? mediaType = reader.GetAttribute("media-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("media-type");

                    var issue = new OdfManifestFileEntryIssue();
                    bool hasIssue = false;

                    if (path == null)
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

                    if (mediaType == null)
                    {
                        issue.MissingMediaType = true;
                        hasIssue = true;
                    }

                    if (hasIssue)
                    {
                        _manifestFileEntryIssues.Add(issue);
                    }

                    if (path != null)
                    {
                        string normPath;
                        if (path == "/")
                        {
                            normPath = "/";
                        }
                        else if (!IsSafeManifestPath(path))
                        {
                            normPath = path;
                        }
                        else
                        {
                            try
                            {
                                normPath = SanitizeEntryName(path);
                            }
                            catch (SecurityException)
                            {
                                normPath = path;
                            }
                        }

                        if (_manifest.ContainsKey(normPath))
                        {
                            _duplicateManifestPaths.Add(normPath);
                        }
                        if (mediaType != null)
                        {
                            _manifest[normPath] = mediaType;
                        }

                        if (normPath != "/" && _entries.TryGetValue(normPath, out var entry))
                        {
                            currentEntry = entry;
                        }
                        else
                        {
                            currentEntry = null;
                        }
                    }
                    else
                    {
                        currentEntry = null;
                    }
                    currentEncryptionInfo = null;
                }
                else if (reader.LocalName == "encryption-data" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEntry != null)
                {
                    currentEncryptionInfo = new OdfEncryptionInfo();
                    string? checksumType = reader.GetAttribute("checksum-type", OdfNamespaces.Manifest) ?? reader.GetAttribute("checksum-type");
                    string? checksumStr = reader.GetAttribute("checksum", OdfNamespaces.Manifest) ?? reader.GetAttribute("checksum");

                    if (!string.IsNullOrEmpty(checksumType))
                    {
                        currentEncryptionInfo.ChecksumType = checksumType;
                    }
                    if (!string.IsNullOrEmpty(checksumStr))
                    {
                        currentEncryptionInfo.Checksum = Convert.FromBase64String(checksumStr);
                    }

                    currentEntry.EncryptionInfo = currentEncryptionInfo;

                    // 將其他屬性載入 ExtensionProperties
                    for (int i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);
                        if (reader.LocalName != "checksum-type" && reader.LocalName != "checksum")
                        {
                            currentEncryptionInfo.ExtensionProperties[reader.LocalName] = reader.Value;
                        }
                    }
                    reader.MoveToElement();
                }
                else if (reader.LocalName == "algorithm" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    string? algorithmName = reader.GetAttribute("algorithm-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("algorithm-name");
                    string? ivStr = reader.GetAttribute("initialisation-vector", OdfNamespaces.Manifest) ?? reader.GetAttribute("initialisation-vector");

                    if (!string.IsNullOrEmpty(algorithmName))
                    {
                        currentEncryptionInfo.AlgorithmName = algorithmName;
                    }
                    if (!string.IsNullOrEmpty(ivStr))
                    {
                        currentEncryptionInfo.InitialisationVector = Convert.FromBase64String(ivStr);
                    }
                }
                else if (reader.LocalName == "key-derivation" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    string? derivationName = reader.GetAttribute("key-derivation-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-derivation-name");
                    string? keySizeStr = reader.GetAttribute("key-size", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-size");
                    string? iterationCountStr = reader.GetAttribute("iteration-count", OdfNamespaces.Manifest) ?? reader.GetAttribute("iteration-count");
                    string? saltStr = reader.GetAttribute("salt", OdfNamespaces.Manifest) ?? reader.GetAttribute("salt");

                    if (!string.IsNullOrEmpty(derivationName))
                    {
                        currentEncryptionInfo.KeyDerivationName = derivationName;
                    }
                    if (int.TryParse(keySizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int keySize))
                    {
                        currentEncryptionInfo.KeySize = keySize;
                    }
                    if (int.TryParse(iterationCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iterationCount))
                    {
                        if (iterationCount > 50000)
                        {
                            throw new CryptographicException($"PBKDF2 iteration count {iterationCount} exceeds the maximum limit of 50000.");
                        }
                        currentEncryptionInfo.IterationCount = iterationCount;
                    }
                    if (!string.IsNullOrEmpty(saltStr))
                    {
                        currentEncryptionInfo.Salt = Convert.FromBase64String(saltStr);
                    }

                    // 讀取 key-derivation 節點的所有其它擴充屬性
                    for (int i = 0; i < reader.AttributeCount; i++)
                    {
                        reader.MoveToAttribute(i);
                        if (reader.LocalName is not "key-derivation-name" and not "key-size" and not "iteration-count" and not "salt")
                        {
                            currentEncryptionInfo.ExtensionProperties[reader.LocalName] = reader.Value;
                        }
                    }
                    reader.MoveToElement();
                }
                else if (reader.LocalName == "encrypted-key" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
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
                        continue;
                    }

                    string keyPacket = reader.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(keyPacket))
                    {
                        encryptedKey.KeyPacket = Convert.FromBase64String(keyPacket.Trim());
                    }
                    currentEncryptionInfo.OpenPgpEncryptedKeys.Add(encryptedKey);
                    continue;
                }
                else if (reader.LocalName == "start-key-generation" && reader.NamespaceURI == OdfNamespaces.Manifest && currentEncryptionInfo != null)
                {
                    string? startKeyGenName = reader.GetAttribute("start-key-generation-name", OdfNamespaces.Manifest) ?? reader.GetAttribute("start-key-generation-name");
                    string? startKeySizeStr = reader.GetAttribute("key-size", OdfNamespaces.Manifest) ?? reader.GetAttribute("key-size");

                    if (!string.IsNullOrEmpty(startKeyGenName))
                    {
                        currentEncryptionInfo.StartKeyGenerationName = startKeyGenName;
                    }
                    if (int.TryParse(startKeySizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int startKeySize))
                    {
                        currentEncryptionInfo.StartKeySize = startKeySize;
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
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
    }

    private bool IsSafeManifestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

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
        {
            return false;
        }

        var parts = normalized.Split('/');
        foreach (var part in parts)
        {
            if (part.Length == 0 || part == "." || part == "..")
            {
                return false;
            }
        }

        return true;
    }


    #endregion
}
