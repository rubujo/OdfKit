using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Xunit;
using OdfKit.Spreadsheet;
using OdfKit.DOM;
using OdfKit.Core;
using OdfKit.Text;
using OdfKit.Styles;
using OdfKit.Formula;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace OdfKit.Tests
{
    public class OdfBoundaryRegressionTests
    {
        [Fact]
        public void TestOdsStreamWriterDateTimeLimits()
        {
            using var ms = new MemoryStream();

            // Test 1: timezoneNaive = true does not convert to UTC and should not throw on MinValue/MaxValue
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell(DateTime.MinValue, timezoneNaive: true);
                writer.WriteCell(DateTime.MaxValue, timezoneNaive: true);
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            // Test 2: timezoneNaive = false calls ToUniversalTime().
            // Under positive timezone offset (e.g. UTC+8), converting 1am and 2am to universal time
            // both result in 0001-01-01T00:00:00.0000000Z due to .NET's silent capping.
            // This causes silent data corruption/loss of precision rather than throwing.
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            if (offset.Ticks > 0)
            {
                var dt1 = DateTime.MinValue.AddHours(1);
                var dt2 = DateTime.MinValue.AddHours(2);
                Assert.Equal(dt1.ToUniversalTime(), dt2.ToUniversalTime()); // silent data corruption!
            }
        }

        [Fact]
        public void TestOdsStreamWriterDoubleDispose()
        {
            var ms = new MemoryStream();
            var writer = new OdsStreamWriter(ms);
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("Hello");
            writer.WriteEndRow();
            writer.WriteEndSheet();

            // First dispose should succeed
            writer.Dispose();

            // Second dispose should be idempotent and not throw
            writer.Dispose();
        }

        [Fact]
        public void TestOdsStreamWriterNullValueCell()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell((string)null!); // Does not throw ArgumentNullException
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            // Verify it writes empty paragraph and office:value-type="string"
            ms.Position = 0;
            using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry("content.xml");
                Assert.NotNull(entry);
                using var s = entry.Open();
                using var sr = new StreamReader(s);
                string xml = sr.ReadToEnd();
                Assert.Contains("office:value-type=\"string\"", xml);
                Assert.Contains("<text:p", xml);
            }
        }

        [Fact]
        public void TestOdsStreamWriterStateChecks()
        {
            using var ms = new MemoryStream();
            using var writer = new OdsStreamWriter(ms);

            // Writing a cell without starting a sheet or row does not throw, but generates malformed XML.
            // Let's verify that it executes without exception (but writes invalid nested XML structure).
            writer.WriteCell("OrphanCell");
        }

        [Fact]
        public void TestOdfCommentSelfReferenceAndDiamond()
        {
            // Self-reference comment: c1 -> c1
            var c1 = new OdfComment("Author1", "Comment 1");
            c1.AddReply(c1);

            // ToXmlNode should throw InvalidOperationException
            Assert.Throws<InvalidOperationException>(() => c1.ToXmlNode());

            // Diamond reference: root -> reply1 -> shared, root -> reply2 -> shared
            var root = new OdfComment("Author", "Root");
            var reply1 = new OdfComment("Author", "Reply1");
            var reply2 = new OdfComment("Author", "Reply2");
            var shared = new OdfComment("Author", "Shared");

            root.AddReply(reply1);
            root.AddReply(reply2);
            reply1.AddReply(shared);
            reply2.AddReply(shared);

            // This should not throw circular reference because it's a DAG, not a cycle.
            // Ensure DAG (diamond) configurations are supported and avoid duplicate serialization of same comment.
            var xmlNode = root.ToXmlNode();
            Assert.NotNull(xmlNode);

            // Find shared comments in the tree - should only be serialized once (count is 2: 1 for annotation, 1 for p text)
            int count = CountNodesWithText(xmlNode, "Shared");
            Assert.Equal(2, count);
        }

        private static int CountNodesWithText(OdfNode node, string text)
        {
            int count = 0;
            if (node.TextContent == text)
                count++;
            foreach (var child in node.Children)
            {
                count += CountNodesWithText(child, text);
            }
            return count;
        }

        [Fact]
        public void TestOdfMailMergeFormulaShiftingBuggyTokenizer()
        {
            // Case 1: named range starts with coordinate letters + numbers + underscore (e.g. A100_Name)
            // The formula translator should NOT split A100_Name, so it remains unchanged, while B2 shifts to B3.
            string formula1 = "=A100_Name + B2";
            string shifted1 = OdfFormulaTranslator.TranslateFormulaOffset(formula1, 1, 0);

            Assert.Equal("=A100_Name + B3", shifted1);

            // Case 2: identifier looks like cell coordinate (e.g. SUM1 or TRUE1)
            // SUM1 has 3 letters (<=3), so it shifts to SUM2. TRUE1 has 4 letters (>3), so it remains unchanged.
            string formula2 = "=SUM1 + TRUE1";
            string shifted2 = OdfFormulaTranslator.TranslateFormulaOffset(formula2, 1, 0);

            Assert.Equal("=SUM2 + TRUE1", shifted2);
        }

        [Fact]
        public void TestOdsStreamWriterStress()
        {
            // Verify streaming high cell count is fast and uses minimal memory growth
            int rowCount = 20000;
            int colCount = 10; // 200,000 cells total

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long memoryBefore = GC.GetTotalMemory(true);

            var stopwatch = Stopwatch.StartNew();

            using (var writer = new OdsStreamWriter(Stream.Null))
            {
                writer.WriteStartSheet("StressSheet");
                for (int r = 0; r < rowCount; r++)
                {
                    writer.WriteStartRow();
                    for (int c = 0; c < colCount; c++)
                    {
                        writer.WriteCell("Cell");
                    }
                    writer.WriteEndRow();
                }
                writer.WriteEndSheet();
            }

            stopwatch.Stop();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long memoryAfter = GC.GetTotalMemory(true);
            long memoryGrowth = memoryAfter - memoryBefore;

            // Performance assertion: 200,000 cells should write in less than 2 seconds
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Writing took too long: {stopwatch.ElapsedMilliseconds} ms");

            // Memory assertion: memory growth should be minimal (e.g. less than 15 MB) since it is streamed
            // Note: GC memory may fluctuate, so we keep the threshold reasonable, but it should be very low.
            Assert.True(memoryGrowth < 15 * 1024 * 1024, $"Memory growth too high: {memoryGrowth / (1024.0 * 1024.0):F2} MB");
        }

        [Fact]
        public void TestOdfCommentStressAndDeepNesting()
        {
            // 1. Flat replies stress test
            var root = new OdfComment("Author", "Root");
            for (int i = 0; i < 5000; i++)
            {
                root.AddReply("Author", $"Reply {i}");
            }

            var sw = Stopwatch.StartNew();
            var xmlNode = root.ToXmlNode();
            sw.Stop();

            Assert.NotNull(xmlNode);
            Assert.True(sw.ElapsedMilliseconds < 1000, $"Flat replies serialization took too long: {sw.ElapsedMilliseconds} ms");

            // 2. Cycle detection with larger indirect cycles
            // c1 -> c2 -> c3 -> c4 -> c2
            var c1 = new OdfComment("A", "C1");
            var c2 = new OdfComment("A", "C2");
            var c3 = new OdfComment("A", "C3");
            var c4 = new OdfComment("A", "C4");

            c1.AddReply(c2);
            c2.AddReply(c3);
            c3.AddReply(c4);
            c4.AddReply(c2); // cycle back to c2

            Assert.Throws<InvalidOperationException>(() => c1.ToXmlNode());

            // 3. Deep Nesting Stack Overflow Risk (Review-only stress test)
            // Recursive functions in C# can cause stack overflow if depth is large.
            // We want to see if a deep chain of 1000 replies is handled or crashes.
            // If it crashes, it's a critical vulnerability.
            var deepRoot = new OdfComment("Author", "Start");
            var current = deepRoot;
            for (int i = 0; i < 1000; i++)
            {
                var reply = new OdfComment("Author", $"Reply {i}");
                current.AddReply(reply);
                current = reply;
            }

            // Serialize and measure depth
            // If it doesn't crash the stack, verify it succeeds.
            var nestedXml = deepRoot.ToXmlNode();
            Assert.NotNull(nestedXml);
        }

        [Fact]
        public void TestOdfMailMergeEngineStressAndBoundary()
        {
            // Construct a mini Odf document package
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            // Create a table with template row
            var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");

            // Row with placeholder containing repeating collection name and properties
            var row = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");

            var cell1 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
            var p1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            p1.TextContent = "{{items.Name}}";
            cell1.AppendChild(p1);

            // Cell with formula to shift
            var cell2 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
            cell2.SetAttribute("formula", OdfNamespaces.Table, "oooc:=[.A1]+[.B1]");
            var p2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            p2.TextContent = "{{items.Value}}";
            cell2.AppendChild(p2);

            row.AppendChild(cell1);
            row.AppendChild(cell2);
            table.AppendChild(row);

            // DataSource with 1000 items
            var itemsList = new List<Dictionary<string, object>>();
            for (int i = 0; i < 1000; i++)
            {
                itemsList.Add(new Dictionary<string, object>
                {
                    { "Name", $"Item_{i}" },
                    { "Value", i * 1.5 }
                });
            }
            var dataSource = new Dictionary<string, object>
            {
                { "items", itemsList }
            };

            var engine = new OdfMailMergeEngine(doc);
            var sw = Stopwatch.StartNew();
            engine.Execute(table, dataSource);
            sw.Stop();

            // The template row should be removed, and 1000 rows inserted
            Assert.Equal(1000, table.Children.Count);

            // Verify correct values and shifted formulas
            var firstMergedRow = table.Children[0];
            Assert.Equal("Item_0", firstMergedRow.Children[0].TextContent);
            Assert.Equal("oooc:=[.A1]+[.B1]", firstMergedRow.Children[1].GetAttribute("formula", OdfNamespaces.Table));

            var lastMergedRow = table.Children[999];
            Assert.Equal("Item_999", lastMergedRow.Children[0].TextContent);
            // Formula in row index 999 should be shifted by 999: oooc:=[.A1000]+[.B1000]
            Assert.Equal("oooc:=[.A1000]+[.B1000]", lastMergedRow.Children[1].GetAttribute("formula", OdfNamespaces.Table));

            // Verify performance: 1000 rows should merge and shift formulas in < 1 second
            Assert.True(sw.ElapsedMilliseconds < 1000, $"Mail merge took too long: {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void TestHtmlParsingRawInequalities()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var p = doc.AddParagraph();

            // Test raw < character (not a tag)
            string html1 = "A < B";
            p.AddHtmlFragment(html1);
            // Let's assert what the text content actually becomes to document it.
            // If the bug is present, "< B" might be lost or mangled. We will output this in our report.
            string text1 = p.TextContent;

            // Test raw inequality in statement
            p = doc.AddParagraph();
            string html2 = "if (x < y && y > z)";
            p.AddHtmlFragment(html2);
            string text2 = p.TextContent;

            // Test < B > treated as tag
            p = doc.AddParagraph();
            string html3 = "a < b > c";
            p.AddHtmlFragment(html3);
            string text3 = p.TextContent;

            Trace.WriteLine($"html1: {html1} -> parsed: '{text1}'");
            Trace.WriteLine($"html2: {html2} -> parsed: '{text2}'");
            Trace.WriteLine($"html3: {html3} -> parsed: '{text3}'");
        }

        [Fact]
        public void TestHtmlParsingUnclosedTags()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var p = doc.AddParagraph();

            // Unclosed <b> tag should format subsequent text as bold but not throw or crash
            string html1 = "Hello <b>world how are you";
            p.AddHtmlFragment(html1);
            string text1 = p.TextContent;

            // Unclosed <a> tag
            p = doc.AddParagraph();
            string html2 = "Click <a href=\"http://test.com\">here for more text";
            p.AddHtmlFragment(html2);
            string text2 = p.TextContent;

            Trace.WriteLine($"unclosed b: {html1} -> parsed: '{text1}'");
            Trace.WriteLine($"unclosed a: {html2} -> parsed: '{text2}'");
        }

        [Fact]
        public void TestHtmlParsingScriptStyleFilterEdgeCases()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var p = doc.AddParagraph();

            // Nested script tag bypass
            string html1 = "<script><script>alert(1)</script></script>TextAfter";
            p.AddHtmlFragment(html1);
            string text1 = p.TextContent;

            // Script tag without closing tag
            p = doc.AddParagraph();
            string html2 = "Before <script src=unsafe.js alert(1) After";
            p.AddHtmlFragment(html2);
            string text2 = p.TextContent;

            Trace.WriteLine($"nested script: {html1} -> parsed: '{text1}'");
            Trace.WriteLine($"unclosed script: {html2} -> parsed: '{text2}'");
        }

        [Fact]
        public void TestOdfCommentXmlCycleParsing()
        {
            // Create an XML document containing a cycle: c1 -> c2 -> c1
            var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);

            var c1Node = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            c1Node.SetAttribute("name", OdfNamespaces.Office, "c1", "office");
            c1Node.SetAttribute("annotation-parent", OdfNamespaces.Office, "c2", "office");
            var creator1 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "A" };
            var p1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Msg1" };
            c1Node.AppendChild(creator1);
            c1Node.AppendChild(p1);

            var c2Node = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            c2Node.SetAttribute("name", OdfNamespaces.Office, "c2", "office");
            c2Node.SetAttribute("annotation-parent", OdfNamespaces.Office, "c1", "office");
            var creator2 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "A" };
            var p2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Msg2" };
            c2Node.AppendChild(creator2);
            c2Node.AppendChild(p2);

            container.AppendChild(c1Node);
            container.AppendChild(c2Node);

            // Parsing should construct the cyclic graph in memory without throwing or looping infinitely
            var comment = OdfComment.FromXmlNode(container);
            Assert.NotNull(comment);

            // Serialization must detect the cycle and throw InvalidOperationException
            Assert.Throws<InvalidOperationException>(() => comment.ToXmlNode());
        }

        [Fact]
        public void TestOdfCommentDiamondDagRoundTrip()
        {
            // Build diamond DAG
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
            Assert.NotNull(xmlNode);

            // Deserialize
            var parsed = OdfComment.FromXmlNode(xmlNode);
            Assert.NotNull(parsed);

            // Check if the diamond relationship is preserved (it should not be due to ODF XML format parent attribute limitation)
            // Let's see what the parsed replies look like
            Assert.Equal("Root", parsed.Text);
            Assert.Equal(2, parsed.Replies.Count);
        }
    }
}

