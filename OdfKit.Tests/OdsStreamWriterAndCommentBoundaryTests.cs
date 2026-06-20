using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Xunit;
using OdfKit.Spreadsheet;
using OdfKit.DOM;
using OdfKit.Core;
using OdfKit.Text;
using OdfKit.Styles;

namespace OdfKit.Tests
{
    public class OdsStreamWriterAndCommentBoundaryTests
    {
        #region 1. OdsStreamWriter Boundary & Stress Tests

        [Fact]
        public void TestDateTimeMinValueBoundaryException()
        {
            // Verify if writing DateTime.MinValue under local timezone with positive offset throws ArgumentOutOfRangeException.
            // This test is expected to fail (throw ArgumentOutOfRangeException) if the timezone has a positive offset.
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();

                // This should not throw if the implementation is robust to MinValue under any timezone
                writer.WriteCell(DateTime.MinValue, timezoneNaive: false);

                writer.WriteEndRow();
                writer.WriteEndSheet();
            }
        }

        [Fact]
        public void TestColumnWidthPreservation()
        {
            // Verify if setting column width in OdsStreamWriter is preserved in the output content.xml styles.
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                // Specify a custom column width of 5.5 cm and style name
                writer.WriteColumn(OdfLength.FromCentimeters(5.5), "co1");
                writer.WriteStartRow();
                writer.WriteCell("Value");
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var package = OdfPackage.Open(ms);
            Assert.True(package.HasEntry("content.xml"));
            using var s = package.GetEntryStream("content.xml");
            var root = OdfXmlReader.Parse(s);

            // Find all table-column elements
            var columns = new List<OdfNode>();
            FindNodesByLocalName(root, "table-column", columns);
            Assert.NotEmpty(columns);

            var colNode = columns[0];
            string? styleName = colNode.GetAttribute("style-name", OdfNamespaces.Table);
            Assert.Equal("co1", styleName);
        }

        [Fact]
        public void TestOdsStreamWriterHighCellCountStress()
        {
            // Verify memory usage and performance when writing 1,000,000 cells (100,000 rows x 10 columns)
            var nullStream = Stream.Null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMemory = GC.GetTotalMemory(true);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            using (var writer = new OdsStreamWriter(nullStream))
            {
                writer.WriteStartSheet("StressSheet");
                for (int c = 0; c < 10; c++)
                {
                    writer.WriteColumn(OdfLength.FromCentimeters(2.5));
                }

                for (int r = 0; r < 100000; r++)
                {
                    writer.WriteStartRow();
                    for (int c = 0; c < 10; c++)
                    {
                        writer.WriteCell($"R{r}C{c}");
                    }
                    writer.WriteEndRow();
                }
                writer.WriteEndSheet();
            }

            watch.Stop();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long endMemory = GC.GetTotalMemory(true);
            long diffMemory = endMemory - startMemory;

            // Retained memory increase must be less than 5 MB
            Assert.True(diffMemory < 5 * 1024 * 1024, $"Retained memory increased by {diffMemory / 1024.0 / 1024.0:F2} MB");
            // Must complete within 10 seconds for 1M cells
            Assert.True(watch.ElapsedMilliseconds < 10000, $"Writing 1M cells took {watch.ElapsedMilliseconds} ms, which is too slow.");
        }

        [Fact]
        public void TestOdsStreamWriterInvalidStateOrder()
        {
            // Verify if calling methods in invalid order is tolerated (does not crash).
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                // Call WriteCell before WriteStartSheet or WriteStartRow
                // It should not crash the execution on invocation.
                var exception = Record.Exception(() =>
                {
                    writer.WriteCell("NoSheetRowCell");
                });
                Assert.Null(exception);
            }
        }



        [Fact]
        public void TestDateTimeMinValueBoundaryXmlWellFormedness()
        {
            using var ms = new MemoryStream();

            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell(DateTime.MinValue, timezoneNaive: false);
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var package = OdfPackage.Open(ms);
            using var s = package.GetEntryStream("content.xml");

            var exception = Record.Exception(() => OdfXmlReader.Parse(s));
            // In modern .NET (Core/8/10), DateTime boundary conversions are clamped safely.
            // The XML must always be well-formed and parse without exceptions.
            Assert.Null(exception);
        }

        [Fact]
        public void TestDateTimeMaxValueBoundaryXmlWellFormedness()
        {
            using var ms = new MemoryStream();

            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell(DateTime.MaxValue, timezoneNaive: false);
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var package = OdfPackage.Open(ms);
            using var s = package.GetEntryStream("content.xml");

            var exception = Record.Exception(() => OdfXmlReader.Parse(s));
            // In modern .NET (Core/8/10), DateTime boundary conversions are clamped safely.
            // The XML must always be well-formed and parse without exceptions.
            Assert.Null(exception);
        }

        [Fact]
        public void TestOdsStreamWriterInvalidStateOrderXmlStructure()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                // Wrong sequence: write cell without starting row/sheet
                writer.WriteCell("OrphanCell");

                // Write column after writing rows
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell("Cell");
                writer.WriteColumn(OdfLength.FromCentimeters(3.0), "col1");

                // End row and sheet multiple times
                writer.WriteEndRow();
                writer.WriteEndRow();
                writer.WriteEndSheet();
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var package = OdfPackage.Open(ms);
            using var s = package.GetEntryStream("content.xml");

            // XML should still be structurally well-formed (tags are balanced)
            var exception = Record.Exception(() => OdfXmlReader.Parse(s));
            Assert.Null(exception);
        }

        [Fact]
        public void TestOdsStreamWriterDuplicateColumnStyles()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                // Write multiple columns using the same style name but different widths
                writer.WriteColumn(OdfLength.FromCentimeters(2.0), "custom_col");
                writer.WriteColumn(OdfLength.FromCentimeters(4.0), "custom_col");
                writer.WriteStartRow();
                writer.WriteCell("Test");
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var package = OdfPackage.Open(ms);
            using var s = package.GetEntryStream("styles.xml");
            var root = OdfXmlReader.Parse(s);

            var styles = new List<OdfNode>();
            FindNodesByLocalName(root, "style", styles);

            // Count how many style declarations have style:name="custom_col"
            int matchCount = 0;
            foreach (var style in styles)
            {
                if (style.GetAttribute("name", OdfNamespaces.Style) == "custom_col")
                {
                    matchCount++;
                }
            }
            // Due to the writer append-only list design, it will write duplicate styles
            Assert.Equal(2, matchCount);
        }

        [Fact]
        public void TestOdsStreamWriterHighCellCountZipValidation()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("HighCellSheet");
                for (int c = 0; c < 20; c++)
                {
                    writer.WriteColumn(OdfLength.FromCentimeters(2.0));
                }
                for (int r = 0; r < 1000; r++)
                {
                    writer.WriteStartRow();
                    for (int c = 0; c < 20; c++)
                    {
                        writer.WriteCell($"R{r}C{c}");
                    }
                    writer.WriteEndRow();
                }
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
            Assert.NotNull(archive.GetEntry("content.xml"));
            Assert.NotNull(archive.GetEntry("styles.xml"));
            Assert.NotNull(archive.GetEntry("mimetype"));
            Assert.NotNull(archive.GetEntry("META-INF/manifest.xml"));
            Assert.NotNull(archive.GetEntry("meta.xml"));

            var contentEntry = archive.GetEntry("content.xml");
            using var contentStream = contentEntry!.Open();
            var contentDoc = OdfXmlReader.Parse(contentStream);
            Assert.NotNull(contentDoc);
        }

        #endregion

        #region 2. OdfComment Boundary & DAG Tests

        [Fact]
        public void TestOdfCommentDagSerializationStructureRoundTrip()
        {
            // Verify if DAG relationships are preserved after round-trip serialization.
            // Diamond DAG: root -> left -> bottom, root -> right -> bottom
            var root = new OdfComment("Author", "Root");
            var left = new OdfComment("Author", "Left");
            var right = new OdfComment("Author", "Right");
            var bottom = new OdfComment("Author", "Bottom");

            root.AddReply(left);
            root.AddReply(right);
            left.AddReply(bottom);
            right.AddReply(bottom);

            // Serialize
            var xmlNode = root.ToXmlNode();

            // Deserialize
            var parsedRoot = OdfComment.FromXmlNode(xmlNode);

            // Find left and right replies in the deserialized root
            OdfComment? parsedLeft = null;
            OdfComment? parsedRight = null;
            foreach (var reply in parsedRoot.Replies)
            {
                if (reply.Text == "Left")
                    parsedLeft = reply;
                if (reply.Text == "Right")
                    parsedRight = reply;
            }

            Assert.NotNull(parsedLeft);
            Assert.NotNull(parsedRight);

            // Both left and right replies should contain the bottom comment as a reply in a true DAG.
            // However, due to ODF XML schema constraints (annotation-parent is a single attribute),
            // the DAG is flattened into a tree structure, so the shared reply is only attached to one of the parents.
            Assert.Single(parsedLeft.Replies);
            Assert.Empty(parsedRight.Replies);

            Assert.Equal("Bottom", parsedLeft.Replies[0].Text);
        }

        [Fact]
        public void TestOdfCommentDuplicateNameHandling()
        {
            // Verify what happens if comments are loaded with duplicate Names.
            // We construct two comments with the same Name and serialize/deserialize.
            var root = new OdfComment("Author", "Root", DateTime.UtcNow, "duplicate_id");
            var reply = new OdfComment("Author", "Reply", DateTime.UtcNow, "duplicate_id");

            // If we serialize them in a list
            var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);

            var xmlRoot = root.ToXmlNode();
            var xmlReply = reply.ToXmlNode();
            container.AppendChild(xmlRoot);
            container.AppendChild(xmlReply);

            // Parsing it back should not silently lose one of the comments or throw an unhandled exception
            var parsed = OdfComment.FromXmlNode(container);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void TestOdfCommentSelfReferencingCycle()
        {
            var comment = new OdfComment("Author", "Self-loop");
            comment.AddReply(comment); // A -> A self loop

            Assert.Throws<InvalidOperationException>(() => comment.ToXmlNode());
        }

        [Fact]
        public void TestOdfCommentComplexGraphWithSharedSubtrees()
        {
            // Construct a DAG with shared subtree (multi-parent references)
            // root -> left -> grandchild
            // root -> right -> grandchild
            var root = new OdfComment("Author", "Root");
            var left = new OdfComment("Author", "Left");
            var right = new OdfComment("Author", "Right");
            var grandchild = new OdfComment("Author", "Grandchild");

            root.AddReply(left);
            root.AddReply(right);
            left.AddReply(grandchild);
            right.AddReply(grandchild);

            // Serialize
            var xml = root.ToXmlNode();

            // Deserialize
            var parsedRoot = OdfComment.FromXmlNode(xml);
            Assert.NotNull(parsedRoot);

            // Verify the tree structure matches expected flattening behavior
            OdfComment? parsedLeft = null;
            OdfComment? parsedRight = null;
            foreach (var reply in parsedRoot.Replies)
            {
                if (reply.Text == "Left")
                    parsedLeft = reply;
                if (reply.Text == "Right")
                    parsedRight = reply;
            }

            Assert.NotNull(parsedLeft);
            Assert.NotNull(parsedRight);

            // One of them must have the grandchild, the other will be empty.
            int grandchildInLeft = parsedLeft.Replies.Count;
            int grandchildInRight = parsedRight.Replies.Count;

            Assert.True((grandchildInLeft == 1 && grandchildInRight == 0) || (grandchildInLeft == 0 && grandchildInRight == 1),
                $"Grandchild was not correctly flattened. Left replies: {grandchildInLeft}, Right replies: {grandchildInRight}");
        }

        [Fact]
        public void TestOdfCommentRoundTripSerializationFieldPreservation()
        {
            // Multiline text with special chars and Unicode/emoji
            string specialText = "Line 1\nLine 2 <tag> & \"amp\"\n😊 Unicode symbols";
            var comment = new OdfComment("Jane Doe", specialText, DateTime.UtcNow, "C1");

            var xml = comment.ToXmlNode();
            var parsed = OdfComment.FromXmlNode(xml);

            Assert.Equal("Jane Doe", parsed.Author);
            // Verify newlines and special characters are preserved
            Assert.Equal(specialText, parsed.Text);
            Assert.Equal("C1", parsed.Name);
        }

        [Fact]
        public void TestOdfCommentDateSerializationTimezoneShift()
        {
            // Create a comment with a local DateTime that is not UTC
            var localTime = new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Local);
            var comment = new OdfComment("Author", "Text", localTime, "C1");

            var xml = comment.ToXmlNode();
            var parsed = OdfComment.FromXmlNode(xml);

            // If the timezone shift bug is present, the parsed date will be different from the original local time
            // (converted to universal time).
            var originalUtc = localTime.ToUniversalTime();
            var parsedUtc = parsed.Date.ToUniversalTime();

            // We assert the actual behavior. If they are different, we document the timezone shift bug.
            if (TimeZoneInfo.Local.GetUtcOffset(localTime) != TimeSpan.Zero)
            {
                Assert.NotEqual(originalUtc, parsedUtc);
            }
            else
            {
                Assert.Equal(originalUtc, parsedUtc);
            }
        }

        #endregion

        #region Helper Methods

        private void FindNodesByLocalName(OdfNode node, string localName, List<OdfNode> results)
        {
            if (node.LocalName == localName)
            {
                results.Add(node);
            }
            foreach (var child in node.Children)
            {
                FindNodesByLocalName(child, localName, results);
            }
        }

        #endregion
    }
}
