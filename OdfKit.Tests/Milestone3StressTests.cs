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
    public class Milestone3StressTests
    {
        #region OdsStreamWriter Tests

        [Fact]
        public void TestOdsStreamWriterLowMemoryHighCellCount()
        {
            // Discard data to isolate writer and ZipArchive memory usage from MemoryStream buffers.
            var nullStream = Stream.Null;
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startMemory = GC.GetTotalMemory(true);
            
            using (var writer = new OdsStreamWriter(nullStream))
            {
                writer.WriteStartSheet("StressSheet");
                
                // Write column widths
                for (int c = 0; c < 10; c++)
                {
                    writer.WriteColumn(OdfLength.FromCentimeters(2.5));
                }
                
                // Write 50,000 rows, 10 cells each = 500,000 cells
                for (int r = 0; r < 50000; r++)
                {
                    writer.WriteStartRow();
                    for (int c = 0; c < 10; c++)
                    {
                        // Alternate data types to cover all overloads
                        int choice = (r + c) % 4;
                        if (choice == 0)
                            writer.WriteCell("Text_" + r + "_" + c);
                        else if (choice == 1)
                            writer.WriteCell((double)(r + c));
                        else if (choice == 2)
                            writer.WriteCell(DateTime.UtcNow, timezoneNaive: c % 2 == 0);
                        else
                            writer.WriteCell((r + c) % 2 == 0);
                    }
                    writer.WriteEndRow();
                }
                
                writer.WriteEndSheet();
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long endMemory = GC.GetTotalMemory(true);
            long diffMemory = endMemory - startMemory;
            
            // Assert that the retained memory increase is very small, e.g., less than 5 MB
            Assert.True(diffMemory < 5 * 1024 * 1024, $"Retained memory increased by {diffMemory / 1024.0 / 1024.0:F2} MB, which is too high for a stream writer.");
        }

        [Fact]
        public void TestOdsStreamWriterXmlValidation()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell("Hello <XML> & \"Quotes\" & 'Apos'");
                writer.WriteCell(123.45);
                writer.WriteCell(true);
                writer.WriteCell(new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc));
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }
            
            // Reopen zip and verify content.xml
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                Assert.True(package.HasEntry("content.xml"));
                Assert.True(package.HasEntry("styles.xml"));
                Assert.True(package.HasEntry("mimetype"));
                
                // Read mimetype
                using (var s = package.GetEntryStream("mimetype"))
                using (var sr = new StreamReader(s))
                {
                    string mime = sr.ReadToEnd();
                    Assert.Equal("application/vnd.oasis.opendocument.spreadsheet", mime);
                }
                
                // Parse content.xml
                using (var s = package.GetEntryStream("content.xml"))
                {
                    var root = OdfXmlReader.Parse(s);
                    Assert.NotNull(root);
                    
                    // Verify the cells are present
                    var cells = new List<OdfNode>();
                    FindNodesByLocalName(root, "table-cell", cells);
                    Assert.Equal(4, cells.Count);
                    
                    Assert.Equal("Hello <XML> & \"Quotes\" & 'Apos'", cells[0].TextContent);
                    Assert.Equal("string", cells[0].GetAttribute("value-type", OdfNamespaces.Office));
                    
                    Assert.Equal("123.45", cells[1].TextContent);
                    Assert.Equal("float", cells[1].GetAttribute("value-type", OdfNamespaces.Office));
                    Assert.Equal("123.45", cells[1].GetAttribute("value", OdfNamespaces.Office));
                    
                    Assert.Equal("TRUE", cells[2].TextContent);
                    Assert.Equal("boolean", cells[2].GetAttribute("value-type", OdfNamespaces.Office));
                    Assert.Equal("true", cells[2].GetAttribute("boolean-value", OdfNamespaces.Office));
                    
                    Assert.Equal("2026-06-09T08:00:00Z", cells[3].TextContent);
                    Assert.Equal("date", cells[3].GetAttribute("value-type", OdfNamespaces.Office));
                    Assert.Equal("2026-06-09T08:00:00Z", cells[3].GetAttribute("date-value", OdfNamespaces.Office));
                }
            }
        }

        [Fact]
        public void TestOdsStreamWriterInterleavedAndEdgeCases()
        {
            using var ms = new MemoryStream();
            using (var writer = new OdsStreamWriter(ms))
            {
                // Write empty sheet
                writer.WriteStartSheet("EmptySheet");
                writer.WriteEndSheet();
                
                // Write sheet with no row end
                writer.WriteStartSheet("NoRowEndSheet");
                writer.WriteStartRow();
                writer.WriteCell("Cell");
                
                writer.WriteStartSheet("AutoClosedSheet");
                writer.WriteStartRow();
                writer.WriteCell("Value");
            }
            
            // Reopen and check if it parses correctly
            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                using (var s = package.GetEntryStream("content.xml"))
                {
                    var root = OdfXmlReader.Parse(s);
                    Assert.NotNull(root);
                    
                    var tables = new List<OdfNode>();
                    FindNodesByLocalName(root, "table", tables);
                    Assert.Equal(3, tables.Count);
                    
                    Assert.Equal("EmptySheet", tables[0].GetAttribute("name", OdfNamespaces.Table));
                    Assert.Equal("NoRowEndSheet", tables[1].GetAttribute("name", OdfNamespaces.Table));
                    Assert.Equal("AutoClosedSheet", tables[2].GetAttribute("name", OdfNamespaces.Table));
                }
            }
        }

        #endregion

        #region OdfComment Tests

        [Fact]
        public void TestOdfCommentDeepNestingLimit()
        {
            // Build a nested comment chain of depth 300
            var root = new OdfComment("Author", "Root");
            var current = root;
            for (int i = 0; i < 300; i++)
            {
                var reply = new OdfComment("Author", $"Reply {i}");
                current.AddReply(reply);
                current = reply;
            }
            
            // Serialize to XML node
            var xmlNode = root.ToXmlNode();
            
            // Since they are now flat, XML depth is 4 (accounts for annotation-list, annotation, p and text node)
            int depth = GetNodeDepth(xmlNode);
            Assert.Equal(4, depth);
            
            // Re-render to XML byte stream and try parsing.
            // Since they are flat, it should parse successfully without throwing SecurityException.
            using var ms = new MemoryStream();
            OdfXmlWriter.Write(xmlNode, ms, new OdfSaveOptions());
            ms.Position = 0;
            
            var parsed = OdfXmlReader.Parse(ms);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void TestOdfCommentCircularReferenceStackOverflow()
        {
            var comment1 = new OdfComment("Author1", "Comment 1");
            var comment2 = new OdfComment("Author2", "Comment 2");
            
            // Verify cycle is allowed to be constructed by API
            comment1.AddReply(comment2);
            comment2.AddReply(comment1);
            
            Assert.Contains(comment2, comment1.Replies);
            Assert.Contains(comment1, comment2.Replies);

            // Verify cycle is detected when serializing to XML node and throws InvalidOperationException
            Assert.Throws<InvalidOperationException>(() => comment1.ToXmlNode());
        }

        [Fact]
        public void TestOdfCommentWideTreePerformance()
        {
            var root = new OdfComment("Author", "Root");
            for (int i = 0; i < 10000; i++)
            {
                root.AddReply(new OdfComment("Author", $"Reply {i}"));
            }
            
            Assert.Equal(10000, root.Replies.Count);
            
            // Measure time to serialize
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var xmlNode = root.ToXmlNode();
            watch.Stop();
            
            Assert.True(watch.ElapsedMilliseconds < 500, $"Serialization of 10k flat replies took {watch.ElapsedMilliseconds} ms, which is too slow.");
            
            // Re-render and parse back
            using var ms = new MemoryStream();
            OdfXmlWriter.Write(xmlNode, ms, new OdfSaveOptions());
            ms.Position = 0;
            
            var parsed = OdfXmlReader.Parse(ms);
            Assert.NotNull(parsed);
            
            var parsedComment = OdfComment.FromXmlNode(parsed);
            Assert.Equal(10000, parsedComment.Replies.Count);
        }

        [Fact]
        public void TestOdfCommentDagSerializationNoDuplicates()
        {
            // Diamond DAG configuration:
            // root -> left -> bottom
            // root -> right -> bottom
            var root = new OdfComment("Author", "Root");
            var left = new OdfComment("Author", "Left");
            var right = new OdfComment("Author", "Right");
            var bottom = new OdfComment("Author", "Bottom");

            root.AddReply(left);
            root.AddReply(right);
            left.AddReply(bottom);
            right.AddReply(bottom);

            // Serialization should not throw circular reference and should serialize bottom comment only once
            var xmlNode = root.ToXmlNode();
            Assert.NotNull(xmlNode);

            // Find all nodes in the tree to count how many bottom comments exist
            int count = 0;

            OdfNode? FindChild(OdfNode parent, string localName, string ns)
            {
                foreach (var child in parent.Children)
                {
                    if (child.LocalName == localName && child.NamespaceUri == ns)
                        return child;
                }
                return null;
            }
            var queue = new Queue<OdfNode>();
            queue.Enqueue(xmlNode);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.LocalName == "annotation" && current.NamespaceUri == OdfNamespaces.Office)
                {
                    // Check if creator is Author and text matches Bottom
                    var creatorNode = FindChild(current, "creator", OdfNamespaces.Dc);
                    var pNode = FindChild(current, "p", OdfNamespaces.Text);
                    if (creatorNode?.TextContent == "Author" && pNode?.TextContent == "Bottom")
                    {
                        count++;
                    }
                }
                foreach (var child in current.Children)
                {
                    queue.Enqueue(child);
                }
            }

            // Since it's a DAG, bottom must be serialized exactly once!
            Assert.Equal(1, count);
        }

        [Fact]
        public void TestAddHtmlFragmentRegexQuotesAndEntityDecoding()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);
                var p = doc.AddParagraph();

                // 1. Single quotes inside style: style='font-style: italic;'
                // 2. Double quotes inside style: style="font-weight: bold;"
                // 3. Script tag content filtered out
                // 4. Style tag content filtered out
                // 5. HTML entities decoded: &lt; -> <, &gt; -> >, &amp; -> &, &quot; -> "
                string html = "This is <span style=\"font-weight: bold;\">bold text</span>, <span style='font-style: italic;'>italic text</span>, " +
                             "<script>var a = 1; console.log(a);</script> and <style>p { color: green; }</style>" +
                             "special &lt;characters&gt; &amp; &quot;quotes&quot;.";

                doc.AddHtmlFragment(p, html);

                // Expected final plain text should contain decoded characters and NOT contain script or style block content
                string plainText = p.TextContent;
                Assert.Contains("This is bold text, italic text,  and special <characters> & \"quotes\".", plainText);
                Assert.DoesNotContain("var a = 1", plainText);
                Assert.DoesNotContain("p { color: green; }", plainText);

                // Verify the spans were parsed and styled
                var spans = new List<OdfNode>();
                FindSpans(p.Node, spans);
                Assert.Equal(2, spans.Count);

                var boldSpan = spans[0];
                Assert.Equal("bold text", boldSpan.TextContent);
                var boldStyleName = boldSpan.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("bold", doc.StyleEngine.GetStyleProperty(boldStyleName!, "font-weight", OdfNamespaces.Fo, "text"));

                var italicSpan = spans[1];
                Assert.Equal("italic text", italicSpan.TextContent);
                var italicStyleName = italicSpan.GetAttribute("style-name", OdfNamespaces.Text);
                Assert.Equal("italic", doc.StyleEngine.GetStyleProperty(italicStyleName!, "font-style", OdfNamespaces.Fo, "text"));
            }
        }

        private static void FindSpans(OdfNode node, List<OdfNode> spans)
        {
            if (node.LocalName == "span" && node.NamespaceUri == OdfNamespaces.Text)
            {
                spans.Add(node);
            }
            foreach (var child in node.Children)
            {
                FindSpans(child, spans);
            }
        }

        #endregion

        #region OdfMailMergeEngine Tests

        [Fact]
        public void TestOdfMailMergeBasicAndRepeatingRows()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var p = doc.AddParagraph("Customer: {{customerName}}");
            
            var tableNode = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            doc.BodyTextRoot.AppendChild(tableNode);
            
            var rowNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table-row");
            tableNode.AppendChild(rowNode);
            
            var cellNode = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table-cell");
            rowNode.AppendChild(cellNode);
            
            var cellPara = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellPara.TextContent = "Item: {{items.name}} - Price: {{items.price}}";
            cellNode.AppendChild(cellPara);
            
            var dataSource = new Dictionary<string, object>
            {
                { "customerName", "Alice" },
                { "items", new List<Dictionary<string, object>>
                    {
                        new() { { "name", "Pen" }, { "price", 1.50 } },
                        new() { { "name", "Notebook" }, { "price", 3.00 } }
                    }
                }
            };
            
            doc.MailMerge(dataSource);
            
            Assert.Equal("Customer: Alice", p.Node.TextContent);
            
            var rows = new List<OdfNode>();
            FindNodesByLocalName(doc.BodyTextRoot, "table-row", rows);
            Assert.Equal(2, rows.Count);
            
            Assert.Equal("Item: Pen - Price: 1.5", rows[0].TextContent);
            Assert.Equal("Item: Notebook - Price: 3", rows[1].TextContent);
        }

        [Fact]
        public void TestOdfMailMergeFormulaShiftingBug()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var tableNode = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            doc.BodyTextRoot.AppendChild(tableNode);
            
            var rowNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table-row");
            tableNode.AppendChild(rowNode);
            
            var cellNode = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table-cell");
            cellNode.SetAttribute("formula", OdfNamespaces.Table, "oooc:=SUM([.B1:.D1])");
            rowNode.AppendChild(cellNode);
            
            var cellPara = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellPara.TextContent = "Item: {{items.name}}";
            cellNode.AppendChild(cellPara);
            
            var dataSource = new Dictionary<string, object>
            {
                { "items", new List<Dictionary<string, object>>
                    {
                        new() { { "name", "Pen" } },
                        new() { { "name", "Notebook" } }
                    }
                }
            };
            
            doc.MailMerge(dataSource);
            
            var rows = new List<OdfNode>();
            FindNodesByLocalName(doc.BodyTextRoot, "table-row", rows);
            Assert.Equal(2, rows.Count);
            
            var cell1 = rows[0].Children[0];
            var cell2 = rows[1].Children[0];
            
            string? formula1 = cell1.GetAttribute("formula", OdfNamespaces.Table);
            string? formula2 = cell2.GetAttribute("formula", OdfNamespaces.Table);
            
            Assert.Equal("oooc:=SUM([.B1:.D1])", formula1);
            Assert.Equal("oooc:=SUM([.B2:.D2])", formula2); // Fails if formula shifting isn't implemented
        }

        [Fact]
        public void TestOdfMailMergePerformanceAndReflectionBottleneck()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var tableNode = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            doc.BodyTextRoot.AppendChild(tableNode);
            
            var rowNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table-row");
            tableNode.AppendChild(rowNode);
            
            var cellNode1 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table-cell");
            var cellPara1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellPara1.TextContent = "Item: {{items.name}}";
            cellNode1.AppendChild(cellPara1);
            rowNode.AppendChild(cellNode1);
            
            var cellNode2 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table-cell");
            var cellPara2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellPara2.TextContent = "Sku: {{items.sku}}";
            cellNode2.AppendChild(cellPara2);
            rowNode.AppendChild(cellNode2);

            var itemsList = new List<MergeItem>();
            for (int i = 0; i < 5000; i++)
            {
                itemsList.Add(new MergeItem { Name = $"Item {i}", Sku = $"SKU-{i}" });
            }
            
            var dataSource = new { items = itemsList };
            
            var watch = System.Diagnostics.Stopwatch.StartNew();
            doc.MailMerge(dataSource);
            watch.Stop();
            
            var rows = new List<OdfNode>();
            FindNodesByLocalName(doc.BodyTextRoot, "table-row", rows);
            Assert.Equal(5000, rows.Count);
            
            Assert.True(watch.ElapsedMilliseconds < 1500, $"MailMerge of 5000 items took {watch.ElapsedMilliseconds} ms, indicating a potential performance bottleneck.");
        }

        [Fact]
        public void TestOdfMailMergeParentPlaceholderClearingBug()
        {
            using var doc = new TextDocument(OdfPackage.Create(new MemoryStream()));
            var tableNode = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            doc.BodyTextRoot.AppendChild(tableNode);
            
            var rowNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table-row");
            tableNode.AppendChild(rowNode);
            
            var cellNode1 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table-cell");
            var cellPara1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellPara1.TextContent = "Manager: {{managerName}}"; // Parent placeholder
            cellNode1.AppendChild(cellPara1);
            rowNode.AppendChild(cellNode1);
            
            var cellNode2 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table-cell");
            var cellPara2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            cellPara2.TextContent = "Item: {{items.name}}"; // Collection placeholder
            cellNode2.AppendChild(cellPara2);
            rowNode.AppendChild(cellNode2);
            
            var dataSource = new Dictionary<string, object>
            {
                { "managerName", "Bob" },
                { "items", new List<Dictionary<string, object>>
                    {
                        new() { { "name", "Pen" } }
                    }
                }
            };
            
            doc.MailMerge(dataSource);
            
            var rows = new List<OdfNode>();
            FindNodesByLocalName(doc.BodyTextRoot, "table-row", rows);
            Assert.Single(rows);
            
            Assert.Equal("Manager: Bob", rows[0].Children[0].TextContent); // Fails if managerName is cleared
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

        private int GetNodeDepth(OdfNode node)
        {
            int maxChildDepth = 0;
            foreach (var child in node.Children)
            {
                maxChildDepth = Math.Max(maxChildDepth, GetNodeDepth(child));
            }
            return 1 + maxChildDepth;
        }

        private static OdfNode? FindChild(OdfNode parent, string localName, string ns)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            return null;
        }

        public class MergeItem
        {
            public string Name { get; set; } = "";
            public string Sku { get; set; } = "";
        }

        #endregion
    }
}
