using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;

namespace OdfKit.Tests
{
    public class Milestone3BugFixesTests
    {
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

            // 1. Create source doc with some styles
            using (var pkg1 = OdfPackage.Create(ms1, leaveOpen: true))
            {
                var srcDoc = new TextDocument(pkg1);
                var styles = srcDoc.StylesDom;
                var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
                styleNode.SetAttribute("name", OdfNamespaces.Style, "Standard");
                styleNode.SetAttribute("family", OdfNamespaces.Style, "paragraph");

                var stylesStyles = FindOrCreateChild(styles, "styles", OdfNamespaces.Office, "office");
                stylesStyles.AppendChild(styleNode);
                srcDoc.Save();
            }

            ms1.Position = 0;

            // 2. Create dest doc
            using (var pkg2 = OdfPackage.Create(ms2, leaveOpen: true))
            {
                var destDoc = new TextDocument(pkg2);
                var styles = destDoc.StylesDom;
                var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
                styleNode.SetAttribute("name", OdfNamespaces.Style, "Standard");
                styleNode.SetAttribute("family", OdfNamespaces.Style, "paragraph");

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

            // Merge src into dest
            dest.AppendDocument(src, options);

            // Verify that conflicting Standard style was renamed to Standard_s1
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
    }
}
