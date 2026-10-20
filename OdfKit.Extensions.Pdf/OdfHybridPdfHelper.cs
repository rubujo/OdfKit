using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Export.PdfInternal;
using Xml = System.Xml;
namespace OdfKit.Core;

/// <summary>
/// Embeds ODF packages in hybrid PDFs and extracts them back out.
/// 提供混合 PDF （Hybrid PDF）的公用程式方法，支援在 PDF 中內嵌與提取 ODF 檔案。
/// </summary>
/// <remarks>
/// 以增量更新附加內嵌檔案，原有 PDF 內容（含既有簽章的涵蓋範圍）不會被改寫。
/// 支援標準安全處理常式加密的 PDF（RC4 40／128 位元、AES-128、AES-256）；加密但未提供正確密碼時擲出 <see cref="CryptographicException"/>。
/// </remarks>
public static class OdfHybridPdfHelper
{
    private const string OdfRelationName = "OdfKitHybridOdf";

    /// <summary>
    /// XMP 中繼資料 XML 讀取字元上限（與核心 <c>OdfLoadOptions.MaxXmlCharactersInDocument</c> 預設值一致）。
    /// </summary>
    private const long MaxXmpXmlCharactersInDocument = 64 * 1024 * 1024;

    /// <summary>
    /// Extracts an embedded ODF package from a hybrid PDF.
    /// 從混合 PDF 檔案中提取並讀取隱藏的 ODF 檔案。
    /// </summary>
    /// <param name="pdfPath">The path or URI. / PDF 檔案路徑</param>
    /// <param name="password">The value to use. / PDF 檔案密碼，若無則為 <see langword="null"/></param>
    /// <returns>The result. / 傳回提取出的 ODF 檔案位元組陣列；若未找到則為 <see langword="null"/></returns>
    public static byte[]? ExtractOdfFromPdf(string pdfPath, string? password = null)
    {
        using var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return ExtractOdfFromPdf(fs, password);
    }

    /// <summary>
    /// Extracts an embedded ODF package from a hybrid PDF.
    /// 從混合 PDF 檔案流中提取並讀取隱藏的 ODF 檔案。
    /// </summary>
    /// <param name="pdfStream">The source or target object. / PDF 檔案的資料流</param>
    /// <param name="password">The value to use. / PDF 檔案密碼，若無則為 <see langword="null"/></param>
    /// <returns>The result. / 傳回提取出的 ODF 檔案位元組陣列；若未找到則為 <see langword="null"/></returns>
    public static byte[]? ExtractOdfFromPdf(Stream pdfStream, string? password = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(pdfStream, nameof(pdfStream));

        PdfFile document = OpenPdf(pdfStream, password, OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_EncryptedPdfArchivesCannot"));

        PdfDict? catalog = document.ResolveDict(document.Trailer["Root"]);
        PdfDict? names = catalog is null || !catalog.TryGetValue("Names", out object? namesValue) ? null : document.ResolveDict(namesValue);
        if (names is null)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"));
            return null;
        }

        PdfDict? embeddedFiles = names.TryGetValue("EmbeddedFiles", out object? embeddedValue) ? document.ResolveDict(embeddedValue) : null;
        if (embeddedFiles is null)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"));
            return null;
        }

        byte[]? extracted = FindEmbeddedOdf(document, embeddedFiles, 0);
        if (extracted is null)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"));
        }

        return extracted;
    }

    // 逐一走訪名稱樹（Names 陣列與 Kids），找出副檔名為 ODF 的內嵌檔案。
    private static byte[]? FindEmbeddedOdf(PdfFile document, PdfDict node, int depth)
    {
        if (depth > 16)
        {
            return null;
        }

        if (node.TryGetValue("Names", out object? namesValue) && document.ResolveArray(namesValue) is List<object?> namesArray)
        {
            for (int i = 0; i + 1 < namesArray.Count; i += 2)
            {
                PdfDict? filespec = document.ResolveDict(namesArray[i + 1]);
                if (filespec is null || !filespec.TryGetValue("EF", out object? efValue))
                {
                    continue;
                }

                PdfDict? ef = document.ResolveDict(efValue);
                if (ef is null || !ef.TryGetValue("F", out object? streamValue) || document.Resolve(streamValue) is not PdfStream stream)
                {
                    continue;
                }

                string? fileName = (document.Resolve(filespec.TryGetValue("UF", out object? uf) ? uf : null) as PdfString)?.Text()
                    ?? (document.Resolve(filespec.TryGetValue("F", out object? f) ? f : null) as PdfString)?.Text();
                if (fileName is not null && (fileName.EndsWith(".odt", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".ods", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".odp", StringComparison.OrdinalIgnoreCase)))
                {
                    byte[]? data = document.DecodeStream(stream);
                    if (data is not null)
                    {
                        OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted", fileName));
                        return data;
                    }
                }
            }
        }

        if (node.TryGetValue("Kids", out object? kidsValue) && document.ResolveArray(kidsValue) is List<object?> kids)
        {
            foreach (object? kid in kids)
            {
                PdfDict? child = document.ResolveDict(kid);
                if (child is not null && FindEmbeddedOdf(document, child, depth + 1) is byte[] found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Embeds an ODF package into a PDF as a hybrid PDF attachment.
    /// 將 ODF 檔案作為附件注入 PDF 中，產生混合 PDF（Hybrid PDF）。
    /// </summary>
    /// <param name="pdfPath">The path or URI. / 來源 PDF 檔案路徑</param>
    /// <param name="odfPath">The path or URI. / 要注入的 ODF 檔案路徑</param>
    /// <param name="outputPdfPath">The path or URI. / 輸出的混合 PDF 檔案路徑</param>
    /// <param name="password">The value to use. / PDF 密碼，若無則為 <see langword="null"/></param>
    public static void InjectOdfToPdf(string pdfPath, string odfPath, string outputPdfPath, string? password = null)
    {
        using var pdfSrc = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var odfSrc = new FileStream(odfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string? directory = Path.GetDirectoryName(outputPdfPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = OdfAtomicFile.CreateTemporaryPath(outputPdfPath);
        try
        {
            using (var pdfDest = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                InjectOdfToPdf(pdfSrc, odfSrc, pdfDest, Path.GetFileName(odfPath), password);
            }

            OdfAtomicFile.Publish(temporaryPath, Path.GetFullPath(outputPdfPath));
        }
        finally
        {
            OdfAtomicFile.TryDelete(temporaryPath);
        }
    }

    /// <summary>
    /// Embeds an ODF package into a PDF as a hybrid PDF attachment.
    /// 將 ODF 檔案串流作為附件注入 PDF 檔案串流中，產生混合 PDF。
    /// </summary>
    /// <param name="pdfStream">The source or target object. / 來源 PDF 檔案的資料流</param>
    /// <param name="odfStream">The source or target object. / 要注入的 ODF 檔案的資料流</param>
    /// <param name="outputPdfStream">The source or target object. / 接收輸出的混合 PDF 檔案資料流</param>
    /// <param name="odfFileName">The path or URI. / 注入的 ODF 專案檔名</param>
    /// <param name="password">The value to use. / PDF 密碼，若無則為 <see langword="null"/></param>
    public static void InjectOdfToPdf(Stream pdfStream, Stream odfStream, Stream outputPdfStream, string odfFileName, string? password = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(pdfStream, nameof(pdfStream));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(odfStream, nameof(odfStream));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(outputPdfStream, nameof(outputPdfStream));
        if (string.IsNullOrWhiteSpace(odfFileName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_OdfFileNameSpecified"), nameof(odfFileName));

        // 讀取 ODF 資料
        byte[] odfData;
        using (var ms = new MemoryStream())
        {
            odfStream.CopyTo(ms);
            odfData = ms.ToArray();
        }

        PdfFile document = OpenPdf(pdfStream, password, OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableProcessEncryptedPdf"));

        // 決定 MIME 類型
        string mimeType = "application/vnd.oasis.opendocument.text";
        if (odfFileName.EndsWith(".ods", StringComparison.OrdinalIgnoreCase))
        {
            mimeType = "application/vnd.oasis.opendocument.spreadsheet";
        }
        else if (odfFileName.EndsWith(".odp", StringComparison.OrdinalIgnoreCase))
        {
            mimeType = "application/vnd.oasis.opendocument.presentation";
        }

        var update = new PdfIncrementalUpdate(document);

        // 1. 內嵌檔案資料流（ODF 套件本身已是 ZIP，不再壓縮）。
        var efDictionary = new PdfDict
        {
            ["Type"] = new PdfName("EmbeddedFile"),
            ["Subtype"] = new PdfName(mimeType),
            ["Params"] = new PdfDict { ["Size"] = (long)odfData.Length },
        };
        int efNumber = update.Add(new PdfStream(efDictionary, odfData));
        var efRef = new PdfRef(efNumber, 0);

        // 2. Filespec 字典（含 PDF/A-3 關聯性中繼資料）。
        var filespec = new PdfDict
        {
            ["Type"] = new PdfName("Filespec"),
            ["F"] = new PdfString(Encoding.UTF8.GetBytes(odfFileName)),
            ["UF"] = new PdfString(EncodeTextString(odfFileName)),
            ["AFRelationship"] = new PdfName("Source"),
            ["EF"] = new PdfDict { ["F"] = efRef, ["UF"] = efRef },
        };
        int filespecNumber = update.Add(filespec);
        var filespecRef = new PdfRef(filespecNumber, 0);

        // 3. 把 Filespec 加到 Catalog 的 /Names -> /EmbeddedFiles 名稱樹與 /AF。
        object? rootValue = document.Trailer["Root"];
        if (rootValue is not PdfRef rootRef || document.ResolveDict(rootRef) is not PdfDict catalog)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }

        var newCatalog = new PdfDict(catalog);
        AddToNameTree(document, update, newCatalog, filespecRef);
        AppendAssociatedFile(document, newCatalog, filespecRef);
        update.Replace(rootRef.Number, newCatalog);

        // 4. 若 Catalog 具有 /Metadata，則將 PDF/A-3 結構描述注入至 XMP 中繼資料中
        InjectPdfAXmpMetadata(document, update, newCatalog);

        byte[] result = update.Build();
        outputPdfStream.Write(result, 0, result.Length);
        OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_OdfAttachmentInjected", odfFileName));
    }

    private static PdfFile OpenPdf(Stream pdfStream, string? password, string encryptedMessage)
    {
        byte[] data;
        using (var ms = new MemoryStream())
        {
            pdfStream.CopyTo(ms);
            data = ms.ToArray();
        }

        try
        {
            return PdfFile.Open(data, password);
        }
        catch (CryptographicException) when (string.IsNullOrEmpty(password))
        {
            // 加密文件但沒有提供密碼。
            throw new CryptographicException(encryptedMessage);
        }
    }

    private static byte[] EncodeTextString(string value)
    {
        byte[] body = Encoding.BigEndianUnicode.GetBytes(value);
        var bytes = new byte[body.Length + 2];
        bytes[0] = 0xFE;
        bytes[1] = 0xFF;
        Buffer.BlockCopy(body, 0, bytes, 2, body.Length);
        return bytes;
    }

    // 以名稱排序插入 /Names 陣列；若名稱樹已有 /Kids，則附加一個新的葉節點。
    private static void AddToNameTree(PdfFile document, PdfIncrementalUpdate update, PdfDict catalog, PdfRef filespecRef)
    {
        var nameKey = new PdfString(Encoding.UTF8.GetBytes(OdfRelationName));
        (PdfDict names, int? namesNumber) = CloneDictionary(document, catalog.TryGetValue("Names", out object? namesValue) ? namesValue : null);
        (PdfDict embedded, int? embeddedNumber) = CloneDictionary(document, names.TryGetValue("EmbeddedFiles", out object? embeddedValue) ? embeddedValue : null);

        if (embedded.TryGetValue("Kids", out object? kidsValue) && document.ResolveArray(kidsValue) is List<object?> kids)
        {
            var leaf = new PdfDict
            {
                ["Names"] = new List<object?> { nameKey, filespecRef },
                ["Limits"] = new List<object?> { nameKey, nameKey },
            };
            var newKids = new List<object?>(kids) { new PdfRef(update.Add(leaf), 0) };
            embedded["Kids"] = newKids;
        }
        else
        {
            var pairs = embedded.TryGetValue("Names", out object? existing) && document.ResolveArray(existing) is List<object?> list
                ? new List<object?>(list)
                : [];
            int position = pairs.Count;
            for (int i = 0; i + 1 < pairs.Count; i += 2)
            {
                if (pairs[i] is PdfString key && CompareBytes(nameKey.Value, key.Value) < 0)
                {
                    position = i;
                    break;
                }
            }

            pairs.Insert(position, filespecRef);
            pairs.Insert(position, nameKey);
            embedded["Names"] = pairs;
        }

        if (embeddedNumber is int embeddedObject)
        {
            update.Replace(embeddedObject, embedded);
            names["EmbeddedFiles"] = new PdfRef(embeddedObject, 0);
        }
        else
        {
            names["EmbeddedFiles"] = embedded;
        }

        if (namesNumber is int namesObject)
        {
            update.Replace(namesObject, names);
            catalog["Names"] = new PdfRef(namesObject, 0);
        }
        else
        {
            catalog["Names"] = names;
        }
    }

    // 回傳字典的可修改複本，以及它原本若是間接物件時的物件編號。
    private static (PdfDict Copy, int? Number) CloneDictionary(PdfFile document, object? value)
    {
        if (value is PdfRef reference && document.ResolveDict(reference) is PdfDict resolved)
        {
            return (new PdfDict(resolved), reference.Number);
        }

        if (value is PdfDict direct)
        {
            return (new PdfDict(direct), null);
        }

        return (new PdfDict(), null);
    }

    private static int CompareBytes(byte[] a, byte[] b)
    {
        int length = Math.Min(a.Length, b.Length);
        for (int i = 0; i < length; i++)
        {
            if (a[i] != b[i])
            {
                return a[i].CompareTo(b[i]);
            }
        }

        return a.Length.CompareTo(b.Length);
    }

    private static void AppendAssociatedFile(PdfFile document, PdfDict catalog, PdfRef filespecRef)
    {
        var list = catalog.TryGetValue("AF", out object? existing) && document.ResolveArray(existing) is List<object?> current
            ? new List<object?>(current)
            : [];
        list.Add(filespecRef);
        catalog["AF"] = list;
    }

    private static void InjectPdfAXmpMetadata(PdfFile document, PdfIncrementalUpdate update, PdfDict catalog)
    {
        if (!catalog.TryGetValue("Metadata", out object? metadataValue) || metadataValue is not PdfRef metadataRef
            || document.Resolve(metadataRef) is not PdfStream metadata)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"));
            return;
        }

        try
        {
            // 擷取原始的 XMP XML 位元組
            byte[] xmpBytes = document.DecodeStream(metadata) ?? throw new InvalidDataException("XMP metadata stream cannot be decoded.");
            string xmpText = Encoding.UTF8.GetString(xmpBytes);

            // 載入至 XmlDocument
            var xmlDoc = new Xml.XmlDocument();
            var xmlSettings = new Xml.XmlReaderSettings
            {
                DtdProcessing = Xml.DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxXmpXmlCharactersInDocument,
            };
            using (var reader = Xml.XmlReader.Create(new StringReader(xmpText), xmlSettings))
            {
                xmlDoc.Load(reader);
            }

            var nsManager = new Xml.XmlNamespaceManager(xmlDoc.NameTable);
            nsManager.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
            nsManager.AddNamespace("pdfaExtension", "http://www.aiim.org/pdfa/ns/extension/");
            nsManager.AddNamespace("pdfaSchema", "http://www.aiim.org/pdfa/ns/schema#");
            nsManager.AddNamespace("pdfaProperty", "http://www.aiim.org/pdfa/ns/property#");

            var descNode = xmlDoc.SelectSingleNode("//rdf:Description", nsManager);
            if (descNode is not null)
            {
                // 為確保通過驗證器，我們注入基本的 PDF/A 附件結構描述
                string schemaSnippet = $@"
                    <pdfaExtension:schemas xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"" xmlns:pdfaExtension=""http://www.aiim.org/pdfa/ns/extension/"" xmlns:pdfaSchema=""http://www.aiim.org/pdfa/ns/schema#"" xmlns:pdfaProperty=""http://www.aiim.org/pdfa/ns/property#"">
                        <rdf:Bag>
                            <rdf:li rdf:parseType=""Resource"">
                                <pdfaSchema:schema>PDF/A-3 Attachment Schema</pdfaSchema:schema>
                                <pdfaSchema:prefix>pdfaSchema</pdfaSchema:prefix>
                                <pdfaSchema:namespaceURI>http://www.aiim.org/pdfa/ns/schema#</pdfaSchema:namespaceURI>
                                <pdfaSchema:property>
                                    <rdf:Seq>
                                        <rdf:li rdf:parseType=""Resource"">
                                            <pdfaProperty:name>AFRelationship</pdfaProperty:name>
                                            <pdfaProperty:valueType>Text</pdfaProperty:valueType>
                                            <pdfaProperty:category>external</pdfaProperty:category>
                                            <pdfaProperty:description>Relationship to file</pdfaProperty:description>
                                        </rdf:li>
                                    </rdf:Seq>
                                </pdfaSchema:property>
                            </rdf:li>
                        </rdf:Bag>
                    </pdfaExtension:schemas>";

                var tempDoc = new Xml.XmlDocument();
                using (var reader = Xml.XmlReader.Create(new StringReader(schemaSnippet), xmlSettings))
                {
                    tempDoc.Load(reader);
                }

                var imported = xmlDoc.ImportNode(tempDoc.DocumentElement!, true);
                descNode.AppendChild(imported);

                // 寫回更新後的中繼資料資料流（未壓縮，維持其他字典項目）。
                using var ms = new MemoryStream();
                var settings = new Xml.XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false,
                };
                using (var writer = Xml.XmlWriter.Create(ms, settings))
                {
                    xmlDoc.Save(writer);
                }

                var dictionary = new PdfDict(metadata.Dictionary);
                dictionary.Remove("Filter");
                dictionary.Remove("DecodeParms");
                update.Replace(metadataRef.Number, new PdfStream(dictionary, ms.ToArray()));
                OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"));
            }
        }
        catch (Exception ex) when (ex is Xml.XmlException or InvalidDataException or ArgumentException)
        {
            OdfKitDiagnostics.Warn(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed", ex.Message));
        }
    }
}
