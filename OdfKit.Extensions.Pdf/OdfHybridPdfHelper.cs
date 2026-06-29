using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Compliance;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using Xml = System.Xml;
namespace OdfKit.Core;

/// <summary>
/// Embeds ODF packages in hybrid PDFs and extracts them back out.
/// 提供混合 PDF （Hybrid PDF）的公用程式方法，支援在 PDF 中內嵌與提取 ODF 檔案。
/// </summary>
public static class OdfHybridPdfHelper
{
    private const string OdfRelationName = "OdfKitHybridOdf";

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
        if (pdfStream is null)
            throw new ArgumentNullException(nameof(pdfStream));

        // 載入 PDF 文件（用於提取附件的匯入模式）
        using var document = string.IsNullOrEmpty(password)
            ? PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import)
            : PdfReader.Open(pdfStream, password, PdfDocumentOpenMode.Import);

        // 加密防禦檢查
        if (document.SecuritySettings.IsEncrypted && string.IsNullOrEmpty(password))
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_EncryptedPdfArchivesCannot"));
        }

        PdfCatalog catalog = document.Internals.Catalog;
        var names = catalog.Elements.GetDictionary("/Names");
        if (names is null)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfCatalogNamesMissing"));
            return null;
        }

        var embeddedFiles = names.Elements.GetDictionary("/EmbeddedFiles");
        if (embeddedFiles is null)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfEmbeddedFilesMissing"));
            return null;
        }

        var namesArray = embeddedFiles.Elements.GetArray("/Names");
        if (namesArray is null)
            return null;

        // 逐一查看名稱樹狀陣列中的名稱值對
        for (int i = 0; i < namesArray.Elements.Count; i += 2)
        {
            if (i + 1 >= namesArray.Elements.Count)
                break;

            // 值是一個 Filespec 字典（間接或直接）
            var filespecRef = namesArray.Elements[i + 1] as PdfReference;
            var filespec = (filespecRef is not null ? filespecRef.Value : namesArray.Elements[i + 1]) as PdfDictionary;
            if (filespec is null)
                continue;

            var ef = filespec.Elements.GetDictionary("/EF");
            if (ef is null)
                continue;

            // /F 鍵保存了實際的內嵌資料流物件
            var fItem = ef.Elements["/F"];
            var fRef = fItem as PdfReference;
            var streamDict = (fRef is not null ? fRef.Value : fItem) as PdfDictionary;
            if (streamDict is null)
                continue;

            // 檢查名稱的副檔名（例如 .odt, .ods）
            string? fileName = filespec.Elements.GetString("/F");
            if (fileName is not null && (fileName.EndsWith(".odt", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".ods", StringComparison.OrdinalIgnoreCase) ||
                                      fileName.EndsWith(".odp", StringComparison.OrdinalIgnoreCase)))
            {
                if (streamDict.Stream is not null)
                {
                    OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_EmbeddedOdfExtracted", fileName));
                    return streamDict.Stream.Value;
                }
            }
        }

        OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_EmbeddedOdfNotFound"));
        return null;
    }

    /// <summary>
    /// Embeds an ODF package into a PDF as a hybrid PDF attachment.
    /// 將 ODF 檔案作為附件注入 PDF 中，生成混合 PDF （Hybrid PDF）。
    /// </summary>
    /// <param name="pdfPath">The path or URI. / 來源 PDF 檔案路徑</param>
    /// <param name="odfPath">The path or URI. / 要注入的 ODF 檔案路徑</param>
    /// <param name="outputPdfPath">The path or URI. / 輸出的混合 PDF 檔案路徑</param>
    /// <param name="password">The value to use. / PDF 密碼，若無則為 <see langword="null"/></param>
    public static void InjectOdfToPdf(string pdfPath, string odfPath, string outputPdfPath, string? password = null)
    {
        using var pdfSrc = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var odfSrc = new FileStream(odfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var pdfDest = new FileStream(outputPdfPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        InjectOdfToPdf(pdfSrc, odfSrc, pdfDest, Path.GetFileName(odfPath), password);
    }

    /// <summary>
    /// Embeds an ODF package into a PDF as a hybrid PDF attachment.
    /// 將 ODF 檔案流作為附件注入 PDF 檔案流中，生成混合 PDF 。
    /// </summary>
    /// <param name="pdfStream">The source or target object. / 來源 PDF 檔案的資料流</param>
    /// <param name="odfStream">The source or target object. / 要注入的 ODF 檔案的資料流</param>
    /// <param name="outputPdfStream">The source or target object. / 接收輸出的混合 PDF 檔案資料流</param>
    /// <param name="odfFileName">The path or URI. / 注入的 ODF 專案檔名</param>
    /// <param name="password">The value to use. / PDF 密碼，若無則為 <see langword="null"/></param>
    public static void InjectOdfToPdf(Stream pdfStream, Stream odfStream, Stream outputPdfStream, string odfFileName, string? password = null)
    {
        if (pdfStream is null)
            throw new ArgumentNullException(nameof(pdfStream));
        if (odfStream is null)
            throw new ArgumentNullException(nameof(odfStream));
        if (outputPdfStream is null)
            throw new ArgumentNullException(nameof(outputPdfStream));
        if (string.IsNullOrWhiteSpace(odfFileName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_OdfFileNameSpecified"), nameof(odfFileName));

        // 讀取 ODF 資料
        byte[] odfData;
        using (var ms = new MemoryStream())
        {
            odfStream.CopyTo(ms);
            odfData = ms.ToArray();
        }

        // 載入來源 PDF
        using var document = string.IsNullOrEmpty(password)
            ? PdfReader.Open(pdfStream, PdfDocumentOpenMode.Modify)
            : PdfReader.Open(pdfStream, password, PdfDocumentOpenMode.Modify);

        // 加密防禦檢查
        if (document.SecuritySettings.IsEncrypted && string.IsNullOrEmpty(password))
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableProcessEncryptedPdf"));
        }

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

        // 1. 為內嵌檔案建立資料流字典
        var efStream = new PdfDictionary(document);
        document.Internals.AddObject(efStream);
        efStream.Elements.SetName("/Type", "/EmbeddedFile");
        efStream.Elements.SetString("/Subtype", mimeType);
        efStream.CreateStream(odfData);

        // 2. 建立 Filespec 字典
        var filespec = new PdfDictionary(document);
        document.Internals.AddObject(filespec);
        filespec.Elements.SetName("/Type", "/Filespec");
        filespec.Elements.SetString("/F", odfFileName);
        filespec.Elements.SetString("/UF", odfFileName);

        // 新增 PDF/A-3 關聯性中繼資料
        filespec.Elements.SetName("/AFRelationship", "/Source");

        var efDict = new PdfDictionary(document);
        filespec.Elements.SetObject("/EF", efDict);
        efDict.Elements.SetReference("/F", efStream);

        // 3. 將 Filespec 參考新增至 /Names -> /EmbeddedFiles Catalog 名稱樹
        PdfCatalog catalog = document.Internals.Catalog;

        var names = catalog.Elements.GetDictionary("/Names");
        if (names is null)
        {
            names = new PdfDictionary(document);
            document.Internals.AddObject(names);
            catalog.Elements.SetReference("/Names", names);
        }

        var embeddedFiles = names.Elements.GetDictionary("/EmbeddedFiles");
        if (embeddedFiles is null)
        {
            embeddedFiles = new PdfDictionary(document);
            document.Internals.AddObject(embeddedFiles);
            names.Elements.SetReference("/EmbeddedFiles", embeddedFiles);
        }

        var namesArray = embeddedFiles.Elements.GetArray("/Names");
        if (namesArray is null)
        {
            namesArray = new PdfArray(document);
            embeddedFiles.Elements.SetObject("/Names", namesArray);
        }

        // 新增至 Names 陣列：名稱與 Filespec 參考
        namesArray.Elements.Add(new PdfString(OdfRelationName));
        if (filespec.Reference is null)
        {
            throw new CryptographicException(OdfLocalizer.GetMessage("Err_OdfHybridPdfHelper_UnableGeneratePdfReference"));
        }
        namesArray.Elements.Add(filespec.Reference);

        // 4. 若 Catalog 具有 /Metadata，則將 PDF/A-3 結構描述注入至 XMP 中繼資料中
        InjectPdfAXmpMetadata(document, odfFileName, mimeType);

        // 儲存修改後的文件
        document.Save(outputPdfStream);
        OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_OdfAttachmentInjected", odfFileName));
    }

    private static void InjectPdfAXmpMetadata(PdfDocument document, string odfFileName, string mimeType)
    {
        PdfCatalog catalog = document.Internals.Catalog;
        var metadataRef = catalog.Elements["/Metadata"] as PdfReference;
        var metadataDict = (metadataRef is not null ? metadataRef.Value : catalog.Elements["/Metadata"]) as PdfDictionary;

        if (metadataDict is null || metadataDict.Stream is null)
        {
            OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfCatalogMetadataMissing"));
            return;
        }

        try
        {
            // 擷取原始的 XMP XML 位元組
            byte[] xmpBytes = metadataDict.Stream.Value;
            string xmpText = Encoding.UTF8.GetString(xmpBytes);

            // 載入至 XmlDocument
            var xmlDoc = new Xml.XmlDocument();
            var xmlSettings = new Xml.XmlReaderSettings { DtdProcessing = Xml.DtdProcessing.Prohibit, XmlResolver = null };
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
                // 若不存在，則新增 PDF/A-3 結構描述宣告
                // 為確保通過驗證器，我們注入基本的 PDF/A 附件結構描述
                string schemaSnippet = $@"
                    <pdfaExtension:schemas>
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

                // 寫回更新後的中繼資料資料流
                using var ms = new MemoryStream();
                var settings = new Xml.XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false
                };
                using (var writer = Xml.XmlWriter.Create(ms, settings))
                {
                    xmlDoc.Save(writer);
                }
                metadataDict.CreateStream(ms.ToArray());
                OdfKitDiagnostics.Info(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfaXmpMetadataInjected"));
            }
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn(OdfLocalizer.GetMessage("Diag_OdfHybridPdfHelper_PdfaXmpMetadataUpdateFailed", ex.Message));
        }
    }
}

