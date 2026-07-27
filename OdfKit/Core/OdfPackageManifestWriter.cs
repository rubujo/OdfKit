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
    /// 將目前封裝狀態序列化為 META-INF/manifest.xml 虛擬專案。
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
                {
                    // ODF 1.0～1.4 Part 2 §3.4.1：加密項目必須宣告原始未壓縮未加密大小，
                    // 消費端據此配置解壓緩衝；屬性順序須排在 encryption-data 子元素之前。
                    if (entry.EncryptionInfo.PlaintextSize is long plaintextSize)
                    {
                        writer.WriteAttributeString(
                            "manifest",
                            "size",
                            OdfNamespaces.Manifest,
                            plaintextSize.ToString(CultureInfo.InvariantCulture));
                    }

                    WriteEncryptionData(writer, entry.EncryptionInfo);
                }

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

        // ODF 1.0～1.4 Part 2 的 manifest schema 只允許 encryption-data 帶 checksum-type 與 checksum；
        // 金鑰衍生的擴充屬性屬於 key-derivation，不在此處輸出。
        writer.WriteAttributeString("manifest", "checksum-type", OdfNamespaces.Manifest, info.ChecksumType);
        writer.WriteAttributeString("manifest", "checksum", OdfNamespaces.Manifest, Convert.ToBase64String(info.Checksum));

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

        // manifest schema 的 encryption-data 內容順序為 algorithm →〔start-key-generation〕→ key-derivation。
        if (!string.IsNullOrEmpty(info.StartKeyGenerationName) && info.StartKeySize.HasValue)
        {
            writer.WriteStartElement("start-key-generation", OdfNamespaces.Manifest);
            writer.WriteAttributeString("manifest", "start-key-generation-name", OdfNamespaces.Manifest, info.StartKeyGenerationName);
            writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.StartKeySize.Value.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        bool isArgon2 = string.Equals(info.KeyDerivationName, OdfEncryption.Argon2idDerivationUri, StringComparison.Ordinal)
            || string.Equals(info.KeyDerivationName, OdfEncryption.Argon2idOdf15DerivationUri, StringComparison.Ordinal);

        writer.WriteStartElement("key-derivation", OdfNamespaces.Manifest);
        writer.WriteAttributeString("manifest", "key-derivation-name", OdfNamespaces.Manifest, info.KeyDerivationName);
        writer.WriteAttributeString("manifest", "key-size", OdfNamespaces.Manifest, info.KeySize.ToString(CultureInfo.InvariantCulture));

        // Argon2id 分支不使用 manifest:iteration-count：迭代次數由 loext:argon2-iterations 表示，
        // 且 LibreOffice 的 manifest schema 在該分支不允許此屬性。
        if (!isArgon2)
        {
            writer.WriteAttributeString("manifest", "iteration-count", OdfNamespaces.Manifest, info.IterationCount.ToString(CultureInfo.InvariantCulture));
        }

        writer.WriteAttributeString("manifest", "salt", OdfNamespaces.Manifest, Convert.ToBase64String(info.Salt));

        if (info.ExtensionProperties is not null)
        {
            foreach (KeyValuePair<string, string> prop in info.ExtensionProperties)
            {
                if (LoextKeyDerivationAttributes.Contains(prop.Key))
                {
                    writer.WriteAttributeString("loext", prop.Key, OdfNamespaces.LoExt, prop.Value);
                }
            }
        }

        writer.WriteEndElement();

        writer.WriteEndElement();
    }

    /// <summary>
    /// key-derivation 允許輸出的 loext 擴充屬性；與 LibreOffice
    /// `OpenDocument-v1.4+libreoffice-manifest-schema.rng` 的 Argon2id 分支一致，
    /// 另保留早期 OdfKit 版本的縮寫屬性名以維持既有檔案的來回讀寫。
    /// </summary>
    private static readonly HashSet<string> LoextKeyDerivationAttributes = new(StringComparer.Ordinal)
    {
        "argon2-iterations",
        "argon2-memory",
        "argon2-lanes",
        "kdf-name",
        "argon2-t",
        "argon2-m",
        "argon2-p"
    };
}
