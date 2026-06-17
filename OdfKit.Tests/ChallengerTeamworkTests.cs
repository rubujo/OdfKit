using System;
using System.IO;
using System.IO.Compression;
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
    public class ChallengerTeamworkTests
    {
        #region OdsStreamWriter State Boundaries & Ignored Parameters

        [Fact]
        public void TestOdsStreamWriterIgnoredParameters()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                // 1. Pass non-default column width
                writer.WriteColumn(OdfLength.FromCentimeters(5.5), "ColStyle");

                // 2. Pass height and optimal height to row
                writer.WriteStartRow(height: 15.0, styleName: "RowStyle", useOptimalHeight: true);

                writer.WriteCell("Value");
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("content.xml");
            Assert.NotNull(entry);

            using var s = entry.Open();
            using var sr = new StreamReader(s);
            string xml = sr.ReadToEnd();

            // Assert that the width, height, and optimal height values are NOT present in the XML.
            // Since they are ignored in OdsStreamWriter, this test documents this limitation/bug.
            Assert.DoesNotContain("5.5", xml);
            Assert.DoesNotContain("15", xml);
            Assert.DoesNotContain("optimal-row-height", xml);

            // Check that styles were written as attributes, even though they aren't defined anywhere
            Assert.Contains("table:style-name=\"ColStyle\"", xml);
            Assert.Contains("table:style-name=\"RowStyle\"", xml);
        }

        [Fact]
        public void TestOdsStreamWriterStateViolations()
        {
            using var ms = new MemoryStream();

            // Writing cells out of order should not crash the writer but will result in invalid ODF XML schema structure.
            using (var writer = new OdsStreamWriter(ms))
            {
                // Write a cell directly without a sheet or row started
                writer.WriteCell("OrphanCell1");

                writer.WriteStartSheet("Sheet1");
                // Write a cell without a row started
                writer.WriteCell("OrphanCell2");

                writer.WriteStartRow();
                writer.WriteCell("NormalCell");
                writer.WriteEndRow();

                // Write a column AFTER starting/ending a row (invalid ODF schema ordering)
                writer.WriteColumn(OdfLength.FromCentimeters(2.0));

                writer.WriteEndSheet();

                // Write cell after sheet has ended
                writer.WriteCell("OrphanCell3");
            }

            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("content.xml");
            Assert.NotNull(entry);

            using var s = entry.Open();
            using var sr = new StreamReader(s);
            string xml = sr.ReadToEnd();

            // The writer permits these out-of-order calls, which leads to invalid schema structure.
            // Let's document the generated XML structure to prove that no state validation is performed.
            Assert.Contains("<table:table-cell office:value-type=\"string\"><text:p>OrphanCell1</text:p></table:table-cell>", xml);
            Assert.Contains("<table:table-cell office:value-type=\"string\"><text:p>OrphanCell2</text:p></table:table-cell>", xml);
            Assert.Contains("<table:table-cell office:value-type=\"string\"><text:p>OrphanCell3</text:p></table:table-cell>", xml);
            Assert.Contains("<table:table-column", xml); // written after table-row elements
        }

        [Fact]
        public void TestOdsStreamWriterAutoClosingOnDispose()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell("Cell");
                // Dispose without calling WriteEndRow or WriteEndSheet
            }

            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("content.xml");
            Assert.NotNull(entry);

            using var s = entry.Open();
            using var sr = new StreamReader(s);
            string xml = sr.ReadToEnd();

            // Verify that the XML tags are closed properly by Dispose
            Assert.EndsWith("</table:table-row></table:table></office:spreadsheet></office:body></office:document-content>", xml);
        }

        #endregion

        #region OdfComment Multi-thread & Orphans & Deep Nesting

        [Fact]
        public void TestOdfCommentMultiRootsInXml()
        {
            // OdfComment.FromXmlNode takes an annotation-list node.
            // If the XML list has multiple independent comment threads (multiple roots),
            // verify that it only returns the first root, silently discarding the rest.
            var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);

            var thread1Node = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            thread1Node.SetAttribute("name", OdfNamespaces.Office, "T1", "office");
            var creator1 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "Author1" };
            var p1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Thread 1 Root" };
            thread1Node.AppendChild(creator1);
            thread1Node.AppendChild(p1);

            var thread2Node = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            thread2Node.SetAttribute("name", OdfNamespaces.Office, "T2", "office");
            var creator2 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "Author2" };
            var p2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Thread 2 Root" };
            thread2Node.AppendChild(creator2);
            thread2Node.AppendChild(p2);

            container.AppendChild(thread1Node);
            container.AppendChild(thread2Node);

            var rootComment = OdfComment.FromXmlNode(container);
            Assert.NotNull(rootComment);

            // It parses and returns Thread 1 root, but Thread 2 root is completely lost.
            Assert.Equal("T1", rootComment.Name);
            Assert.Equal("Thread 1 Root", rootComment.Text);

            // Confirm thread 2 root is not in replies either
            Assert.Empty(rootComment.Replies);
        }

        [Fact]
        public void TestOdfCommentOrphanedReplies()
        {
            // If the XML annotation list contains a comment that has a parent attribute,
            // but that parent name is not in the list (an orphan reply),
            // verify that it is parsed but silently discarded from the returned tree structure.
            var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);

            var rootNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            rootNode.SetAttribute("name", OdfNamespaces.Office, "RootNode", "office");
            var creator1 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "Author" };
            var p1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Root" };
            rootNode.AppendChild(creator1);
            rootNode.AppendChild(p1);

            var orphanNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            orphanNode.SetAttribute("name", OdfNamespaces.Office, "OrphanNode", "office");
            orphanNode.SetAttribute("annotation-parent", OdfNamespaces.Office, "NonExistentParent", "office");
            var creator2 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "Author" };
            var p2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Orphan reply" };
            orphanNode.AppendChild(creator2);
            orphanNode.AppendChild(p2);

            container.AppendChild(rootNode);
            container.AppendChild(orphanNode);

            var rootComment = OdfComment.FromXmlNode(container);
            Assert.NotNull(rootComment);
            Assert.Equal("RootNode", rootComment.Name);

            // The orphan node is silently discarded
            Assert.Empty(rootComment.Replies);
        }

        [Fact]
        public void TestOdfCommentExtremeNestingDoesNotCrash()
        {
            // Challenge depth limits. Let's build a chain of 5000 replies.
            // Since OdfComment uses iterative algorithms for serialization (Stack-based DFS)
            // and deserialization (Queue-based BFS), it should process depth 5000 easily without StackOverflow.
            var root = new OdfComment("Author", "Root");
            var current = root;
            for (int i = 0; i < 5000; i++)
            {
                var reply = new OdfComment("Author", $"Reply {i}");
                current.AddReply(reply);
                current = reply;
            }

            // Serialize
            var xmlNode = root.ToXmlNode();
            Assert.NotNull(xmlNode);

            // Deserialize
            var parsed = OdfComment.FromXmlNode(xmlNode);
            Assert.NotNull(parsed);

            // Check that the chain is still intact
            var check = parsed;
            int count = 0;
            while (check.Replies.Count > 0)
            {
                count++;
                check = check.Replies[0];
            }
            Assert.Equal(5000, count);
        }

        [Fact]
        public void TestOdfCommentNewlinesRoundTrip()
        {
            // A comment with complex formatting and newlines
            string text = "Line 1\nLine 2\r\nLine 3\n\nLine 4\r\n\r\nLine 5";
            var root = new OdfComment("Author", text);

            var xmlNode = root.ToXmlNode();
            Assert.NotNull(xmlNode);

            var parsed = OdfComment.FromXmlNode(xmlNode);
            Assert.NotNull(parsed);

            // When parsed back, the library represents newlines as '\n' regardless of the original separator ('\r\n' or '\n').
            // Let's assert that the roundtrip resolves all newlines to '\n'.
            string expected = "Line 1\nLine 2\nLine 3\n\nLine 4\n\nLine 5";
            Assert.Equal(expected, parsed.Text);
        }

        #endregion
    }
}
