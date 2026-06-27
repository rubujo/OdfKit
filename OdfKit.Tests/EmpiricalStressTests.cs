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

namespace OdfKit.Tests
{
    [Trait(TestCategories.Kind, TestCategories.Stress)]
    [Trait(TestCategories.Kind, TestCategories.Performance)]
    public class EmpiricalStressTests
    {
        [Fact]
        public void TestOdsStreamWriterOneMillionCells()
        {
            // Discard data to isolate memory usage of writer and ZipArchive
            var nullStream = Stream.Null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startMemory = GC.GetTotalMemory(true);
            var watch = Stopwatch.StartNew();

            int rows = 100000;
            int cols = 10; // 1,000,000 cells total

            using (var writer = new OdsStreamWriter(nullStream))
            {
                writer.WriteStartSheet("HugeSheet");

                for (int c = 0; c < cols; c++)
                {
                    writer.WriteColumn(OdfLength.FromCentimeters(2.0));
                }

                for (int r = 0; r < rows; r++)
                {
                    writer.WriteStartRow();
                    for (int c = 0; c < cols; c++)
                    {
                        int choice = (r + c) % 4;
                        if (choice == 0)
                            writer.WriteCell("Text_" + r + "_" + c);
                        else if (choice == 1)
                            writer.WriteCell((double)(r + c));
                        else if (choice == 2)
                            writer.WriteCell(DateTime.UtcNow, timezoneNaive: true);
                        else
                            writer.WriteCell((r + c) % 2 == 0);
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

            double timeSec = watch.ElapsedMilliseconds / 1000.0;
            double memMB = diffMemory / 1024.0 / 1024.0;

            LogStressResult($"[OdsStreamWriter Stress] 1M Cells written in {timeSec:F2} seconds. Retained Memory: {memMB:F2} MB.");

            // Performance: 1,000,000 cells should write in under 15 seconds
            Assert.True(timeSec < 15.0, $"Writing 1M cells took too long: {timeSec:F2} seconds.");

            // Memory: Retained memory should be under 5 MB since it's fully streamed
            Assert.True(memMB < 5.0, $"Memory growth is too high for streaming: {memMB:F2} MB.");
        }

        [Fact]
        public void TestOdfCommentNestingDepthThresholds()
        {
            // Recursive function test for StackOverflowException
            // StackOverflow occurs above ~500 in test environment
            int[] depthsToTest = { 100, 250, 450 };

            foreach (var depth in depthsToTest)
            {
                var root = new OdfComment("Author", "Root");
                var current = root;
                for (int i = 0; i < depth; i++)
                {
                    var reply = new OdfComment("Author", $"Reply {i}");
                    current.AddReply(reply);
                    current = reply;
                }

                try
                {
                    var sw = Stopwatch.StartNew();
                    var xmlNode = root.ToXmlNode();
                    sw.Stop();
                    LogStressResult($"[OdfComment Nesting] Depth {depth} serialized in {sw.ElapsedMilliseconds} ms without crash.");
                    Assert.NotNull(xmlNode);
                }
                catch (StackOverflowException)
                {
                    // If .NET runtime allowed us to catch it, but it doesn't. Process would crash.
                    // If we reach here, it did not crash.
                    Assert.Fail($"StackOverflowException thrown for depth {depth}.");
                }
                catch (Exception ex)
                {
                    LogStressResult($"[OdfComment Nesting] Depth {depth} threw: {ex.Message}");
                    throw;
                }
            }
        }

        [Fact]
        public void TestOdfCommentLongCycleDetection()
        {
            // Indirect cycle: c1 -> c2 -> c3 -> c4 -> c5 -> c6 -> c7 -> c8 -> c9 -> c10 -> c1
            int cycleLength = 20;
            var comments = new List<OdfComment>();
            for (int i = 0; i < cycleLength; i++)
            {
                comments.Add(new OdfComment("Author", $"Comment {i}"));
            }

            for (int i = 0; i < cycleLength - 1; i++)
            {
                comments[i].AddReply(comments[i + 1]);
            }
            comments[cycleLength - 1].AddReply(comments[0]); // Cycle back

            Assert.Throws<InvalidOperationException>(() => comments[0].ToXmlNode());
        }

        [Fact]
        public void TestOdfCommentDiamondDAGRoundtripBehavior()
        {
            // Verify how the library behaves on parsing back a DAG structure with diamond dependencies
            var root = new OdfComment("Author", "Root");
            var reply1 = new OdfComment("Author", "Reply1");
            var reply2 = new OdfComment("Author", "Reply2");
            var shared = new OdfComment("Author", "SharedComment");

            root.AddReply(reply1);
            root.AddReply(reply2);
            reply1.AddReply(shared);
            reply2.AddReply(shared);

            // 1. Serialize to XML node
            var xmlNode = root.ToXmlNode();
            Assert.NotNull(xmlNode);

            // 2. Parse back
            var parsed = OdfComment.FromXmlNode(xmlNode);
            Assert.NotNull(parsed);

            // 3. Let's inspect the reconstructed hierarchy
            LogStressResult($"[OdfComment Diamond] Root replies count: {parsed.Replies.Count}");
            Assert.Equal(2, parsed.Replies.Count);

            var parsedReply1 = parsed.Replies[0];
            var parsedReply2 = parsed.Replies[1];

            LogStressResult($"[OdfComment Diamond] Reply1 replies count: {parsedReply1.Replies.Count}");
            LogStressResult($"[OdfComment Diamond] Reply2 replies count: {parsedReply2.Replies.Count}");

            // Verify that the diamond relationship was lost because ODF 1.3 annotations are flat 
            // and the XML structure has to map to one parent.
            // Specifically, only one of the replies should contain the shared comment.
            int totalSharedOccurrences = 0;
            if (ContainsReply(parsedReply1, "SharedComment"))
                totalSharedOccurrences++;
            if (ContainsReply(parsedReply2, "SharedComment"))
                totalSharedOccurrences++;

            LogStressResult($"[OdfComment Diamond] Shared comment occurrences in parsed tree: {totalSharedOccurrences}");

            // As per our logic chain, parentMap only retains the last occurrence's parent (reply2)
            // So it should be present in only one parent.
            Assert.Equal(1, totalSharedOccurrences);
        }

        private bool ContainsReply(OdfComment parent, string text)
        {
            foreach (var r in parent.Replies)
            {
                if (r.Text == text)
                    return true;
            }
            return false;
        }

        [Fact]
        public void TestOdfMailMergeFormulaShiftingExtensive()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");

            var row = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");

            // Define formulas to test shifting
            var formulaTestCases = new[]
            {
                "oooc:=SUM([.A1:.B1])",           // Relative range
                "oooc:=[.C1]+[.D1]",             // Relative cell references
                "oooc:=[$Sheet1.E1]",             // Absolute sheet reference, relative row reference
                "oooc:=[.F$1]",                   // Relative column reference, absolute row reference
                "oooc:=[.$G$1]",                  // Absolute column and row reference
                "oooc:=SUM([.A1];[.B1])",         // Range separator semicolon
                "oooc:=SUM([.A1:.B1]) + [.$G$1]" // Mixed relative and absolute
            };

            foreach (var formula in formulaTestCases)
            {
                var cell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                cell.SetAttribute("formula", OdfNamespaces.Table, formula);
                var p = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                p.TextContent = "{{items.Name}}"; // repeating row triggers
                cell.AppendChild(p);
                row.AppendChild(cell);
            }

            table.AppendChild(row);

            // Perform mail merge with 3 items (clone index 0, 1, 2)
            var itemsList = new List<Dictionary<string, object>>
            {
                new() { { "Name", "Row1" } },
                new() { { "Name", "Row2" } },
                new() { { "Name", "Row3" } }
            };

            var dataSource = new Dictionary<string, object>
            {
                { "items", itemsList }
            };

            var engine = new OdfMailMergeEngine(doc);
            engine.Execute(table, dataSource);

            Assert.Equal(3, table.Children.Count);

            // Verify shifted formulas in Copy 0 (rowIndex = 0, should not shift relative addresses)
            var row0 = table.Children[0];
            Assert.Equal("oooc:=SUM([.A1:.B1])", row0.Children[0].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.C1]+[.D1]", row0.Children[1].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[$Sheet1.E1]", row0.Children[2].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.F$1]", row0.Children[3].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.$G$1]", row0.Children[4].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=SUM([.A1];[.B1])", row0.Children[5].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=SUM([.A1:.B1]) + [.$G$1]", row0.Children[6].GetAttribute("formula", OdfNamespaces.Table));

            // Verify shifted formulas in Copy 1 (rowIndex = 1, shift relative by 1)
            var row1 = table.Children[1];
            Assert.Equal("oooc:=SUM([.A2:.B2])", row1.Children[0].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.C2]+[.D2]", row1.Children[1].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[$Sheet1.E2]", row1.Children[2].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.F$1]", row1.Children[3].GetAttribute("formula", OdfNamespaces.Table)); // Absolute row preserved
            Assert.Equal("oooc:=[.$G$1]", row1.Children[4].GetAttribute("formula", OdfNamespaces.Table)); // Absolute row preserved
            Assert.Equal("oooc:=SUM([.A2];[.B2])", row1.Children[5].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=SUM([.A2:.B2]) + [.$G$1]", row1.Children[6].GetAttribute("formula", OdfNamespaces.Table));

            // Verify shifted formulas in Copy 2 (rowIndex = 2, shift relative by 2)
            var row2 = table.Children[2];
            Assert.Equal("oooc:=SUM([.A3:.B3])", row2.Children[0].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.C3]+[.D3]", row2.Children[1].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[$Sheet1.E3]", row2.Children[2].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=[.F$1]", row2.Children[3].GetAttribute("formula", OdfNamespaces.Table)); // Absolute row preserved
            Assert.Equal("oooc:=[.$G$1]", row2.Children[4].GetAttribute("formula", OdfNamespaces.Table)); // Absolute row preserved
            Assert.Equal("oooc:=SUM([.A3];[.B3])", row2.Children[5].GetAttribute("formula", OdfNamespaces.Table));
            Assert.Equal("oooc:=SUM([.A3:.B3]) + [.$G$1]", row2.Children[6].GetAttribute("formula", OdfNamespaces.Table));
        }

        [Fact]
        public void TestOdfMailMergeLargeScalePerformance()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);

            var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            var row = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");

            var cell1 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
            var p1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            p1.TextContent = "{{items.Name}}";
            cell1.AppendChild(p1);

            var cell2 = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
            cell2.SetAttribute("formula", OdfNamespaces.Table, "oooc:=[.A1]*1.1");
            var p2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            p2.TextContent = "{{items.Price}}";
            cell2.AppendChild(p2);

            row.AppendChild(cell1);
            row.AppendChild(cell2);
            table.AppendChild(row);

            // Merge 25,000 rows
            int itemsCount = 25000;
            var itemsList = new List<MergeItem>();
            for (int i = 0; i < itemsCount; i++)
            {
                itemsList.Add(new MergeItem { Name = $"Item{i}", Price = i * 0.5 });
            }

            var dataSource = new Dictionary<string, object>
            {
                { "items", itemsList }
            };

            var engine = new OdfMailMergeEngine(doc);
            var sw = Stopwatch.StartNew();
            engine.Execute(table, dataSource);
            sw.Stop();

            LogStressResult($"[OdfMailMerge Stress] Merged {itemsCount} items in {sw.ElapsedMilliseconds} ms.");

            Assert.Equal(itemsCount, table.Children.Count);

            // Check formula in last row (index 24999 should shift by 24999: oooc:=[.A25000]*1.1)
            Assert.Equal("oooc:=[.A25000]*1.1", table.Children[itemsCount - 1].Children[1].GetAttribute("formula", OdfNamespaces.Table));

            // Performance: 25,000 rows should take under 3.5 seconds
            Assert.True(sw.ElapsedMilliseconds < 3500, $"MailMerge took too long: {sw.ElapsedMilliseconds} ms");
        }

        private class MergeItem
        {
            public string Name { get; set; } = string.Empty;
            public double Price { get; set; }
        }

        private static void LogStressResult(string message)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "stress_test_runs.log");
            try
            {
                File.AppendAllText(logPath, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ssZ} - {message}\n");
            }
            catch { }
            Console.WriteLine(message);
        }
    }
}
