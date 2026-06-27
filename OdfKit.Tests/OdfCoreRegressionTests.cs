using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;

namespace OdfKit.Tests
{
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

            var minPropVal = (DateTime)doc.GetCustomProperty("MinProp")!;
            if (minPropVal != DateTime.MinValue)
            {
                Assert.Equal(DateTime.MinValue, minPropVal.ToUniversalTime());
            }
            else
            {
                Assert.Equal(DateTime.MinValue, minPropVal);
            }
            var maxPropVal = (DateTime)doc.GetCustomProperty("MaxProp")!;
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
