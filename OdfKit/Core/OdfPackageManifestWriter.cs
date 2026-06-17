using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 manifest.xml 寫入引擎（內部協作者）。
/// </summary>
internal static class OdfPackageManifestWriter
{
    /// <summary>
    /// 將目前封裝狀態序列化為 META-INF/manifest.xml 虛擬項目。
    /// </summary>
    internal static void WriteManifest(OdfPackage.OdfPackageSaveCollaborators ctx)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = ctx.SaveOptions.IndentXml
        };

        using (var writer = XmlWriter.Create(ms, settings))
        {
            writer.WriteStartDocument();
            string versionText = ctx.Version switch
            {
                OdfVersion.Odf10 => "1.0",
                OdfVersion.Odf11 => "1.1",
                OdfVersion.Odf12 => "1.2",
                OdfVersion.Odf13 => "1.3",
                OdfVersion.Odf14 => "1.4",
                _ => "1.4"
            };
            writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, versionText);

            writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
            writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, ctx.MimeType ?? "application/vnd.oasis.opendocument.text");
            writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, versionText);
            writer.WriteEndElement();

            var directories = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in ctx.Manifest.Keys)
            {
                int slashIdx = key.IndexOf('/');
                if (slashIdx == -1)
                    continue;

                string dir = key.Substring(0, slashIdx + 1);
                if (directories.ContainsKey(dir))
                    continue;

                string mimeKey = dir + "mimetype";
                string mimeType = "";
                if (ctx.Entries.TryGetValue(mimeKey, out OdfPackageEntry? mimeEntry))
                {
                    try
                    {
                        using var r = new StreamReader(mimeEntry.OpenReader(), Encoding.UTF8);
                        mimeType = r.ReadToEnd().Trim();
                    }
                    catch
                    {
                        // 忽略個別 mimetype 讀取失敗
                    }
                }

                directories[dir] = mimeType;
            }

            var sortedKeys = new List<string>(ctx.Manifest.Keys);
            foreach (string dir in directories.Keys)
            {
                if (!sortedKeys.Contains(dir))
                    sortedKeys.Add(dir);
            }

            sortedKeys.Sort(StringComparer.Ordinal);
            foreach (string key in sortedKeys)
            {
                if (key == "/" || key == "mimetype" || key == "META-INF/manifest.xml")
                    continue;

                writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, key);

                string mediaType = ctx.Manifest.TryGetValue(key, out string? mt)
                    ? mt
                    : directories.TryGetValue(key, out string? dm) ? dm : "";
                writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, mediaType);

                if (ctx.Entries.TryGetValue(key, out OdfPackageEntry? entry) && entry.EncryptionInfo is not null)
                    WriteEncryptionData(writer, entry.EncryptionInfo);

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        const string manifestEntryName = "META-INF/manifest.xml";
        ctx.Entries[manifestEntryName] = new OdfPackageEntry(manifestEntryName, ms.ToArray());
        ctx.Manifest[manifestEntryName] = "text/xml";
    }

    private static void WriteEncryptionData(XmlWriter writer, OdfEncryptionInfo info)
    {
        writer.WriteStartElement("encryption-data", OdfNamespaces.Manifest);
        writer.WriteAttributeString("manifest", "checksum-type", OdfNamespaces.Manifest, info.ChecksumType);
        writer.WriteAttributeString("manifest", "checksum", OdfNamespaces.Manifest, Convert.ToBase64String(info.Checksum));

        if (info.ExtensionProperties is not null)
        {
            foreach (KeyValuePair<string, string> prop in info.ExtensionProperties)
                writer.WriteAttributeString("manifest", prop.Key, OdfNamespaces.Manifest, prop.Value);
        }

        writer.WriteStartElement("algorithm", OdfNamespaces.Manifest);
        writer.WriteAttributeString("manifest", "algorithm-name", OdfNamespaces.Manifest, info.AlgorithmName);
        writer.WriteAttributeString("manifest", "initialisation-vector", OdfNamespaces.Manifest, Convert.ToBase64String(info.InitialisationVector));
        writer.WriteEndElement();

        foreach (OdfOpenPgpEncryptedKeyInfo encryptedKey in info.OpenPgpEncryptedKeys)
        {
            writer.WriteStartElement("encrypted-key", OdfNamespaces.Manifest);
            if (!string.IsNullOrEmpty(encryptedKey.KeyId))
                writer.WriteAttributeString("manifest", "key-id", OdfNamespaces.Manifest, encryptedKey.KeyId);
            if (!string.IsNullOrEmpty(encryptedKey.Recipient))
                writer.WriteAttributeString("manifest", "recipient", OdfNamespaces.Manifest, encryptedKey.Recipient);
            if (!string.IsNullOrEmpty(encryptedKey.AlgorithmName))
                writer.WriteAttributeString("manifest", "algorithm-name", OdfNamespaces.Manifest, encryptedKey.AlgorithmName);
            foreach (KeyValuePair<string, string> prop in encryptedKey.ExtensionProperties)
                writer.WriteAttributeString("manifest", prop.Key, OdfNamespaces.Manifest, prop.Value);
            if (encryptedKey.KeyPacket.Length > 0)
                writer.WriteString(Convert.ToBase64String(encryptedKey.KeyPacket));
            writer.WriteEndElement();
        }

        writer.WriteStartElement("key-derivation", OdfNamespaces.Manifest);
        writer.WriteAttributeString("manifest", "key-derivation-name", OdfNamespaces.Manifest, info.KeyDerivationName);
        writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.KeySize.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("manifest", "iteration-count", OdfNamespaces.Manifest, info.IterationCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("manifest", "salt", OdfNamespaces.Manifest, Convert.ToBase64String(info.Salt));

        if (info.ExtensionProperties is not null)
        {
            foreach (KeyValuePair<string, string> prop in info.ExtensionProperties)
            {
                switch (prop.Key)
                {
                    case "kdf-name":
                        writer.WriteAttributeString("loext", "kdf-name", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                        break;
                    case "argon2-t":
                        writer.WriteAttributeString("loext", "argon2-t", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                        break;
                    case "argon2-m":
                        writer.WriteAttributeString("loext", "argon2-m", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                        break;
                    case "argon2-p":
                        writer.WriteAttributeString("loext", "argon2-p", "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0", prop.Value);
                        break;
                }
            }
        }

        writer.WriteEndElement();

        if (!string.IsNullOrEmpty(info.StartKeyGenerationName) && info.StartKeySize.HasValue)
        {
            writer.WriteStartElement("start-key-generation", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "start-key-generation-name", OdfNamespaces.Manifest, info.StartKeyGenerationName);
            writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.StartKeySize.Value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }
}
