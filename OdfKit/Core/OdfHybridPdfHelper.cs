using System.Security.Cryptography;
using System.Text;
using Xml = System.Xml;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Advanced;

namespace OdfKit.Core
{
    public static class OdfHybridPdfHelper
    {
        private const string OdfRelationName = "OdfKitHybridOdf";

        /// <summary>
        /// 從混合 PDF (Hybrid PDF) 中提取並讀取隱藏的 ODF 檔案（.odt 或 .ods 等）。
        /// </summary>
        public static byte[]? ExtractOdfFromPdf(string pdfPath, string? password = null)
        {
            using var fs = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ExtractOdfFromPdf(fs, password);
        }

        /// <summary>
        /// 從混合 PDF 檔案流中提取並讀取隱藏的 ODF 檔案。
        /// </summary>
        public static byte[]? ExtractOdfFromPdf(Stream pdfStream, string? password = null)
        {
            if (pdfStream == null) throw new ArgumentNullException(nameof(pdfStream));

            // Load PDF document (Import mode for extracting attachments)
            using var document = string.IsNullOrEmpty(password)
                ? PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import)
                : PdfReader.Open(pdfStream, password, PdfDocumentOpenMode.Import);

            // Encryption defense check
            if (document.SecuritySettings.IsEncrypted && string.IsNullOrEmpty(password))
            {
                throw new CryptographicException("Encrypted PDF files cannot be processed for hybrid ODF extraction without a password.");
            }

            PdfCatalog catalog = document.Internals.Catalog;
            var names = catalog.Elements.GetDictionary("/Names");
            if (names == null)
            {
                OdfKitDiagnostics.Info("No '/Names' dictionary found in PDF Catalog.");
                return null;
            }

            var embeddedFiles = names.Elements.GetDictionary("/EmbeddedFiles");
            if (embeddedFiles == null)
            {
                OdfKitDiagnostics.Info("No '/EmbeddedFiles' dictionary found in PDF Names.");
                return null;
            }

            var namesArray = embeddedFiles.Elements.GetArray("/Names");
            if (namesArray == null) return null;

            // Iterate name-value pairs in the name tree array
            for (int i = 0; i < namesArray.Elements.Count; i += 2)
            {
                if (i + 1 >= namesArray.Elements.Count) break;

                // Value is a Filespec dictionary (indirect or direct)
                var filespecRef = namesArray.Elements[i + 1] as PdfReference;
                var filespec = (filespecRef != null ? filespecRef.Value : namesArray.Elements[i + 1]) as PdfDictionary;
                if (filespec == null) continue;

                var ef = filespec.Elements.GetDictionary("/EF");
                if (ef == null) continue;

                // /F key holds the actual embedded stream object
                var fItem = ef.Elements["/F"];
                var fRef = fItem as PdfReference;
                var streamDict = (fRef != null ? fRef.Value : fItem) as PdfDictionary;
                if (streamDict == null) continue;

                // Check file extension of name (e.g. .odt, .ods)
                string? fileName = filespec.Elements.GetString("/F");
                if (fileName != null && (fileName.EndsWith(".odt", StringComparison.OrdinalIgnoreCase) || 
                                          fileName.EndsWith(".ods", StringComparison.OrdinalIgnoreCase) || 
                                          fileName.EndsWith(".odp", StringComparison.OrdinalIgnoreCase)))
                {
                    if (streamDict.Stream != null)
                    {
                        OdfKitDiagnostics.Info($"Successfully extracted embedded ODF file '{fileName}' from PDF.");
                        return streamDict.Stream.Value;
                    }
                }
            }

            OdfKitDiagnostics.Info("No embedded ODF attachment found in PDF.");
            return null;
        }

        /// <summary>
        /// 將 ODF 檔案作為附件注入 PDF 中，生成混合 PDF (Hybrid PDF)。
        /// </summary>
        public static void InjectOdfToPdf(string pdfPath, string odfPath, string outputPdfPath, string? password = null)
        {
            using var pdfSrc = new FileStream(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var odfSrc = new FileStream(odfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var pdfDest = new FileStream(outputPdfPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            
            InjectOdfToPdf(pdfSrc, odfSrc, pdfDest, Path.GetFileName(odfPath), password);
        }

        /// <summary>
        /// 將 ODF 檔案流作為附件注入 PDF 檔案流中，生成混合 PDF。
        /// </summary>
        public static void InjectOdfToPdf(Stream pdfStream, Stream odfStream, Stream outputPdfStream, string odfFileName, string? password = null)
        {
            if (pdfStream == null) throw new ArgumentNullException(nameof(pdfStream));
            if (odfStream == null) throw new ArgumentNullException(nameof(odfStream));
            if (outputPdfStream == null) throw new ArgumentNullException(nameof(outputPdfStream));
            if (string.IsNullOrWhiteSpace(odfFileName)) throw new ArgumentException("ODF file name must be specified.", nameof(odfFileName));

            // Read the ODF data
            byte[] odfData;
            using (var ms = new MemoryStream())
            {
                odfStream.CopyTo(ms);
                odfData = ms.ToArray();
            }

            // Load source PDF
            using var document = string.IsNullOrEmpty(password)
                ? PdfReader.Open(pdfStream, PdfDocumentOpenMode.Modify)
                : PdfReader.Open(pdfStream, password, PdfDocumentOpenMode.Modify);

            // Encryption defense check
            if (document.SecuritySettings.IsEncrypted && string.IsNullOrEmpty(password))
            {
                throw new CryptographicException("Encrypted PDF files cannot be processed for hybrid ODF injection without a password.");
            }

            // Determine mime type
            string mimeType = "application/vnd.oasis.opendocument.text";
            if (odfFileName.EndsWith(".ods", StringComparison.OrdinalIgnoreCase))
            {
                mimeType = "application/vnd.oasis.opendocument.spreadsheet";
            }
            else if (odfFileName.EndsWith(".odp", StringComparison.OrdinalIgnoreCase))
            {
                mimeType = "application/vnd.oasis.opendocument.presentation";
            }

            // 1. Create Stream dictionary for the embedded file
            var efStream = new PdfDictionary(document);
            document.Internals.AddObject(efStream);
            efStream.Elements.SetName("/Type", "/EmbeddedFile");
            efStream.Elements.SetString("/Subtype", mimeType);
            efStream.CreateStream(odfData);

            // 2. Create Filespec dictionary
            var filespec = new PdfDictionary(document);
            document.Internals.AddObject(filespec);
            filespec.Elements.SetName("/Type", "/Filespec");
            filespec.Elements.SetString("/F", odfFileName);
            filespec.Elements.SetString("/UF", odfFileName);
            
            // Add PDF/A-3 Relationship metadata
            filespec.Elements.SetName("/AFRelationship", "/Source");

            var efDict = new PdfDictionary(document);
            filespec.Elements.SetObject("/EF", efDict);
            efDict.Elements.SetReference("/F", efStream);

            // 3. Add Filespec reference to /Names -> /EmbeddedFiles Catalog name tree
            PdfCatalog catalog = document.Internals.Catalog;
            
            var names = catalog.Elements.GetDictionary("/Names");
            if (names == null)
            {
                names = new PdfDictionary(document);
                document.Internals.AddObject(names);
                catalog.Elements.SetObject("/Names", names);
            }

            var embeddedFiles = names.Elements.GetDictionary("/EmbeddedFiles");
            if (embeddedFiles == null)
            {
                embeddedFiles = new PdfDictionary(document);
                document.Internals.AddObject(embeddedFiles);
                names.Elements.SetObject("/EmbeddedFiles", embeddedFiles);
            }

            var namesArray = embeddedFiles.Elements.GetArray("/Names");
            if (namesArray == null)
            {
                namesArray = new PdfArray(document);
                embeddedFiles.Elements.SetObject("/Names", namesArray);
            }

            // Add to Names array: Name and Filespec Reference
            namesArray.Elements.Add(new PdfString(OdfRelationName));
            if (filespec.Reference == null)
            {
                throw new CryptographicException("Failed to generate PDF reference for ODF file attachment.");
            }
            namesArray.Elements.Add(filespec.Reference);

            // 4. Inject PDF/A-3 Schema into XMP metadata if Catalog has /Metadata
            InjectPdfAXmpMetadata(document, odfFileName, mimeType);

            // Save modified document
            document.Save(outputPdfStream);
            OdfKitDiagnostics.Info($"Successfully injected ODF '{odfFileName}' as PDF/A-3 attachment into output PDF.");
        }

        private static void InjectPdfAXmpMetadata(PdfDocument document, string odfFileName, string mimeType)
        {
            PdfCatalog catalog = document.Internals.Catalog;
            var metadataRef = catalog.Elements["/Metadata"] as PdfReference;
            var metadataDict = (metadataRef != null ? metadataRef.Value : catalog.Elements["/Metadata"]) as PdfDictionary;

            if (metadataDict == null || metadataDict.Stream == null)
            {
                OdfKitDiagnostics.Info("No existing /Metadata stream in PDF catalog. PDF/A XMP metadata injection skipped.");
                return;
            }

            try
            {
                // Retrieve original XMP XML bytes
                byte[] xmpBytes = metadataDict.Stream.Value;
                string xmpText = Encoding.UTF8.GetString(xmpBytes);

                // Load to XmlDocument
                var xmlDoc = new Xml.XmlDocument();
                var xmlSettings = new Xml.XmlReaderSettings { DtdProcessing = Xml.DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = Xml.XmlReader.Create(new System.IO.StringReader(xmpText), xmlSettings))
                {
                    xmlDoc.Load(reader);
                }

                var nsManager = new Xml.XmlNamespaceManager(xmlDoc.NameTable);
                nsManager.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
                nsManager.AddNamespace("pdfaExtension", "http://www.aiim.org/pdfa/ns/extension/");
                nsManager.AddNamespace("pdfaSchema", "http://www.aiim.org/pdfa/ns/schema#");
                nsManager.AddNamespace("pdfaProperty", "http://www.aiim.org/pdfa/ns/property#");

                var descNode = xmlDoc.SelectSingleNode("//rdf:Description", nsManager);
                if (descNode != null)
                {
                    // Add PDF/A-3 schema declaration if not present
                    // To ensure it passes validators, we inject basic PDF/A attachment schemas
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
                    using (var reader = Xml.XmlReader.Create(new System.IO.StringReader(schemaSnippet), xmlSettings))
                    {
                        tempDoc.Load(reader);
                    }
                    var imported = xmlDoc.ImportNode(tempDoc.DocumentElement!, true);
                    descNode.AppendChild(imported);

                    // Write back updated metadata stream
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
                    OdfKitDiagnostics.Info("Successfully injected PDF/A-3 schema declarations into XMP Metadata.");
                }
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"Failed to update PDF/A XMP metadata stream: {ex.Message}");
            }
        }
    }
}
