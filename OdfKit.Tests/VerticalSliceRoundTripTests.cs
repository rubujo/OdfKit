using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Formula.AST;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests
{
    public class VerticalSliceRoundTripTests
    {
        [Fact]
        public void TestOdtVerticalSliceRoundTrip()
        {
            using var ms = new MemoryStream();

            // 1. Create and Write
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new TextDocument(package);

                var heading = doc.AddHeading("Test Heading", 1);
                heading.StyleName = "H1";
                heading.HorizontalAlignment = "center";

                var p = doc.AddParagraph("Test Paragraph");
                p.StyleName = "P1";
                p.HorizontalAlignment = "left";

                var pPlain = doc.AddParagraph("Plain Paragraph");
                pPlain.StyleName = "MyCustomStyleName";

                var list = doc.AddList();
                var item1 = list.AddListItem("Item 1");
                var item2 = list.AddListItem("Item 2");
                list.RestartNumbering(5);

                var table = doc.AddTable(2, 2);
                table.SetColumnWidth(0, OdfLength.FromCentimeters(5));
                table.SetColumnWidth(1, OdfLength.FromCentimeters(10));

                var cell = table.GetCell(0, 0);
                cell.TextContent = "Cell 0,0";
                cell.AddParagraph("Cell 0,0 extra paragraph");

                doc.Save();
            }

            ms.Position = 0;

            // 2. Reload and Verify
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new TextDocument(package);

                var headings = new List<OdfParagraph>();
                var paragraphs = new List<OdfParagraph>();
                OdfList? list = null;
                OdfTable? table = null;

                foreach (var child in doc.BodyTextRoot.Children)
                {
                    if (child.NodeType == OdfNodeType.Element)
                    {
                        if (child.LocalName == "h" && child.NamespaceUri == OdfNamespaces.Text)
                            headings.Add(new OdfParagraph(child, doc));
                        else if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                            paragraphs.Add(new OdfParagraph(child, doc));
                        else if (child.LocalName == "list" && child.NamespaceUri == OdfNamespaces.Text)
                            list = new OdfList(child, doc);
                        else if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
                            table = new OdfTable(child, 0, 0, doc);
                    }
                }

                Assert.Single(headings);
                var heading = headings[0];
                Assert.Equal("P1", heading.StyleName);
                Assert.Equal("center", heading.HorizontalAlignment);

                Assert.Equal(2, paragraphs.Count);
                var paragraph1 = paragraphs[0];
                Assert.Equal("P2", paragraph1.StyleName);
                Assert.Equal("left", paragraph1.HorizontalAlignment);

                var paragraph2 = paragraphs[1];
                Assert.Equal("MyCustomStyleName", paragraph2.StyleName);
                Assert.Null(paragraph2.HorizontalAlignment);

                Assert.NotNull(list);
                Assert.Equal(false, list.ContinueNumbering);
                var firstItemNode = list.Node.Children.FirstOrDefault(c => c.LocalName == "list-item");
                Assert.NotNull(firstItemNode);
                var firstItem = new OdfListItem(firstItemNode, doc);
                Assert.Equal(5, firstItem.StartValue);

                Assert.NotNull(table);
                var cellReloaded = table.GetCell(0, 0);
                Assert.Contains("Cell 0,0", cellReloaded.TextContent);
                Assert.Contains("Cell 0,0 extra paragraph", cellReloaded.Node.Children.Last().TextContent);
            }
        }

        [Fact]
        public void TestOdsVerticalSliceRoundTrip()
        {
            using var ms = new MemoryStream();

            // 1. Create and Write
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.AddSheet("Sheet1");

                // Set repeated cell columns
                var cell = sheet.GetCell(0, 0);
                cell.SetValue("Repeated");
                cell.Node.SetAttribute("number-columns-repeated", OdfNamespaces.Table, "5");

                // Set repeated rows
                var rowNode = cell.Node.Parent;
                Assert.NotNull(rowNode);
                rowNode.SetAttribute("number-rows-repeated", OdfNamespaces.Table, "3");

                // Set visibility
                sheet.SetRowVisible(0, false); // row 0 is collapsed
                sheet.SetColumnVisible(1, false); // col 1 is collapsed

                doc.Save();
            }

            ms.Position = 0;

            // 2. Reload and Verify
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new SpreadsheetDocument(package);
                var sheet = doc.GetSheets()[0];

                Assert.False(sheet.IsRowVisible(0));
                Assert.False(sheet.IsColumnVisible(1));
                Assert.True(sheet.IsRowVisible(1));
                Assert.True(sheet.IsColumnVisible(0));

                // Verify cell values (indices are shifted / expanded)
                Assert.Equal("Repeated", sheet.GetCell(0, 0).DisplayText);
                Assert.Equal("Repeated", sheet.GetCell(0, 4).DisplayText);
                Assert.Equal("Repeated", sheet.GetCell(2, 0).DisplayText);

                // Write to a middle cell to trigger split
                var middleCell = sheet.GetCell(1, 2);
                middleCell.SetValue("SplitTarget");

                // Verify that only cell (1, 2) is modified, others remain "Repeated"
                Assert.Equal("Repeated", sheet.GetCell(1, 0).DisplayText);
                Assert.Equal("Repeated", sheet.GetCell(1, 1).DisplayText);
                Assert.Equal("SplitTarget", sheet.GetCell(1, 2).DisplayText);
                Assert.Equal("Repeated", sheet.GetCell(1, 3).DisplayText);
                Assert.Equal("Repeated", sheet.GetCell(1, 4).DisplayText);

                // Ensure row 0 and 2 were not modified
                Assert.Equal("Repeated", sheet.GetCell(0, 2).DisplayText);
                Assert.Equal("Repeated", sheet.GetCell(2, 2).DisplayText);
            }
        }

        [Fact]
        public void TestOdpOdgVerticalSliceRoundTrip()
        {
            using var ms = new MemoryStream();

            // 1. Create and Write
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new DrawingDocument(package);
                var page = doc.AddPage("Page1");
                page.MasterPageName = "DefaultMaster";

                var shape1 = page.AddShape(OdfShapeType.Rectangle, OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(4));
                shape1.FillColor = "#ff0000";
                shape1.StrokeColor = "#0000ff";

                var points = new List<PointF> { new PointF(0, 0), new PointF(10, 10), new PointF(20, 0) };
                page.AddPolyline(points, OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(5), OdfLength.FromCentimeters(5));

                doc.Save();
            }

            ms.Position = 0;

            // 2. Reload and Verify
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new DrawingDocument(package);
                var page = doc.Pages[0];

                Assert.Equal("Page1", page.Name);
                Assert.Equal("DefaultMaster", page.MasterPageName);

                OdfShape? shape = null;
                OdfShape? polyline = null;

                foreach (var child in page.Node.Children)
                {
                    if (child.NodeType == OdfNodeType.Element)
                    {
                        if (child.LocalName == "rect")
                            shape = new OdfShape(child, doc);
                        else if (child.LocalName == "polyline")
                            polyline = new OdfShape(child, doc);
                    }
                }

                Assert.NotNull(shape);
                Assert.Equal("#ff0000", shape.FillColor);
                Assert.Equal("#0000ff", shape.StrokeColor);

                Assert.NotNull(polyline);
                Assert.Equal("0,0 10,10 20,0", polyline.Node.GetAttribute("points", OdfNamespaces.Draw));
            }
        }

        [Fact]
        public void TestOpenFormulaRoundTripSerialization()
        {
            var formulas = new[]
            {
                "A1~B1",
                "A1!B1",
                "(A1~B1)",
                "(A1~B1)!C1",
                "SUM((A1~B1),C1)",
                "IF((A1>0),1,0)"
            };

            foreach (var f in formulas)
            {
                var parser = new FormulaParser(f);
                var ast = parser.Parse();
                string serialized = ast.Serialize();
                Assert.Equal(f, serialized);
            }
        }
    }
}
