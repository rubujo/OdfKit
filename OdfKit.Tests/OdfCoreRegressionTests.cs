using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;

namespace OdfKit.Tests
{
    [Trait(TestCategories.Kind, TestCategories.Smoke)]
    public class OdfCoreRegressionTests
    {
        [Fact]
        public void OdfCrc32UsesZipIsoHdlcPolynomialAndSupportsIncrementalState()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("123456789");

            uint oneShot = OdfCrc32.Compute(bytes);
            uint state = OdfCrc32.Compute(0xFFFFFFFF, bytes.AsSpan(0, 4));
            state = OdfCrc32.Compute(state, bytes.AsSpan(4));
            uint incremental = state ^ 0xFFFFFFFF;

            Assert.Equal(0xCBF43926u, oneShot);
            Assert.Equal(oneShot, incremental);
            Assert.NotEqual(0xE3069283u, oneShot);
        }

        [Fact]
        public void TestDeepNestingCommentRepliesRecursionPrevention()
        {
            var root = new OdfComment("Author", "Root");
            var current = root;

            // Build a very deep comment chain (160 levels)
            for (int i = 1; i <= 160; i++)
            {
                var reply = new OdfComment($"Author{i}", $"Reply {i}");
                current.AddReply(reply);
                current = reply;
            }

            // Test serialization (should not stack overflow)
            OdfNode xmlContainer = root.ToXmlNode();
            Assert.NotNull(xmlContainer);

            // Test deserialization (should not stack overflow)
            OdfComment deserialized = OdfComment.FromXmlNode(xmlContainer);
            Assert.NotNull(deserialized);
            Assert.Equal("Root", deserialized.Text);

            // Verify the nesting depth of deserialized comment replies is 160
            int depth = 0;
            var check = deserialized;
            while (check.Replies.Count > 0)
            {
                depth++;
                check = check.Replies[0];
            }
            Assert.Equal(160, depth);
        }

        [Fact]
        public void TestDiamondDagAndCycleDetection()
        {
            var root = new OdfComment("Author", "Root");
            var left = new OdfComment("Author", "Left");
            var right = new OdfComment("Author", "Right");
            var bottom = new OdfComment("Author", "Bottom");

            root.AddReply(left);
            root.AddReply(right);
            left.AddReply(bottom);
            right.AddReply(bottom); // Diamond reference

            // 1. Verify DAG serialization works and handles duplication/prevent infinite loop
            OdfNode xmlNode = root.ToXmlNode();
            Assert.NotNull(xmlNode);

            // 2. Cycle detection throws InvalidOperationException
            var c1 = new OdfComment("Author", "C1");
            var c2 = new OdfComment("Author", "C2");
            c1.AddReply(c2);
            c2.AddReply(c1); // Circular dependency

            Assert.Throws<InvalidOperationException>(() => c1.ToXmlNode());
        }

        [Fact]
        public void TestHtmlParsingEnhancements()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // 1. Script/Style filter tests (case-insensitively, with attributes and content)
            string htmlWithScriptsAndStyles =
                "Hello <script type=\"text/javascript\">var x = 1;</script>" +
                "<style>body { color: red; }</style>" +
                "<SCRIPT src=\"foo.js\" />" +
                "<STYLE type=\"text/css\">p { margin: 0; }</STYLE>" +
                "World &lt;&gt;&amp;&quot;&apos;&#39;&#x27;";

            p.AddHtmlFragment(htmlWithScriptsAndStyles);

            // Reconstruct plain text to verify scripts and styles are gone, and entities are decoded
            string plainText = p.Node.TextContent;

            // "Hello " and "World <>&\"''''"
            Assert.Contains("Hello ", plainText);
            Assert.Contains("World <>&\"'''", plainText);

            // Ensure scripts and styles are not present anywhere in the text content
            Assert.DoesNotContain("var x = 1", plainText);
            Assert.DoesNotContain("body { color: red; }", plainText);
            Assert.DoesNotContain("p { margin: 0; }", plainText);
            Assert.DoesNotContain("foo.js", plainText);
        }

        [Fact]
        public void TestStyleMergingAndRemapping()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();

            // 1. 建立帶有來源樣式的文件
            using (var pkg1 = OdfPackage.Create(ms1, leaveOpen: true))
            {
                var srcDoc = new TextDocument(pkg1);
                var styles = srcDoc.StylesDom;
                var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
                styleNode.SetAttribute("name", OdfNamespaces.Style, "Standard");
                styleNode.SetAttribute("family", OdfNamespaces.Style, "paragraph");
                var textProperties = new OdfNode(OdfNodeType.Element, "text-properties", OdfNamespaces.Style, "style");
                textProperties.SetAttribute("color", OdfNamespaces.Fo, "#FF0000", "fo");
                styleNode.AppendChild(textProperties);

                var stylesStyles = FindOrCreateChild(styles, "styles", OdfNamespaces.Office, "office");
                stylesStyles.AppendChild(styleNode);
                srcDoc.Save();
            }

            ms1.Position = 0;

            // 2. 建立帶有同名但語意不同樣式的目標文件
            using (var pkg2 = OdfPackage.Create(ms2, leaveOpen: true))
            {
                var destDoc = new TextDocument(pkg2);
                var styles = destDoc.StylesDom;
                var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
                styleNode.SetAttribute("name", OdfNamespaces.Style, "Standard");
                styleNode.SetAttribute("family", OdfNamespaces.Style, "paragraph");
                var textProperties = new OdfNode(OdfNodeType.Element, "text-properties", OdfNamespaces.Style, "style");
                textProperties.SetAttribute("color", OdfNamespaces.Fo, "#0000FF", "fo");
                styleNode.AppendChild(textProperties);

                var stylesStyles = FindOrCreateChild(styles, "styles", OdfNamespaces.Office, "office");
                stylesStyles.AppendChild(styleNode);
                destDoc.Save();
            }

            ms2.Position = 0;

            using var src = new TextDocument(OdfPackage.Open(ms1));
            using var dest = new TextDocument(OdfPackage.Open(ms2));

            var options = new OdfMergeOptions
            {
                StyleConflictResolution = ConflictResolution.KeepSourceFormatting
            };

            // 將來源文件合併到目標文件
            dest.AppendDocument(src, options);

            // 驗證同名但語意不同的 Standard 樣式會重新命名為 Standard_s1
            var automaticStyles = FindOrCreateChild(dest.StylesDom, "styles", OdfNamespaces.Office, "office");
            bool foundRenamed = false;
            foreach (var child in automaticStyles.Children)
            {
                if (child.LocalName == "style" && child.NamespaceUri == OdfNamespaces.Style)
                {
                    string? name = child.GetAttribute("name", OdfNamespaces.Style);
                    if (name == "Standard_s1")
                    {
                        foundRenamed = true;
                        break;
                    }
                }
            }

            Assert.True(foundRenamed, "Standard style should have been renamed to Standard_s1 to resolve the conflict");
        }

        [Fact]
        public void TestOdsStreamWriterDateTimeBoundaryValues()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell(DateTime.MinValue, timezoneNaive: false);
                writer.WriteCell(DateTime.MaxValue, timezoneNaive: false);
                writer.WriteCell(DateTime.MinValue, timezoneNaive: true);
                writer.WriteCell(DateTime.MaxValue, timezoneNaive: true);
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            // Just running to the end without throwing ArgumentOutOfRangeException is a success
            Assert.True(ms.Length > 0);
        }

        [Fact]
        public void TestOdfDocumentDateTimeBoundaryValues()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);

            // 1. FormatMetaDate / ParseMetaDate
            doc.CreationDate = DateTime.MinValue;
            Assert.Equal(DateTime.MinValue, doc.CreationDate);

            doc.ModificationDate = DateTime.MaxValue;
            Assert.NotNull(doc.ModificationDate);
            var expectedMaxDate = new DateTime(DateTime.MaxValue.Year, DateTime.MaxValue.Month, DateTime.MaxValue.Day, DateTime.MaxValue.Hour, DateTime.MaxValue.Minute, DateTime.MaxValue.Second, DateTimeKind.Utc);
            Assert.Equal(expectedMaxDate, doc.ModificationDate.Value.ToUniversalTime());

            // 2. SetCustomProperty with Date boundaries
            var safeMaxPropDate = new DateTime(9999, 12, 30, 23, 59, 59, DateTimeKind.Utc);
            doc.SetCustomProperty("MinProp", DateTime.MinValue, "date");
            doc.SetCustomProperty("MaxProp", safeMaxPropDate, "date");

            var minPropVal = (DateTime)doc.FindCustomProperty("MinProp")!;
            if (minPropVal != DateTime.MinValue)
            {
                Assert.Equal(DateTime.MinValue, minPropVal.ToUniversalTime());
            }
            else
            {
                Assert.Equal(DateTime.MinValue, minPropVal);
            }
            var maxPropVal = (DateTime)doc.FindCustomProperty("MaxProp")!;
            Assert.Equal(safeMaxPropDate, maxPropVal.ToUniversalTime());
        }

        [Fact]
        public void TestSpreadsheetDocumentDateTimeBoundaryValues()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");

            var cell1 = sheet.GetCell(0, 0);
            cell1.SetValue(DateTime.MinValue, useTimezoneNaive: false);
            Assert.Equal("date", cell1.ValueType);
            Assert.Contains("Z", cell1.Node.GetAttribute("date-value", OdfNamespaces.Office) ?? "");

            var cell2 = sheet.GetCell(0, 1);
            cell2.SetValue(DateTime.MaxValue, useTimezoneNaive: false);
            Assert.Equal("date", cell2.ValueType);
            Assert.Contains("Z", cell2.Node.GetAttribute("date-value", OdfNamespaces.Office) ?? "");

            var cell3 = sheet.GetCell(0, 2);
            cell3.SetValue(DateTime.MinValue, useTimezoneNaive: true);
            Assert.Equal("date", cell3.ValueType);
            Assert.DoesNotContain("Z", cell3.Node.GetAttribute("date-value", OdfNamespaces.Office) ?? "");
        }

        [Fact]
        public void SpreadsheetCellDateValueRoundTripsAsDateTimeAndClearsStaleAttributes()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new SpreadsheetDocument(package);
            var sheet = doc.AddSheet("Sheet1");
            var cell = sheet.GetCell(0, 0);

            cell.SetValue(true);
            var date = new DateTime(2026, 7, 31, 5, 6, 7, DateTimeKind.Utc);
            cell.SetValue(date, useTimezoneNaive: false);

            Assert.Equal("date", cell.ValueType);
            Assert.Equal(date, Assert.IsType<DateTime>(cell.CellValue));
            DateTime roundTrippedDate = cell.GetValue<DateTime>();
            Assert.Equal(date, roundTrippedDate);
            Assert.Equal(DateTimeKind.Utc, roundTrippedDate.Kind);
            Assert.Equal("07/31/2026 05:06:07", cell.GetValue<string>());
            Assert.Null(cell.Node.GetAttribute("boolean-value", OdfNamespaces.Office));
            Assert.Null(cell.Node.GetAttribute("value", OdfNamespaces.Office));

            cell.ValueType = "time";
            cell.Node.SetAttribute("string-value", OdfNamespaces.Office, "stale", "office");
            cell.Node.SetAttribute("time-value", OdfNamespaces.Office, "PT01H30M", "office");
            cell.Node.SetAttribute("currency", OdfNamespaces.Office, "TWD", "office");
            cell.SetValue("plain text");

            Assert.Equal("string", cell.ValueType);
            Assert.Null(cell.Node.GetAttribute("date-value", OdfNamespaces.Office));
            Assert.Null(cell.Node.GetAttribute("string-value", OdfNamespaces.Office));
            Assert.Null(cell.Node.GetAttribute("time-value", OdfNamespaces.Office));
            Assert.Null(cell.Node.GetAttribute("currency", OdfNamespaces.Office));
            Assert.Equal("plain text", cell.CellValue);
        }

        [Fact]
        public void SpreadsheetCellCurrencyValueWritesCurrencyMetadataAndRoundTripsThroughReader()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Sheet1");
                var cell = sheet.GetCell(0, 0);

                cell.SetCurrencyValue(1234.50m, "twd", "NT$1,234.50");

                Assert.Equal("currency", cell.ValueType);
                Assert.Equal("1234.50", cell.RawValue);
                Assert.Equal("TWD", cell.CurrencyCode);
                Assert.Equal("NT$1,234.50", cell.DisplayText);
                cell.Node.SetAttribute("currency", OdfNamespaces.Office, " twd ", "office");
                Assert.Equal("TWD", cell.CurrencyCode);
                // getter 正規化僅發生於讀取時，不寫回底層屬性。
                Assert.Equal(" twd ", cell.Node.GetAttribute("currency", OdfNamespaces.Office));
                // 必須透過 setter 寫回，底層 XML 屬性才會更新為正規化值。
                cell.CurrencyCode = "twd";
                Assert.Equal("TWD", cell.Node.GetAttribute("currency", OdfNamespaces.Office));

                // 寫入驗證 ISO 4217 形狀（三個 ASCII 字母），但不比對現行代碼清單。
                Assert.Throws<ArgumentException>(() => cell.CurrencyCode = "US");
                Assert.Throws<ArgumentException>(() => cell.CurrencyCode = "USDX");
                Assert.Throws<ArgumentException>(() => cell.CurrencyCode = "12A");
                Assert.Throws<ArgumentException>(() => cell.SetCurrencyValue(1m, "臺幣"));
                cell.CurrencyCode = "xts";   // 測試用代碼不得被拒絕
                Assert.Equal("XTS", cell.CurrencyCode);
                cell.CurrencyCode = "TWD";

                doc.Save();
            }

            ms.Position = 0;
            using var reader = new OdsStreamReader(ms, new OdsStreamReaderOptions { LeaveOpen = true });
            Assert.True(reader.Read());
            OdsCellValue cellValue = reader.GetCell(0);
            Assert.Equal(OdsCellValueKind.Currency, cellValue.Kind);
            Assert.Equal(1234.5d, Assert.IsType<double>(cellValue.Value));
            Assert.Equal("TWD", cellValue.Currency);
            Assert.Equal("NT$1,234.50", cellValue.DisplayText);
        }

        [Fact]
        public void ResetMmfLoadStateDisposesRegisteredEntriesBeforeClearingThem()
        {
            using var package = OdfPackage.Create(new MemoryStream(), leaveOpen: true);
            OdfPackage.OdfPackageLoadCollaborators ctx = package.LoadCollaborators;
            var trackedStream = new TrackingMemoryStream();
            ctx.Entries["content.xml"] = new OdfPackageEntry("content.xml", trackedStream);
            ctx.EntryOrder.Add("content.xml");
            ctx.DuplicateEntryNames.Add("content.xml");
            package.Mmf = MemoryMappedFile.CreateNew(null, 4096);
            package.MmfEntries = [];
            package.PreloadTask = Task.CompletedTask;

            MethodInfo resetMethod = typeof(OdfPackageZipLoader).GetMethod(
                "ResetMmfLoadState",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            resetMethod.Invoke(null, new object?[] { package, ctx });

            Assert.True(trackedStream.DisposeCalled);
            Assert.Null(package.Mmf);
            Assert.Null(package.MmfEntries);
            Assert.Null(package.PreloadTask);
            Assert.Empty(ctx.Entries);
            Assert.Empty(ctx.EntryOrder);
            Assert.Empty(ctx.DuplicateEntryNames);
        }

        [Fact]
        public void OdfPackageDisposeReleasesEntriesWhenUnderlyingDisposeFails()
        {
            var underlying = new ThrowingDisposeMemoryStream();
            var package = OdfPackage.Create(underlying, leaveOpen: false);
            var entryStream = new TrackingMemoryStream();
            package.LoadCollaborators.Entries["content.xml"] = new OdfPackageEntry("content.xml", entryStream);

            IOException exception = Assert.Throws<IOException>(package.Dispose);

            Assert.Equal("Expected dispose failure.", exception.Message);
            Assert.True(entryStream.DisposeCalled);
            package.Dispose();
        }

        [Fact]
        public async Task OdfPackageDisposeAsyncReleasesEntriesWhenUnderlyingDisposeFails()
        {
            var underlying = new ThrowingAsyncDisposeMemoryStream();
            var package = OdfPackage.Create(underlying, leaveOpen: false);
            var entryStream = new TrackingMemoryStream();
            package.LoadCollaborators.Entries["content.xml"] = new OdfPackageEntry("content.xml", entryStream);

            IOException exception = await Assert.ThrowsAsync<IOException>(async () => await package.DisposeAsync());

            Assert.Equal("Expected async dispose failure.", exception.Message);
            Assert.True(entryStream.DisposeCalled);
            await package.DisposeAsync();
        }

        [Fact]
        public void OdfPackageFallsBackToBclZipWhenMmfLoadResetsMidStream()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"odfkit_mmf_fallback_{Guid.NewGuid():N}.odt");
            try
            {
                byte[] contentXml = Encoding.UTF8.GetBytes("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body/></office:document-content>");
                byte[] stylesXml = Encoding.UTF8.GetBytes("<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>");
                byte[] manifestXml = Encoding.UTF8.GetBytes("""
                    <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0">
                      <manifest:file-entry manifest:full-path="/" manifest:media-type="application/vnd.oasis.opendocument.text" />
                      <manifest:file-entry manifest:full-path="content.xml" manifest:media-type="text/xml" />
                      <manifest:file-entry manifest:full-path="styles.xml" manifest:media-type="text/xml" />
                    </manifest:manifest>
                    """);
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    WriteZipEntry(archive, "mimetype", Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.text"));
                    WriteZipEntry(archive, "content.xml", contentXml);
                    WriteZipEntry(archive, "styles.xml", stylesXml);
                    WriteZipEntry(archive, "META-INF/manifest.xml", manifestXml);
                }

                OdfPackageZipLoader.MmfLoadFailureInjectorForTestContext = count =>
                    count == 1 ? new SecurityException("Injected MMF load failure for fallback regression test.") : null;

                using OdfPackage package = OdfPackage.Open(tempPath, new OdfLoadOptions { AllowLazyLoading = true });
                Assert.Null(package.Mmf);
                Assert.Null(package.MmfEntries);
                Assert.Equal("application/vnd.oasis.opendocument.text", package.MimeType);
                Assert.Equal(contentXml, package.ReadEntry("content.xml"));
                Assert.Equal(stylesXml, package.ReadEntry("styles.xml"));
                // 回退至 BCL 路徑後，MMF 期間已部分登錄的 entry 應被清空；
                // 確認沒有因重複登錄而產生假重複。
                Assert.Empty(package.DuplicateEntryNames);
            }
            finally
            {
                OdfPackageZipLoader.MmfLoadFailureInjectorForTestContext = null;
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void TestOdfCommentDateTimeBoundaryValues()
        {
            var commentMin = new OdfComment("Author", "MinValText", DateTime.MinValue, "c_min");
            // Use 1 day before MaxValue to avoid year 10000 overflow when converting to local time in positive timezone offsets
            var maxDate = new DateTime(9999, 12, 30, 23, 59, 59, DateTimeKind.Utc);
            var commentMax = new OdfComment("Author", "MaxValText", maxDate, "c_max");

            var nodeMin = commentMin.ToXmlNode();
            var parsedMin = OdfComment.FromXmlNode(nodeMin);
            Assert.Equal(DateTime.MinValue, parsedMin.Date);

            var nodeMax = commentMax.ToXmlNode();
            var parsedMax = OdfComment.FromXmlNode(nodeMax);
            Assert.Equal(maxDate, parsedMax.Date);
        }

        private static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
            parent.AppendChild(node);
            return node;
        }

        private static void WriteZipEntry(ZipArchive archive, string name, byte[] content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
            using Stream stream = entry.Open();
            stream.Write(content, 0, content.Length);
        }

        private sealed class TrackingMemoryStream : MemoryStream
        {
            public bool DisposeCalled { get; private set; }

            protected override void Dispose(bool disposing)
            {
                DisposeCalled = true;
                base.Dispose(disposing);
            }
        }

        private sealed class ThrowingDisposeMemoryStream : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                throw new IOException("Expected dispose failure.");
            }
        }

        private sealed class ThrowingAsyncDisposeMemoryStream : MemoryStream
        {
            public override async ValueTask DisposeAsync()
            {
                await base.DisposeAsync();
                throw new IOException("Expected async dispose failure.");
            }
        }

        /// <summary>
        /// 驗證 <see cref="OdfVersionInfo.ToVersionString"/> 能將所有已知 <see cref="OdfVersion"/>
        /// 列舉值轉換為對應的規格版本字串，未知值則回退為 "unknown"。
        /// </summary>
        [Fact]
        public void TestOdfVersionInfoToVersionStringCoversAllKnownVersions()
        {
            Assert.Equal("1.0", OdfVersionInfo.ToVersionString(OdfVersion.Odf10));
            Assert.Equal("1.1", OdfVersionInfo.ToVersionString(OdfVersion.Odf11));
            Assert.Equal("1.2", OdfVersionInfo.ToVersionString(OdfVersion.Odf12));
            Assert.Equal("1.3", OdfVersionInfo.ToVersionString(OdfVersion.Odf13));
            Assert.Equal("1.4", OdfVersionInfo.ToVersionString(OdfVersion.Odf14));
            Assert.Equal("unknown", OdfVersionInfo.ToVersionString(OdfVersion.Unknown));
        }

        /// <summary>
        /// 驗證 <see cref="OdfVersionInfo.TryParseVersionString"/> 能正確解析所有已知版本字串，
        /// 且對於 <see langword="null"/>、空字串或未知字串正確回傳 <see langword="false"/> 與
        /// <see cref="OdfVersion.Unknown"/>，並與 <see cref="OdfVersionInfo.ToVersionString"/> 互為反函式。
        /// </summary>
        [Fact]
        public void TestOdfVersionInfoTryParseVersionStringRoundTripsAndRejectsUnknown()
        {
            Assert.True(OdfVersionInfo.TryParseVersionString("1.0", out OdfVersion v10));
            Assert.Equal(OdfVersion.Odf10, v10);
            Assert.True(OdfVersionInfo.TryParseVersionString("1.4", out OdfVersion v14));
            Assert.Equal(OdfVersion.Odf14, v14);

            Assert.False(OdfVersionInfo.TryParseVersionString("9.9", out OdfVersion invalid));
            Assert.Equal(OdfVersion.Unknown, invalid);

            Assert.False(OdfVersionInfo.TryParseVersionString(null, out OdfVersion fromNull));
            Assert.Equal(OdfVersion.Unknown, fromNull);

            Assert.False(OdfVersionInfo.TryParseVersionString(string.Empty, out OdfVersion fromEmpty));
            Assert.Equal(OdfVersion.Unknown, fromEmpty);

            foreach (OdfVersion known in new[] { OdfVersion.Odf10, OdfVersion.Odf11, OdfVersion.Odf12, OdfVersion.Odf13, OdfVersion.Odf14 })
            {
                string text = OdfVersionInfo.ToVersionString(known);
                Assert.True(OdfVersionInfo.TryParseVersionString(text, out OdfVersion roundTripped));
                Assert.Equal(known, roundTripped);
            }
        }

        /// <summary>
        /// 驗證 <see cref="OdfMediaManager.DetectImageFormat"/> 能依幻數正確辨識 PNG／JPEG／GIF／WebP／
        /// BMP／TIFF（小端與大端）／EMF／WMF／SVG 等全部支援格式，且對無法識別的位元組正確回退為
        /// <c>application/octet-stream</c>。
        /// </summary>
        [Fact]
        public void TestOdfMediaManagerDetectImageFormatCoversAllKnownMagicBytes()
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
            OdfMediaManager.DetectImageFormat(png, out string pngMime, out string pngExt);
            Assert.Equal("image/png", pngMime);
            Assert.Equal(".png", pngExt);

            byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
            OdfMediaManager.DetectImageFormat(jpeg, out string jpegMime, out string jpegExt);
            Assert.Equal("image/jpeg", jpegMime);
            Assert.Equal(".jpg", jpegExt);

            byte[] gif = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];
            OdfMediaManager.DetectImageFormat(gif, out string gifMime, out string gifExt);
            Assert.Equal("image/gif", gifMime);
            Assert.Equal(".gif", gifExt);

            byte[] webp = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
            OdfMediaManager.DetectImageFormat(webp, out string webpMime, out string webpExt);
            Assert.Equal("image/webp", webpMime);
            Assert.Equal(".webp", webpExt);

            byte[] bmp = [0x42, 0x4D, 0x00, 0x00];
            OdfMediaManager.DetectImageFormat(bmp, out string bmpMime, out string bmpExt);
            Assert.Equal("image/bmp", bmpMime);
            Assert.Equal(".bmp", bmpExt);

            byte[] tiffLittleEndian = [0x49, 0x49, 0x2A, 0x00];
            OdfMediaManager.DetectImageFormat(tiffLittleEndian, out string tiffLeMime, out string tiffLeExt);
            Assert.Equal("image/tiff", tiffLeMime);
            Assert.Equal(".tiff", tiffLeExt);

            byte[] tiffBigEndian = [0x4D, 0x4D, 0x00, 0x2A];
            OdfMediaManager.DetectImageFormat(tiffBigEndian, out string tiffBeMime, out string tiffBeExt);
            Assert.Equal("image/tiff", tiffBeMime);
            Assert.Equal(".tiff", tiffBeExt);

            byte[] emf = new byte[44];
            emf[0] = 0x01;
            emf[40] = 0x20;
            emf[41] = 0x45;
            emf[42] = 0x4D;
            emf[43] = 0x46;
            OdfMediaManager.DetectImageFormat(emf, out string emfMime, out string emfExt);
            Assert.Equal("image/x-emf", emfMime);
            Assert.Equal(".emf", emfExt);

            byte[] wmfPlaceable = [0xD7, 0xCD, 0xC6, 0x9A];
            OdfMediaManager.DetectImageFormat(wmfPlaceable, out string wmfMime, out string wmfExt);
            Assert.Equal("image/x-wmf", wmfMime);
            Assert.Equal(".wmf", wmfExt);

            byte[] svg = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            OdfMediaManager.DetectImageFormat(svg, out string svgMime, out string svgExt);
            Assert.Equal("image/svg+xml", svgMime);
            Assert.Equal(".svg", svgExt);

            byte[] unrecognized = [0x00, 0x01, 0x02, 0x03];
            OdfMediaManager.DetectImageFormat(unrecognized, out string fallbackMime, out string fallbackExt);
            Assert.Equal("application/octet-stream", fallbackMime);
            Assert.Equal(".bin", fallbackExt);
        }
    }
}
