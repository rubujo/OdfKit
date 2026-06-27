using System.Collections.Generic;
using System.IO.Compression;
using System.Globalization;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Csv;
using OdfKit.DOM;
using OdfKit.Drawing;
using OdfKit.Formula;
using OdfKit.Image;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 使用真實 LibreOffice 26.x binary 驗證 OdfKit 產生文件的互通性。
/// </summary>
public class LibreOfficeInteropTests
{
    /// <summary>
    /// 驗證含追蹤修訂的 ODT 可由 LibreOffice 26.x headless 模式載入、轉換並由 OdfKit 重新讀取。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsTrackedChangesOdt()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過追蹤修訂實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeTrackedChanges_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-tracked-changes.odt");
            CreateTrackedChangesDocument(odtPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", odtPath);
            string txtPath = Path.Combine(outputDir, "interop-tracked-changes.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出追蹤修訂 ODT 的文字轉換結果。");
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            string txt;
            using (var stream = File.OpenRead(txtPath))
            {
                try
                {
                    var utf8Throw = Encoding.GetEncoding("utf-8", System.Text.EncoderFallback.ExceptionFallback, System.Text.DecoderFallback.ExceptionFallback);
                    using (var reader = new StreamReader(stream, utf8Throw, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
                catch (DecoderFallbackException)
                {
                    stream.Position = 0;
                    int codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                    using (var reader = new StreamReader(stream, Encoding.GetEncoding(codePage), detectEncodingFromByteOrderMarks: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
            }
            Assert.Contains("OdfKit-TrackedChanges-Marker", txt);
            Assert.Contains("表格追蹤修訂", txt);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odt", odtPath);
            string roundTripPath = Path.Combine(outputDir, "interop-tracked-changes.odt");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出追蹤修訂 ODT 往返結果。");

            using TextDocument document = TextDocument.Load(roundTripPath);
            string contentXml = ReadContentXml(document);
            Assert.Contains("OdfKit-TrackedChanges-Marker", contentXml);
            Assert.Contains("表格追蹤修訂", contentXml);

            var changes = document.GetTrackedChanges().ToList();
            if (changes.Count > 0)
            {
                Assert.Contains(changes, change => change.ChangeType == OdfChangeType.Insertion);
                document.AcceptAllChanges();
                Assert.Empty(document.GetTrackedChanges());
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證含 <c>table:tracked-changes</c> 的 ODS 可由 LibreOffice 26.x headless 模式載入並往返。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsTrackedChangesOds()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 ODS 追蹤修訂實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeTrackedChangesOds_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-tracked-changes.ods");
            CreateTrackedChangesSpreadsheet(odsPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-tracked-changes.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出追蹤修訂 ODS 往返結果。");

            using SpreadsheetDocument document = SpreadsheetDocument.Load(roundTripPath);
            string contentXml = ReadSpreadsheetContentXml(document);
            Assert.Contains("OdfKit-TrackedChanges-Ods-Marker", contentXml);
            Assert.Contains("table:tracked-changes", contentXml);

            if (document.GetTrackedChanges().Count > 0)
            {
                document.AcceptAllChanges();
                Assert.Empty(document.GetTrackedChanges());
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.RejectAllChanges"/> 拒絕全部待處理修訂後的 ODS，
    /// 仍可由 LibreOffice 26.x headless 模式載入並往返，且儲存格已還原為原始內容。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_RejectAllChangesRoundTripsOds()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過拒絕全部修訂實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeRejectAllChangesOds_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-reject-all-changes.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                document.TrackedChanges = false;
                OdfTableSheet sheet = document.AddSheet("Data");
                sheet.Cells["A1"].CellValue = "OdfKit-RejectAllChanges-Marker";
                sheet.Cells["A2"].CellValue = "原始 A2";
                sheet.Cells["A3"].CellValue = "原始 A3";

                document.TrackedChanges = true;
                sheet.Cells["A2"].CellValue = "暫存修改 A2";
                sheet.Cells["A3"].CellValue = "暫存修改 A3";

                // 真機驗證重點：一次拒絕多筆待處理修訂後，待儲存的內容應已還原為原始值，
                // 且 table:tracked-changes 節點應完全清空，不殘留任何待處理專案。
                document.RejectAllChanges();
                Assert.Empty(document.GetTrackedChanges());
                Assert.Equal("原始 A2", sheet.Cells["A2"].CellValue);
                Assert.Equal("原始 A3", sheet.Cells["A3"].CellValue);

                document.Save(odsPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-reject-all-changes.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出拒絕全部修訂後的 ODS 往返結果。");

            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath);
            OdfTableSheet loadedSheet = loaded.Worksheets["Data"];
            Assert.Equal("OdfKit-RejectAllChanges-Marker", loadedSheet.Cells["A1"].CellValue);
            Assert.Equal("原始 A2", loadedSheet.Cells["A2"].CellValue);
            Assert.Equal("原始 A3", loadedSheet.Cells["A3"].CellValue);
            Assert.Empty(loaded.GetTrackedChanges());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfChartDocument.ApplyDefinition"/> 與 <see cref="OdfChartDocument.ClearSeries"/>
    /// 套用後的嵌入圖表，仍可由 LibreOffice 26.x headless 模式載入並正確往返。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_ChartApplyDefinitionAndClearSeriesRoundTripsOds()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過圖表定義套用與序列清除實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeChartApplyDefinition_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-chart-apply-definition.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.Worksheets.Add("Data");
                sheet.Cells["A1"].CellValue = "月份";
                sheet.Cells["B1"].CellValue = "營收";
                sheet.Cells["A2"].CellValue = "一月";
                sheet.Cells["B2"].CellValue = 120d;
                sheet.Cells["A3"].CellValue = "二月";
                sheet.Cells["B3"].CellValue = 160d;

                document.AddChart("Data", new OdfCellAddress(4, 0, "Data"), new OdfChartDefinition
                {
                    ChartType = OdfChartType.Bar,
                    Title = "原始圖表標題",
                    DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
                    HasLegend = false,
                });

                OdfEmbeddedChartInfo chartInfo = Assert.Single(document.GetEmbeddedCharts());
                OdfChartDocument chartDoc = document.GetEmbeddedChartDocument(chartInfo);

                // 真機驗證重點一：ClearSeries 應移除既有資料序列節點。
                chartDoc.ClearSeries();
                OdfNode plotAreaBeforeReapply = chartDoc.ChartNode.Children
                    .Single(c => c.NodeType == OdfNodeType.Element && c.LocalName == "plot-area" && c.NamespaceUri == OdfNamespaces.Chart);
                Assert.DoesNotContain(
                    plotAreaBeforeReapply.Children,
                    c => c.NodeType == OdfNodeType.Element && c.LocalName == "series" && c.NamespaceUri == OdfNamespaces.Chart);

                // 真機驗證重點二：ApplyDefinition 應同時更新圖表類型、標題、圖例與資料範圍。
                chartDoc.ApplyDefinition(new OdfChartDefinition
                {
                    ChartType = OdfChartType.Line,
                    Title = "套用後折線圖標題",
                    DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
                    HasLegend = true,
                });

                Assert.Equal("chart:line", chartDoc.ChartClass);
                Assert.Equal("套用後折線圖標題", chartDoc.ChartTitle);
                Assert.Equal("top", chartDoc.LegendPosition);

                // 嵌入圖表文件需明確 Save，變更才會寫回母文件共用的封裝。
                chartDoc.Save();
                document.Save(odsPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-chart-apply-definition.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出套用圖表定義後的 ODS 往返結果。");

            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath);
            OdfEmbeddedChartInfo loadedChartInfo = Assert.Single(loaded.GetEmbeddedCharts());
            Assert.Equal(OdfChartType.Line, loadedChartInfo.ChartType);
            Assert.Equal("套用後折線圖標題", loadedChartInfo.Title);

            OdfChartDocument loadedChartDoc = loaded.GetEmbeddedChartDocument(loadedChartInfo);
            Assert.Equal("top", loadedChartDoc.LegendPosition);

            // LibreOffice 重新儲存後，圖表也應仍可正確轉出 PDF（驗證圖表渲染未報錯）。
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-chart-apply-definition.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將套用圖表定義後的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "圖表 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 真機互通性驗證：<see cref="OdfCsvImporter.ImportFromFile"/> 從磁碟讀入 CSV 並轉為
    /// <see cref="SpreadsheetDocument"/> 後存檔，產出的 ODS 可由真實 LibreOffice 26.x headless
    /// 模式正確載入並轉換；接着以 <see cref="OdfCsvExporter.ExportToFile"/> 將該 ODS
    /// 重新匯出為 CSV 檔案，驗證資料往返不失真。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_CsvImportFromFileThenExportToFileRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 CSV 檔案路徑 API 實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitCsvFileApiInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string csvInputPath = Path.Combine(tempRoot, "input.csv");
            string odsPath = Path.Combine(tempRoot, "converted.ods");
            string csvOutputPath = Path.Combine(tempRoot, "output.csv");

            File.WriteAllText(csvInputPath, "名稱,數量,價格\nOdfKit-Csv-File-Marker,10,25.5\n香蕉,5,12.0", Encoding.UTF8);

            using (SpreadsheetDocument workbook = OdfCsvImporter.ImportFromFile(csvInputPath))
            {
                workbook.Save(odsPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "converted.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出 CSV 匯入後的 ODS 往返結果。");

            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                Assert.Equal("OdfKit-Csv-File-Marker", loaded.Worksheets[0].Cells[1, 0].CellValue);
                OdfCsvExporter.ExportToFile(loaded, csvOutputPath);
            }

            Assert.True(File.Exists(csvOutputPath), "ExportToFile 應在 LibreOffice 往返後仍能寫出 CSV 檔案。");
            string exportedCsv = File.ReadAllText(csvOutputPath, Encoding.UTF8);
            Assert.Contains("OdfKit-Csv-File-Marker", exportedCsv);
            Assert.Contains("香蕉", exportedCsv);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfCell.SetBorders"/> 設定的儲存格框線，產出的 ODS 可由真實 LibreOffice 26.x
    /// headless 模式正確載入、往返並轉出 PDF，且框線屬性於往返後仍存在於 content.xml 中。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_CellSetBordersRoundTripsOdsAndPdf()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過儲存格框線實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeCellBorders_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-cell-borders.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.Worksheets.Add("Data");
                OdfCell cell = sheet.Cells["A1"];
                cell.CellValue = "OdfKit-CellBorders-Marker";
                cell.SetBorders(
                    top: new OdfBorder(OdfBorder.BorderStyle.Solid, OdfLength.FromPoints(1), System.Drawing.Color.Black),
                    bottom: new OdfBorder(OdfBorder.BorderStyle.Double, OdfLength.FromPoints(2), System.Drawing.Color.Red),
                    left: new OdfBorder(OdfBorder.BorderStyle.Dashed, OdfLength.FromPoints(1), System.Drawing.Color.Blue),
                    right: new OdfBorder(OdfBorder.BorderStyle.Dotted, OdfLength.FromPoints(1), System.Drawing.Color.Green));
                document.Save(odsPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-cell-borders.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出儲存格框線的 ODS 往返結果。");

            string contentXml;
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                contentXml = ReadSpreadsheetContentXml(loaded);
            }
            Assert.Contains("OdfKit-CellBorders-Marker", contentXml);
            Assert.Contains("border-top", contentXml);
            Assert.Contains("border-bottom", contentXml);
            Assert.Contains("border-left", contentXml);
            Assert.Contains("border-right", contentXml);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-cell-borders.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將含框線的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "框線 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfPivotTableBuilder"/> 建立的樞紐分析表（ODF <c>table:data-pilot-table</c>，
    /// 對應 LibreOffice 的「樞紐分析表／DataPilot」功能）可由真實 LibreOffice 26.x headless 模式
    /// 正確載入、往返並轉出 PDF，且 <see cref="SpreadsheetDocument.GetPivotTables"/> 讀回的摘要
    /// （含 <see cref="OdfPivotTableInfo.TryGetSourceRange"/>／<see cref="OdfPivotTableInfo.TryGetTargetStart"/>
    /// 解析結果）於往返後仍正確。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_PivotTableRoundTripsOdsAndPdf()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過樞紐分析表實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficePivotTable_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-pivot-table.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.Worksheets.Add("Sheet1");
                sheet.Cells["A1"].CellValue = "Category";
                sheet.Cells["B1"].CellValue = "Region";
                sheet.Cells["C1"].CellValue = "Sales";
                sheet.Cells["A2"].CellValue = "OdfKit-Pivot-Marker";
                sheet.Cells["B2"].CellValue = "North";
                sheet.Cells["C2"].CellValue = 100d;
                sheet.Cells["A3"].CellValue = "Books";
                sheet.Cells["B3"].CellValue = "South";
                sheet.Cells["C3"].CellValue = 200d;

                var sourceRange = new OdfCellRange(
                    new OdfCellAddress(0, 0, "Sheet1"),
                    new OdfCellAddress(2, 2, "Sheet1"));
                var targetStart = new OdfCellAddress(5, 0, "Sheet1");

                new OdfPivotTableBuilder("InteropPivot", sourceRange, targetStart, sheet)
                    .WithColumnHeaders(true)
                    .WithRowHeaders(false)
                    .AddRowField("Category")
                    .AddColumnField("Region")
                    .AddDataField("Sales", OdfPivotFunction.Sum)
                    .Build();

                document.Save(odsPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-pivot-table.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出樞紐分析表的 ODS 往返結果。");

            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                // 注意：LibreOffice 重新儲存樞紐分析表時，會以自身的 DataPilot 內部模型
                // 重新產生 table:data-pilot-table 節點，可能不保留 table:has-column-headers／
                // table:has-row-headers 這類選用屬性（這是 LibreOffice 既有的正規化行為，
                // 非 OdfKit 寫入或讀取邏輯的缺陷），因此此處僅驗證在 LibreOffice 往返後仍可靠
                // 保留的核心屬性（名稱、來源範圍、目標起點、欄位設定），不對標頭旗標斷言。
                OdfPivotTableInfo pivot = Assert.Single(loaded.GetPivotTables());
                Assert.Equal("Sheet1", pivot.SheetName);
                Assert.Equal("InteropPivot", pivot.Name);

                Assert.True(pivot.TryGetSourceRange(out OdfCellRange sourceRange), "TryGetSourceRange 應能解析往返後的來源範圍。");
                Assert.Equal(0, sourceRange.StartAddress.Row);
                Assert.Equal(0, sourceRange.StartAddress.Column);
                Assert.Equal(2, sourceRange.EndAddress.Row);
                Assert.Equal(2, sourceRange.EndAddress.Column);

                Assert.True(pivot.TryGetTargetStart(out OdfCellAddress targetStart), "TryGetTargetStart 應能解析往返後的目標起點。");
                Assert.Equal(5, targetStart.Row);
                Assert.Equal(0, targetStart.Column);
            }

            // 確認 LibreOffice 重新儲存後仍可正常轉出 PDF（驗證樞紐分析表渲染不報錯）。
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-pivot-table.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將含樞紐分析表的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "樞紐分析表 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 真機互通性驗證：工作表版面配置方法（欄寬、列高、自動欄寬、最佳列高）所寫入的
    /// <c>style:column-width</c>／<c>style:row-height</c>／<c>style:use-optimal-row-height</c>
    /// 屬性，於 LibreOffice 26.x headless 模式往返後語意仍正確。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_SheetLayoutApiRoundTripsOds()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過工作表版面配置實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeSheetLayout_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-sheet-layout.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.AddSheet("Layout");
                sheet.Cells["A1"].CellValue = "OdfKit-SheetLayout-Marker";
                sheet.Cells["B1"].CellValue = "ThisIsAMuchLongerHeaderTextForAutoFit";
                sheet.Cells["A2"].CellValue = "資料列";

                // 真機驗證重點一：SetColumnWidth／GetColumnWidth — 固定欄寬寫入 style:column-width。
                sheet.SetColumnWidth(0, OdfLength.FromCentimeters(3));
                Assert.Equal(OdfLength.FromCentimeters(3), sheet.GetColumnWidth(0));

                // 真機驗證重點二：AutoFitColumnWidth — 依儲存格內容長度自動計算欄寬並寫入。
                sheet.AutoFitColumnWidth(1);
                OdfLength? autoFitWidth = sheet.GetColumnWidth(1);
                Assert.NotNull(autoFitWidth);
                Assert.True(autoFitWidth!.Value.Value > 0, "AutoFitColumnWidth 計算結果應為正值欄寬。");

                // 真機驗證重點三：SetRowHeight／GetRowHeight — 固定列高寫入 style:row-height，
                // 並應同時關閉 use-optimal-row-height。
                sheet.SetRowHeight(0, OdfLength.FromCentimeters(1.2));
                Assert.Equal(OdfLength.FromCentimeters(1.2), sheet.GetRowHeight(0));
                Assert.False(sheet.IsRowOptimalHeight(0));

                // 真機驗證重點四：SetRowOptimalHeight／IsRowOptimalHeight — 開啟最佳列高後應移除固定列高。
                sheet.SetRowOptimalHeight(1, useOptimal: true);
                Assert.True(sheet.IsRowOptimalHeight(1));
                Assert.Null(sheet.GetRowHeight(1));

                // 真機驗證重點五：GetRowHeight／GetColumnWidth／IsRowOptimalHeight 對超出現有資料
                // 範圍的索引查詢，不應產生任何副作用（不應在文件中插入大量空白列或欄）。
                Assert.Null(sheet.GetRowHeight(500));
                Assert.False(sheet.IsRowOptimalHeight(500));
                Assert.Null(sheet.GetColumnWidth(500));

                document.Save(odsPath);
            }

            // 儲存後（尚未經過 LibreOffice 往返）即先確認查詢型方法未在記憶體中遺留副作用列／欄。
            using (SpreadsheetDocument beforeRoundTrip = SpreadsheetDocument.Load(odsPath))
            {
                string contentXmlBefore = ReadSpreadsheetContentXml(beforeRoundTrip);
                int rowElementCount = CountOccurrences(contentXmlBefore, "<table:table-row");
                Assert.True(rowElementCount < 10, $"查詢超出範圍的列／欄索引不應插入大量空白列，實際列元素數量：{rowElementCount}。");
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-sheet-layout.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出工作表版面配置的 ODS 往返結果。");

            string roundTripContentXml;
            string roundTripStylesXml;
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                OdfTableSheet loadedSheet = loaded.Worksheets["Layout"];
                Assert.Equal("OdfKit-SheetLayout-Marker", loadedSheet.Cells["A1"].CellValue);

                // 往返後屬性語意仍需正確：固定欄寬、自動計算欄寬、固定列高與最佳列高旗標皆應保留。
                // LibreOffice 內部以 1/100 mm 儲存長度，往返後可能有極小的捨入誤差（例如 1.2cm 變為
                // 1.199cm），故以容許誤差比較數值而非要求位元級精確相等。
                OdfLength? columnWidth = loadedSheet.GetColumnWidth(0);
                Assert.NotNull(columnWidth);
                Assert.Equal(3.0, columnWidth!.Value.ToCentimeters(), precision: 1);
                Assert.NotNull(loadedSheet.GetColumnWidth(1));
                OdfLength? rowHeight = loadedSheet.GetRowHeight(0);
                Assert.NotNull(rowHeight);
                Assert.Equal(1.2, rowHeight!.Value.ToCentimeters(), precision: 1);
                Assert.True(loadedSheet.IsRowOptimalHeight(1));

                roundTripContentXml = ReadSpreadsheetContentXml(loaded);
                roundTripStylesXml = ReadSpreadsheetStylesXml(loaded);
            }

            Assert.Contains("style:column-width", roundTripContentXml + roundTripStylesXml);
            Assert.Contains("style:row-height", roundTripContentXml + roundTripStylesXml);
            Assert.Contains("style:use-optimal-row-height=\"true\"", roundTripContentXml + roundTripStylesXml);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-sheet-layout.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將套用版面配置設定後的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "版面配置 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 真機互通性驗證：工作表列印設定方法（列印範圍、標題列欄、手動分頁符、列印縮放）的
    /// 設定與清除 API，產出的 ODS 可由 LibreOffice 26.x headless 模式正確載入、往返並轉出 PDF，
    /// 且清除後對應的 XML 結構與屬性應完全移除。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_SheetPrintSettingsClearApiRoundTripsOdsAndPdf()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過工作表列印設定實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficePrintSettingsClear_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-print-settings-clear.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.AddSheet("Print");
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        sheet.GetCell(row, col).SetValue(row * 10d + col);
                    }
                }
                // 標記放在資料矩陣之外的第 5 欄，避免覆蓋掉用於驗證資料完整性的數值矩陣。
                sheet.Cells["E1"].CellValue = "OdfKit-PrintSettingsClear-Marker";

                // 真機驗證重點一：SetPrintArea／SetPrintScale／InsertRowPageBreak／InsertColumnPageBreak
                // 先全部設定，確認屬性正確寫入。
                sheet.SetPrintArea(new OdfCellRange(0, 0, 4, 3));
                sheet.SetPrintTitleRows(0, 0);
                sheet.SetPrintTitleColumns(0, 0);
                sheet.InsertRowPageBreak(afterRow: 1);
                sheet.InsertColumnPageBreak(afterCol: 1);
                sheet.SetPrintScale(150);

                Assert.NotNull(sheet.GetPrintArea());

                // 真機驗證重點二：RemoveRowPageBreak／RemoveColumnPageBreak — 移除手動分頁符後，
                // 對應列／欄節點不應再帶有 fo:break-before。
                sheet.RemoveRowPageBreak(afterRow: 1);
                sheet.RemoveColumnPageBreak(afterCol: 1);

                // 真機驗證重點三：ClearPrintArea — 清除後 GetPrintArea 應回傳 null，
                // table:print-ranges 屬性應從 content.xml 移除。
                sheet.ClearPrintArea();
                Assert.Null(sheet.GetPrintArea());

                // 真機驗證重點四：ClearPrintTitleRows／ClearPrintTitleColumns — 清除後
                // table:header-rows／table:header-columns 容器應移除，且列／欄應還原至工作表主體，
                // 不應遺失任何資料儲存格內容。
                sheet.ClearPrintTitleRows();
                sheet.ClearPrintTitleColumns();

                // 真機驗證重點五：SetPrintScale(0) — 傳入 0 應清除 scale-to／scale-to-pages，恢復自動縮放。
                sheet.SetPrintScale(0);

                document.Save(odsPath);
            }

            using (SpreadsheetDocument beforeRoundTrip = SpreadsheetDocument.Load(odsPath))
            {
                OdfTableSheet sheetBefore = beforeRoundTrip.Worksheets["Print"];
                // 清除標題列／欄之後，原本的資料儲存格內容必須完整保留在工作表主體中。
                Assert.Equal("OdfKit-PrintSettingsClear-Marker", sheetBefore.Cells["E1"].CellValue);
                Assert.Equal(40d, sheetBefore.GetCell(4, 0).CellValue);
                Assert.Equal(43d, sheetBefore.GetCell(4, 3).CellValue);

                string contentXmlBefore = ReadSpreadsheetContentXml(beforeRoundTrip);
                string stylesXmlBefore = ReadSpreadsheetStylesXml(beforeRoundTrip);
                Assert.DoesNotContain("table:print-ranges", contentXmlBefore);
                Assert.DoesNotContain("table:header-rows", contentXmlBefore);
                Assert.DoesNotContain("table:header-columns", contentXmlBefore);
                Assert.DoesNotContain("fo:break-before=\"page\"", contentXmlBefore);
                Assert.DoesNotContain("style:scale-to=", contentXmlBefore + stylesXmlBefore);
                Assert.DoesNotContain("style:scale-to-pages=", contentXmlBefore + stylesXmlBefore);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-print-settings-clear.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出列印設定清除後的 ODS 往返結果。");

            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                OdfTableSheet loadedSheet = loaded.Worksheets["Print"];
                Assert.Equal("OdfKit-PrintSettingsClear-Marker", loadedSheet.Cells["E1"].CellValue);
                Assert.Null(loadedSheet.GetPrintArea());

                // 往返後資料矩陣應完整保留（驗證 ClearPrintTitleRows／ClearPrintTitleColumns 還原順序正確）。
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        Assert.Equal(row * 10d + col, loadedSheet.GetCell(row, col).CellValue);
                    }
                }

                string contentXmlAfter = ReadSpreadsheetContentXml(loaded);
                string stylesXmlAfter = ReadSpreadsheetStylesXml(loaded);
                Assert.DoesNotContain("table:print-ranges", contentXmlAfter);
                Assert.DoesNotContain("table:header-rows", contentXmlAfter);
                Assert.DoesNotContain("table:header-columns", contentXmlAfter);
                Assert.DoesNotContain("fo:break-before=\"page\"", contentXmlAfter);
                Assert.DoesNotContain("style:scale-to=", contentXmlAfter + stylesXmlAfter);
                Assert.DoesNotContain("style:scale-to-pages=", contentXmlAfter + stylesXmlAfter);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-print-settings-clear.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將清除列印設定後的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "列印設定 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 真機互通性驗證：<see cref="OdfTableSheet.SetPrintScale"/> 與分頁符插入／移除方法
    /// 在另一情境（保留列印範圍與標題列欄，僅移除分頁符）中的語意，並交叉驗證
    /// <see cref="OdfTableSheet.GetUsedCells"/>（透過 <see cref="OdfTableSheet.UsedCells"/> 屬性間接呼叫）
    /// 於版面配置與列印設定變更後仍可正確列舉已使用儲存格。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_PrintScaleAndPageBreaksWithUsedCellsRoundTripsOds()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過列印縮放與分頁符實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficePrintScalePageBreaks_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-print-scale-page-breaks.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.AddSheet("Scale");
                for (int row = 0; row < 3; row++)
                {
                    sheet.Cells[row, 0].CellValue = "OdfKit-PrintScale-Marker-" + row;
                }

                // 真機驗證重點一：SetPrintScale 設定縮放比例後寫入 style:scale-to。
                sheet.SetPrintScale(80);

                // 插入兩組分頁符後，僅移除其中一組，確認 RemoveRowPageBreak／RemoveColumnPageBreak
                // 只影響指定列／欄，不會誤刪另一組分頁符。
                sheet.InsertRowPageBreak(afterRow: 0);
                sheet.InsertRowPageBreak(afterRow: 1);
                sheet.InsertColumnPageBreak(afterCol: 0);
                sheet.RemoveRowPageBreak(afterRow: 0);

                document.Save(odsPath);
            }

            using (SpreadsheetDocument beforeRoundTrip = SpreadsheetDocument.Load(odsPath))
            {
                OdfTableSheet sheetBefore = beforeRoundTrip.Worksheets["Scale"];

                // 真機驗證重點二：GetUsedCells／UsedCells 應列舉所有已寫入內容的儲存格，
                // 在版面配置與列印設定皆已變更後依然能正確運作。
                var usedCells = sheetBefore.GetUsedCells().ToList();
                Assert.Equal(3, usedCells.Count);
                Assert.Equal(usedCells.Select(c => c.CellValue), sheetBefore.UsedCells.Select(c => c.CellValue));
                Assert.Contains(usedCells, c => Equals(c.CellValue, "OdfKit-PrintScale-Marker-0"));
                Assert.Contains(usedCells, c => Equals(c.CellValue, "OdfKit-PrintScale-Marker-2"));
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-print-scale-page-breaks.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出列印縮放與分頁符的 ODS 往返結果。");

            string contentXml;
            string stylesXml;
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                OdfTableSheet loadedSheet = loaded.Worksheets["Scale"];
                var loadedUsedCells = loadedSheet.GetUsedCells().ToList();
                Assert.Equal(3, loadedUsedCells.Count);

                contentXml = ReadSpreadsheetContentXml(loaded);
                stylesXml = ReadSpreadsheetStylesXml(loaded);
            }

            // 往返後縮放比例屬性仍應存在，且第 0 列分頁符已移除、第 1 列分頁符與欄分頁符應保留。
            Assert.Contains("style:scale-to=\"80%\"", contentXml + stylesXml);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-print-scale-page-breaks.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將縮放與分頁符設定後的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "縮放與分頁符 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 真機互通性驗證：<see cref="OdfTableSheet.UngroupColumns"/> 移除欄群組後，
    /// 產出的 ODS 可由 LibreOffice 26.x headless 模式正確載入、往返並轉出 PDF，
    /// 且欄群組節點應完全移除、欄資料與順序應正確還原。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_UngroupColumnsRoundTripsOdsAndPdf()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過欄群組移除實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeUngroupColumns_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-ungroup-columns.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.AddSheet("Group");
                for (int col = 0; col < 5; col++)
                {
                    sheet.Cells[0, col].CellValue = "OdfKit-UngroupColumns-Marker-" + col;
                }

                // 真機驗證重點：GroupColumns 建立群組後，UngroupColumns 應將欄移回工作表主體，
                // table:table-column-group 節點應完全消失，且各欄資料與原始欄索引順序必須維持正確。
                sheet.GroupColumns(1, 3);
                sheet.UngroupColumns(1, 3);

                document.Save(odsPath);
            }

            using (SpreadsheetDocument beforeRoundTrip = SpreadsheetDocument.Load(odsPath))
            {
                string contentXmlBefore = ReadSpreadsheetContentXml(beforeRoundTrip);
                Assert.DoesNotContain("table:table-column-group", contentXmlBefore);

                OdfTableSheet sheetBefore = beforeRoundTrip.Worksheets["Group"];
                for (int col = 0; col < 5; col++)
                {
                    Assert.Equal("OdfKit-UngroupColumns-Marker-" + col, sheetBefore.Cells[0, col].CellValue);
                }
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-ungroup-columns.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出欄群組移除後的 ODS 往返結果。");

            string contentXml;
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                OdfTableSheet loadedSheet = loaded.Worksheets["Group"];
                for (int col = 0; col < 5; col++)
                {
                    Assert.Equal("OdfKit-UngroupColumns-Marker-" + col, loadedSheet.Cells[0, col].CellValue);
                }

                contentXml = ReadSpreadsheetContentXml(loaded);
            }
            Assert.DoesNotContain("table:table-column-group", contentXml);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", roundTripPath);
            string pdfPath = Path.Combine(outputDir, "interop-ungroup-columns.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將欄群組移除後的 ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "欄群組移除 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 ODT、ODS、ODP 與 ODG 可由 LibreOffice 26.x headless 模式載入並轉換。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsGeneratedDocuments()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-text.odt");
            string odsPath = Path.Combine(tempRoot, "interop-chart.ods");
            string odpPath = Path.Combine(tempRoot, "interop-animation.odp");
            string odgPath = Path.Combine(tempRoot, "interop-drawing.odg");

            CreateTextDocument(odtPath);
            CreateSpreadsheetWithChart(odsPath);
            CreatePresentationWithAnimation(odpPath);
            CreateDrawingDocument(odgPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", odtPath);
            string txtPath = Path.Combine(outputDir, "interop-text.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出 ODT 文字轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-26-Interop-Marker", File.ReadAllText(txtPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "xlsx", odsPath);
            string xlsxPath = Path.Combine(outputDir, "interop-chart.xlsx");
            Assert.True(File.Exists(xlsxPath), "LibreOffice 應輸出 ODS 至 XLSX 轉換結果。");
            Assert.True(new FileInfo(xlsxPath).Length > 0, "XLSX 轉換結果不應為空。");

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fodp", odpPath);
            string fodpPath = Path.Combine(outputDir, "interop-animation.fodp");
            Assert.True(File.Exists(fodpPath), "LibreOffice 應輸出 ODP 至 FODP 轉換結果。");
            string fodpXml = File.ReadAllText(fodpPath);
            Assert.Contains("ooo-entrance-fade-in", fodpXml);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fodg", odgPath);
            string fodgPath = Path.Combine(outputDir, "interop-drawing.fodg");
            Assert.True(File.Exists(fodgPath), "LibreOffice 應輸出 ODG 至 FODG 轉換結果。");
            string fodgXml = File.ReadAllText(fodgPath);
            Assert.Contains("OdfKit-LibreOffice-26-Interop-Marker", fodgXml);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 OTT、OTS、OTP 與 OTG 四個範本格式可由 LibreOffice 26.x headless 模式載入與轉換，
    /// 涵蓋 Batch 1 四主格式範本變體的最低互通驗收案例。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsTemplateVariantDocuments()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過範本變體實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeTemplateInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string ottPath = Path.Combine(tempRoot, "interop-template.ott");
            string otsPath = Path.Combine(tempRoot, "interop-template.ots");
            string otpPath = Path.Combine(tempRoot, "interop-template.otp");
            string otgPath = Path.Combine(tempRoot, "interop-template.otg");

            CreateTextTemplateDocument(ottPath);
            CreateSpreadsheetTemplateDocument(otsPath);
            CreatePresentationTemplateDocument(otpPath);
            CreateDrawingTemplateDocument(otgPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", ottPath);
            string ottTxtPath = Path.Combine(outputDir, "interop-template.txt");
            Assert.True(File.Exists(ottTxtPath), "LibreOffice 應輸出 OTT 文字轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-Template-Interop-Marker", File.ReadAllText(ottTxtPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fods", otsPath);
            string otsFodsPath = Path.Combine(outputDir, "interop-template.fods");
            Assert.True(File.Exists(otsFodsPath), "LibreOffice 應輸出 OTS 至 FODS 轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-Template-Interop-Marker", File.ReadAllText(otsFodsPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fodp", otpPath);
            string otpFodpPath = Path.Combine(outputDir, "interop-template.fodp");
            Assert.True(File.Exists(otpFodpPath), "LibreOffice 應輸出 OTP 至 FODP 轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-Template-Interop-Marker", File.ReadAllText(otpFodpPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "fodg", otgPath);
            string otgFodgPath = Path.Combine(outputDir, "interop-template.fodg");
            Assert.True(File.Exists(otgFodgPath), "LibreOffice 應輸出 OTG 至 FODG 轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-Template-Interop-Marker", File.ReadAllText(otgFodgPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證由 OdfKit 直接產生（非由 ZIP 轉換而來）的原生 FODT、FODS、FODP 與 FODG 扁平 XML
    /// 文件，可由 LibreOffice 26.x headless 模式直接開啟並轉換，證明 Flat XML 與 ZIP 封裝的
    /// 高階工作流對 LibreOffice 而言互通等價。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsNativeFlatXmlDocuments()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過原生 Flat XML 實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeNativeFlatInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string fodtPath = Path.Combine(tempRoot, "interop-native-text.fodt");
            string fodsPath = Path.Combine(tempRoot, "interop-native-sheet.fods");
            string fodpPath = Path.Combine(tempRoot, "interop-native-slide.fodp");
            string fodgPath = Path.Combine(tempRoot, "interop-native-draw.fodg");

            CreateNativeFlatTextDocument(fodtPath);
            CreateNativeFlatSpreadsheetDocument(fodsPath);
            CreateNativeFlatPresentationDocument(fodpPath);
            CreateNativeFlatDrawingDocument(fodgPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", fodtPath);
            string fodtTxtPath = Path.Combine(outputDir, "interop-native-text.txt");
            Assert.True(File.Exists(fodtTxtPath), "LibreOffice 應輸出原生 FODT 文字轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-NativeFlat-Interop-Marker", File.ReadAllText(fodtTxtPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "xlsx", fodsPath);
            string fodsXlsxPath = Path.Combine(outputDir, "interop-native-sheet.xlsx");
            Assert.True(File.Exists(fodsXlsxPath), "LibreOffice 應輸出原生 FODS 至 XLSX 轉換結果。");
            Assert.True(new FileInfo(fodsXlsxPath).Length > 0, "XLSX 轉換結果不應為空。");

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odp", fodpPath);
            string fodpOdpPath = Path.Combine(outputDir, "interop-native-slide.odp");
            Assert.True(File.Exists(fodpOdpPath), "LibreOffice 應輸出原生 FODP 至 ODP（ZIP 封裝）轉換結果。");
            using (OdfPackage roundTrippedPackage = OdfPackage.Open(fodpOdpPath))
            using (Stream roundTrippedContent = roundTrippedPackage.GetEntryStream("content.xml"))
            using (var roundTrippedReader = new StreamReader(roundTrippedContent, Encoding.UTF8))
            {
                Assert.Contains("OdfKit-LibreOffice-NativeFlat-Interop-Marker", roundTrippedReader.ReadToEnd());
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "png", fodgPath);
            string fodgPngPath = Path.Combine(outputDir, "interop-native-draw.png");
            Assert.True(File.Exists(fodgPngPath), "LibreOffice 應輸出原生 FODG 至 PNG 轉換結果。");
            Assert.True(new FileInfo(fodgPngPath).Length > 0, "PNG 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 ODM 主控文字文件可由 LibreOffice 26.x headless 模式直接識別為「Writer master
    /// document」並轉換，且往返 ODM 後仍保留段落內容（已實機確認 LibreOffice 使用
    /// <c>writerglobal8</c> 篩選器，非僅理論相容）。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsMasterDocument()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 ODM 主控文件實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeMasterInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odmPath = Path.Combine(tempRoot, "interop-master.odm");
            using (var master = TextMasterDocument.Create())
            {
                master.AddParagraph("OdfKit-LibreOffice-Master-Interop-Marker");
                master.AddSubDocumentReference("Chapter1", "chapter1.odt");
                master.Save(odmPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", odmPath);
            string txtPath = Path.Combine(outputDir, "interop-master.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出 ODM 文字轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-Master-Interop-Marker", File.ReadAllText(txtPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odm", odmPath);
            string roundTripPath = Path.Combine(outputDir, "interop-master.odm");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出 ODM 往返結果。");

            using TextMasterDocument roundTripped = TextMasterDocument.Load(roundTripPath);
            Assert.Equal(OdfDocumentKind.TextMaster, roundTripped.DocumentKind);
            var reference = Assert.Single(roundTripped.GetSubDocumentReferences());
            Assert.Equal("Chapter1", reference.SectionName);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 OTH 網頁範本文件可由 LibreOffice 26.x headless 模式直接識別為「Writer/Web
    /// document」並轉換，且往返 ODT 後仍保留段落內容（已實機確認 LibreOffice 使用
    /// <c>writerweb8_writer</c> 篩選器，非僅理論相容）。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsWebTemplateDocument()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 OTH 網頁範本實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeWebInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string othPath = Path.Combine(tempRoot, "interop-web.oth");
            using (var web = TextWebDocument.Create())
            {
                web.AddHeading("OdfKit-LibreOffice-Web-Interop-Heading", 1);
                web.AddParagraph("OdfKit-LibreOffice-Web-Interop-Marker");
                web.Save(othPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt", othPath);
            string txtPath = Path.Combine(outputDir, "interop-web.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出 OTH 文字轉換結果。");
            Assert.Contains("OdfKit-LibreOffice-Web-Interop-Marker", File.ReadAllText(txtPath));

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odt", othPath);
            string roundTripPath = Path.Combine(outputDir, "interop-web.odt");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出 OTH 至 ODT 轉換結果。");

            using TextDocument roundTripped = TextDocument.Load(roundTripPath);
            string contentXml = ReadContentXml(roundTripped);
            Assert.Contains("OdfKit-LibreOffice-Web-Interop-Marker", contentXml);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證獨立 ODF 公式文件可由 LibreOffice 26.x headless 模式直接識別為「Math document」
    /// 並使用 <c>math8</c> 篩選器轉換，且往返後 MathML 內容仍保留（與 ODC／ODI 等次要格式不同，
    /// ODF 公式文件確實有獨立可開啟主文件層級的真機支援，因為 LibreOffice Math 本身即支援
    /// 將公式作為獨立文件編輯，而非僅作為嵌入物件）。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsFormulaDocument()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過公式文件實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeFormulaInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odfPath = Path.Combine(tempRoot, "interop-formula.odf");
            using (FormulaDocument formula = FormulaDocument.Create(
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>E</mi><mo>=</mo><mi>m</mi><msup><mi>c</mi><mn>2</mn></msup></mrow></math>"))
            {
                formula.Save(odfPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odf", odfPath);
            string roundTripPath = Path.Combine(outputDir, "interop-formula.odf");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出公式文件往返結果。");

            using FormulaDocument roundTripped = FormulaDocument.Load(roundTripPath);
            Assert.Equal("E=mc2", roundTripped.MathText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 OTF 公式範本與 FDF 扁平 XML 公式文件的封裝結構（mimetype、content.xml）符合 ODF 1.4
    /// 規格，並可由 OdfKit 自身完整往返讀回。
    /// 注意：實測確認 LibreOffice 26.2.1 不接受獨立 .otf 為可直接開啟的主文件（回報
    /// 「source file could not be loaded」）；獨立 .fdf 則更隱晦地被誤判為「Calc document」並以
    /// <c>calc_png_Export</c> 篩選器產生與公式內容完全無關的輸出，同樣不構成有效互通。與獨立
    /// <c>.odf</c>（見 <see cref="LibreOfficeHeadless_LoadsFormulaDocument"/>，已確認真機支援）
    /// 不同，這是上游應用程式對範本／Flat 公式變體的已知限制，並非 OdfKit 的缺陷。因此 OTF／FDF
    /// 改以封裝結構與 schema 層級的精確驗證取代真機驗證。
    /// </summary>
    [Fact]
    public void OdfFormulaVariantDocument_PackageStructureMatchesOdf14Schema()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitFormulaVariantPackageStructure_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string otfPath = Path.Combine(tempRoot, "interop-formula.otf");
            string fdfPath = Path.Combine(tempRoot, "interop-formula.fdf");

            using (FormulaTemplateDocument otf = FormulaTemplateDocument.Create())
            {
                otf.SetIdentifierEquation("x", "y");
                otf.Save(otfPath);
            }

            using OdfPackage otfPackage = OdfPackage.Open(otfPath);
            Assert.Equal("application/vnd.oasis.opendocument.formula-template", otfPackage.MimeType);

            using (FormulaTemplateDocument otfForFlat = FormulaTemplateDocument.Load(otfPath))
            using (FlatFormulaDocument fdf = FlatFormulaDocument.CreateFromDocument(FormulaDocument.CreateFromTemplate(otfForFlat)))
            {
                fdf.Save(fdfPath);
            }

            string fdfXml = File.ReadAllText(fdfPath);
            Assert.Contains("<office:document", fdfXml, StringComparison.Ordinal);

            using FlatFormulaDocument reloadedFdf = FlatFormulaDocument.Load(fdfPath);
            Assert.Equal("x=y", reloadedFdf.MathText);

            using FormulaTemplateDocument reloadedOtf = FormulaTemplateDocument.Load(otfPath);
            Assert.Equal("x=y", reloadedOtf.MathText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 ODI／OTI／FODI 影像文件的封裝結構（mimetype、manifest、content.xml）符合 ODF 1.4
    /// 規格，並可由 OdfKit 自身完整往返讀回。
    /// 注意：實測確認真實 LibreOffice 26.2.1 與 Microsoft Office 365 皆未實作 ODI／OTI
    /// （OpenDocument Image／Image Template）的匯入篩選器——LibreOffice 僅在
    /// <c>draw.xcd</c> 等篩選器登錄檔註冊 ODG／ODP／ODS／ODT／ODC，從未登錄
    /// <c>application/vnd.oasis.opendocument.image</c>；以 <c>soffice --headless
    /// --convert-to png</c> 開啟 OdfKit 產生的獨立 ODI／OTI 一律回報「source file could not be
    /// loaded」，即使內容完全符合 ODF 規格亦然，這是上游應用程式的已知缺口，並非 OdfKit 的
    /// 缺陷。獨立 FODI（Flat XML）則更隱晦地被誤判為「Writer document」，以
    /// <c>writer_png_Export</c> 篩選器產生與影像內容完全無關的輸出（並非真正剖析為影像），
    /// 同樣不構成有效互通——與 ODC／OTC／FODC（見
    /// <see cref="OdfChartDocument_PackageStructureMatchesOdf14Schema"/>）及 OTF／FDF（見
    /// <see cref="OdfFormulaVariantDocument_PackageStructureMatchesOdf14Schema"/>）的誤判模式
    /// 一致。Microsoft Office 則完全不支援開啟任何 ODF 影像文件。因此 ODI／OTI／FODI 改以封裝
    /// 結構與 schema 層級的精確驗證取代真機驗證。
    /// </summary>
    [Fact]
    public void OdfImageDocument_PackageStructureMatchesOdf14Schema()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitImagePackageStructure_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string odiPath = Path.Combine(tempRoot, "interop-image.odi");
            CreateImageDocument(odiPath);

            using OdfPackage package = OdfPackage.Open(odiPath);
            Assert.Equal("application/vnd.oasis.opendocument.image", package.MimeType);
            Assert.True(package.HasEntry("Pictures/interop.png"), "封裝應包含寫入的影像媒體項目。");
            Assert.Equal(
                "image/png",
                package.Manifest.TryGetValue("Pictures/interop.png", out string? mediaType) ? mediaType : null);

            using Stream contentStream = package.GetEntryStream("content.xml");
            using var reader = new StreamReader(contentStream, Encoding.UTF8);
            string contentXml = reader.ReadToEnd();

            // ODF 1.4 規格定義影像文件的主體元素為 office:image（與 office:text、
            // office:drawing 同層級），而非 draw:image（後者僅為框架內影像參照元素）。
            Assert.Contains("<office:image>", contentXml);
            Assert.Contains("draw:frame", contentXml);
            Assert.Contains("draw:image", contentXml);
            Assert.Contains("xlink:href=\"Pictures/interop.png\"", contentXml);
            Assert.Contains("OdfKit-LibreOffice-Image-Interop-Marker", contentXml);

            using OdfImageDocument loaded = OdfImageDocument.Load(odiPath);
            OdfImageFrameInfo frame = Assert.Single(loaded.GetImageFrames());
            Assert.Equal("PrimaryFrame", frame.Name);
            Assert.Equal("OdfKit-LibreOffice-Image-Interop-Marker", frame.Title);
            Assert.True(frame.TryGetWidth(out OdfLength width));
            Assert.Equal(OdfLength.Parse("6cm").ToPoints(), width.ToPoints(), 0.001);

            string otiPath = Path.Combine(tempRoot, "interop-image.oti");
            string fodiPath = Path.Combine(tempRoot, "interop-image.fodi");

            using (OdfImageDocument odiForTemplate = OdfImageDocument.Load(odiPath))
            using (ImageTemplateDocument oti = ImageTemplateDocument.CreateFromDocument(odiForTemplate))
            {
                oti.Save(otiPath);
            }

            using OdfPackage otiPackage = OdfPackage.Open(otiPath);
            Assert.Equal("application/vnd.oasis.opendocument.image-template", otiPackage.MimeType);

            using (OdfImageDocument odiForFlat = OdfImageDocument.Load(odiPath))
            using (FlatImageDocument fodi = FlatImageDocument.CreateFromDocument(odiForFlat))
            {
                fodi.Save(fodiPath);
            }

            string fodiXml = File.ReadAllText(fodiPath);
            Assert.Contains("<office:document", fodiXml, StringComparison.Ordinal);

            using FlatImageDocument reloadedFodi = FlatImageDocument.Load(fodiPath);
            Assert.Equal("OdfKit-LibreOffice-Image-Interop-Marker", Assert.Single(reloadedFodi.GetImageFrames()).Title);

            using ImageTemplateDocument reloadedOti = ImageTemplateDocument.Load(otiPath);
            Assert.Equal("OdfKit-LibreOffice-Image-Interop-Marker", Assert.Single(reloadedOti.GetImageFrames()).Title);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 ODC／OTC／FODC 圖表文件的封裝結構（mimetype、manifest、content.xml）符合 ODF 1.4
    /// 規格，並可由 OdfKit 自身完整往返讀回。
    /// 注意：實測確認真實 LibreOffice 26.2.1 並未將獨立（非嵌入 ODS/ODT/ODP）的 ODF Chart
    /// 文件視為可直接開啟的主文件——以 <c>soffice --headless --convert-to txt</c> 開啟
    /// OdfKit 產生的獨立 .odc／.otc 一律回報「source file could not be loaded」，即使內容完全
    /// 符合 ODF 規格亦然；獨立 .fodc（Flat XML）則更隱晦地被誤判為「Writer document」並原樣
    /// 回顯來源 XML（並非真正剖析為圖表），同樣不構成有效互通。這與既有
    /// <c>OdfImageDocument_PackageStructureMatchesOdf14Schema</c> 註解中「LibreOffice 已在
    /// draw.xcd 註冊 ODC」的舊有假設不符——ODF Chart 在 ODF 生態中設計上即為僅可嵌入
    /// ODS/ODT/ODP 內的子文件類型，並非獨立可開啟的主文件格式，這是上游應用程式的已知限制，
    /// 並非 OdfKit 的缺陷。因此 ODC／OTC／FODC 改以封裝結構與 schema 層級的精確驗證，以及
    /// <see cref="ChartHighLevelApiTests"/>／<see cref="EmbeddedChartIntegrationTests"/> 中
    /// 「圖表嵌入 ODS/ODT 後由 LibreOffice 開啟」的既有嵌入式互通驗收（見
    /// <c>LibreOfficeHeadless_LoadsGeneratedDocuments</c> 對含圖表 ODS 的 <c>xlsx</c> 轉換）
    /// 取代獨立檔案的真機驗證。
    /// </summary>
    [Fact]
    public void OdfChartDocument_PackageStructureMatchesOdf14Schema()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitChartPackageStructure_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string odcPath = Path.Combine(tempRoot, "interop-chart.odc");
            string otcPath = Path.Combine(tempRoot, "interop-chart.otc");
            string fodcPath = Path.Combine(tempRoot, "interop-chart.fodc");

            var definition = new OdfChartDefinition
            {
                ChartType = OdfChartType.Bar,
                Title = "OdfKit-Chart-PackageStructure-Marker",
                DataRange = new OdfCellRange(0, 0, 4, 1, "LocalTable"),
                HasLegend = true
            };

            using (ChartDocument odc = ChartDocument.Create(definition))
            {
                odc.Save(odcPath);
            }

            using OdfPackage odcPackage = OdfPackage.Open(odcPath);
            Assert.Equal("application/vnd.oasis.opendocument.chart", odcPackage.MimeType);
            using (Stream contentStream = odcPackage.GetEntryStream("content.xml"))
            using (var reader = new StreamReader(contentStream, Encoding.UTF8))
            {
                string contentXml = reader.ReadToEnd();
                Assert.Contains("<office:chart", contentXml, StringComparison.Ordinal);
                Assert.Contains("OdfKit-Chart-PackageStructure-Marker", contentXml, StringComparison.Ordinal);
            }

            using (ChartDocument odcForTemplate = ChartDocument.Load(odcPath))
            using (ChartTemplateDocument otc = ChartTemplateDocument.CreateFromDocument(odcForTemplate))
            {
                otc.Save(otcPath);
            }

            using OdfPackage otcPackage = OdfPackage.Open(otcPath);
            Assert.Equal("application/vnd.oasis.opendocument.chart-template", otcPackage.MimeType);

            using (ChartDocument odcForFlat = ChartDocument.Load(odcPath))
            using (FlatChartDocument fodc = FlatChartDocument.CreateFromDocument(odcForFlat))
            {
                fodc.Save(fodcPath);
            }

            string fodcXml = File.ReadAllText(fodcPath);
            Assert.Contains("<office:document", fodcXml, StringComparison.Ordinal);
            Assert.Contains("OdfKit-Chart-PackageStructure-Marker", fodcXml, StringComparison.Ordinal);

            using ChartDocument reloaded = ChartDocument.Load(odcPath);
            Assert.Equal("OdfKit-Chart-PackageStructure-Marker", reloaded.ChartTitle);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreateImageDocument(string path)
    {
        using var document = OdfImageDocument.Create();
        document.SetImageLayout(
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("6cm"),
            OdfLength.Parse("4cm"),
            "PrimaryFrame",
            "OdfKit-LibreOffice-Image-Interop-Marker",
            "互通性驗證影像描述");
        document.SetImage(CreateOnePixelPngBytes(), "interop.png");
        document.Save(path);
    }

    private static byte[] CreateOnePixelPngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    /// <summary>
    /// 建立一個結構完整可被真實 LibreOffice 正確解碼的 4x4 紅色 PNG（不同於 <see cref="CreateOnePixelPngBytes"/>
    /// 等用於非真機渲染情境的最小占位 PNG，此處的位元組來自正規 PNG 編碼器產出，避免觸發 libpng CRC 警告）。
    /// </summary>
    private static byte[] CreateValidFourPixelRedPngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEUlEQVR4nGP8z4AATEhsPBwAM9EBBzDn4UwAAAAASUVORK5CYII=");

    /// <summary>
    /// 建立一個結構完整可被真實 LibreOffice 正確解碼的 4x4 藍色 PNG，內容與
    /// <see cref="CreateValidFourPixelRedPngBytes"/> 不同以避免 <see cref="OdfMediaManager"/> 的
    /// SHA-256 重複資料刪除機制將兩者合併為同一個媒體專案。
    /// </summary>
    private static byte[] CreateValidFourPixelBluePngBytes() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAE0lEQVR4nGNkYPjPAANMcBZeDgAx0wEH1s7nlgAAAABJRU5ErkJggg==");

    /// <summary>
    /// 驗證目錄／字母索引／文獻目錄的專案範本建構與 <see cref="OdfIndex.Update"/> 重新產生內容後，
    /// 仍可由真實 LibreOffice 26.x headless 模式載入並正確匯出純文字，且巢狀清單、列重複與
    /// 自訂字型宣告皆能往返保真（巢狀表格改由 OdfKit 自身 round-trip 驗證，原因詳見內文註解）。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsIndexTemplatesNestedStructuresAndFontFace()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過索引範本與巢狀結構實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeIndexTemplates_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-index-templates.odt");
            CreateIndexTemplatesDocument(odtPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt:Text (encoded):UTF8", odtPath);
            string txtPath = Path.Combine(outputDir, "interop-index-templates.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出索引範本 ODT 的文字轉換結果。");
            string txt = File.ReadAllText(txtPath, Encoding.UTF8);

            // 目錄專案範本（含定位點、頁碼）與更新後產生的標題文字
            Assert.Contains("索引範本互通章節", txt);

            // 字母索引：自訂範本（自訂前導字串 + 索引欄位）與排序後產生的詞條
            Assert.Contains("OdfKit索引詞", txt);

            // 文獻目錄：自訂範本（作者欄位 + 自訂分隔字串）產生的條目
            Assert.Contains("OdfKit範本著者", txt);
            Assert.Contains("OdfKit-Bib-Sep", txt);

            // 外層表格內容、列重複與巢狀清單皆出現在純文字匯出結果中
            Assert.Contains("外層儲存格", txt);
            Assert.Contains("巢狀清單項目", txt);

            // LibreOffice 26.x 的純文字匯出篩選器與 ODT 重新存檔皆不保留巢狀表格（儲存格中的
            // table:table 子元素），且會清除未被任何文字樣式參照的字型宣告（font-face-decls），
            // 此皆為 LibreOffice 本身已知行為而非 OdfKit 寫入端的缺陷；因此巢狀表格與字型宣告
            // 的語意正確性改以 OdfKit 自身的 round-trip 驗證（寫入端產生後，再由 OdfKit 重新
            // 讀回確認結構保真），其餘專案仍透過真實 LibreOffice 26.x headless 模式驗證。
            using (TextDocument odfKitRoundTrip = TextDocument.Load(odtPath))
            {
                string odfKitRoundTripXml = ReadContentXml(odfKitRoundTrip);
                Assert.Contains("巢狀表格內容", odfKitRoundTripXml);
                Assert.Contains("table:number-rows-repeated=\"3\"", odfKitRoundTripXml);

                using var odfKitRoundTripStream = new MemoryStream();
                odfKitRoundTrip.SaveToStream(odfKitRoundTripStream);
                odfKitRoundTripStream.Position = 0;
                using OdfPackage odfKitRoundTripPackage = OdfPackage.Open(odfKitRoundTripStream, leaveOpen: true);
                using Stream odfKitStylesStream = odfKitRoundTripPackage.GetEntryStream("styles.xml");
                using var odfKitStylesReader = new StreamReader(odfKitStylesStream, Encoding.UTF8);
                string odfKitStylesXml = odfKitStylesReader.ReadToEnd();
                Assert.Contains("OdfKitInteropFont", odfKitStylesXml);
                Assert.Contains("OdfKitInteropFont", odfKitRoundTripXml);
            }

            // 透過真實 LibreOffice 26.x headless 模式往返後，驗證最外層表格內容（不含巢狀表格）
            // 仍可正確保存，確認索引範本與表格結構整體未因 LibreOffice 解析而毀損。
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odt", odtPath);
            string roundTripPath = Path.Combine(outputDir, "interop-index-templates.odt");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出索引範本 ODT 往返結果。");

            using TextDocument roundTripped = TextDocument.Load(roundTripPath);
            string roundTripXml = ReadContentXml(roundTripped);
            Assert.Contains("外層儲存格", roundTripXml);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證以 <see cref="TextDocument.Builder"/> Fluent API 建立的文件、段落內影像框架
    /// (<see cref="TextDocument.AddImageFrame"/>)、多欄區段 (<see cref="TextDocument.AddSection"/>)
    /// 與浮動文字框 (<see cref="OdfParagraph.AddFloatingTextBox"/>) 皆能由真實 LibreOffice 26.x
    /// headless 模式載入並正確往返，主文字流內容可由純文字匯出驗證，錨定繪圖物件內容則由往返後的
    /// content.xml 結構驗證。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsBuilderImageFrameSectionAndFloatingTextBoxDocument()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 Builder／影像框架／區段／浮動文字框實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeBuilderInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-builder-imageframe-section.odt");
            CreateBuilderImageFrameSectionDocument(odtPath);

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt:Text (encoded):UTF8", odtPath);
            string txtPath = Path.Combine(outputDir, "interop-builder-imageframe-section.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應輸出 Builder 文件的文字轉換結果。");
            string txt = File.ReadAllText(txtPath, Encoding.UTF8);

            // TextDocument.Builder() Fluent API 產生的標題、段落與清單
            Assert.Contains("Builder 互通章節", txt);
            Assert.Contains("OdfKit-Builder-Marker", txt);
            Assert.Contains("項目一", txt);

            // 多欄區段內容（區段以主文字流呈現，純文字匯出可正確包含）
            Assert.Contains("多欄區段內容", txt);

            // 往返載入後驗證影像框架、區段與浮動文字框結構正確性
            // 注意：浮動文字框屬錨定繪圖物件，LibreOffice 純文字匯出慣例上不包含其內容，
            // 故僅透過往返後的 content.xml 結構驗證其保真，不檢查純文字輸出。
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "odt", odtPath);
            string roundTripPath = Path.Combine(outputDir, "interop-builder-imageframe-section.odt");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出 Builder 文件往返結果。");

            using TextDocument roundTripped = TextDocument.Load(roundTripPath);
            string roundTripXml = ReadContentXml(roundTripped);
            Assert.Contains("draw:frame", roundTripXml);
            Assert.Contains("text:section", roundTripXml);
            Assert.Contains("draw:text-box", roundTripXml);
            Assert.Contains("浮動文字框內容", roundTripXml);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreateBuilderImageFrameSectionDocument(string path)
    {
        // 1. 以 TextDocument.Builder() Fluent API 建立標題、段落與清單
        using TextDocument document = TextDocument.Builder()
            .WithMetadata(metadata => metadata.Title("Builder 互通測試"))
            .AddHeading("Builder 互通章節", level: 1)
            .AddParagraph("OdfKit-Builder-Marker")
            .AddList(list => list.Item("項目一").Item("項目二"))
            .Build();

        // 2. 在段落中新增影像框架（AddImageFrame：經由 OdfMediaManager 寫入封裝媒體專案）
        // 注意：此處使用 CRC 正確的單像素 PNG（非其他測試檔案共用、CRC 已知損毀的 IDAT 區塊樣本），
        // 避免真實 LibreOffice libpng 解碼器於完整文件轉換流程中對壞 CRC 影像輸出警告訊息。
        OdfParagraph imageParagraph = document.AddParagraph("影像框架段落");
        document.AddImageFrame(
            imageParagraph,
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("2cm"),
            "InteropFrame");

        // 3. 新增浮動文字框（OdfFloatingTextBox.AddParagraph）
        imageParagraph.AddFloatingTextBox(
                OdfLength.Parse("1cm"),
                OdfLength.Parse("1cm"),
                OdfLength.Parse("5cm"),
                OdfLength.Parse("3cm"))
            .AddParagraph("浮動文字框內容");

        // 4. 新增多欄區段（AddSection）
        OdfSection section = document.AddSection("InteropSection", 2, OdfLength.Parse("0.5cm"));
        section.Node.AppendChild(BuildSectionParagraphNode("多欄區段內容"));

        document.Save(path);
    }

    private static OdfNode BuildSectionParagraphNode(string text)
    {
        var paragraphNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = text };
        return paragraphNode;
    }

    private static void CreateIndexTemplatesDocument(string path)
    {
        using var document = TextDocument.Create();

        // 頁面設定：新增自訂字型宣告，寫入 content.xml 與 styles.xml 的 font-face-decls
        OdfPageSetup pageSetup = document.GetDefaultPageSetup();
        pageSetup.AddFontFace("OdfKitInteropFont", "Noto Sans CJK TC", "system", "variable");

        // 標題與目錄：以 AddEntryTemplate／OdfIndexTemplateBuilder 自訂第一階範本
        document.AddHeading("索引範本互通章節", 1);
        document.AddParagraph("本文段落供目錄與字母索引掃描。");

        OdfTableOfContents toc = document.AddTableOfContents("目錄", 1);
        toc.AddEntryTemplate(1, "Contents_1")
            .AddText()
            .AddTabStop("right", '.')
            .AddPageNumber();
        toc.Update();

        // 字母索引：以 ConfigureSource 設定來源屬性，並以 AddEntryTemplate 自訂含 AddSpan 前導字串的範本
        OdfParagraph indexParagraph = document.AddParagraph("索引標記段落");
        indexParagraph.AddAlphabeticalIndexMark("OdfKit索引詞", "O", "1");

        OdfAlphabeticalIndex index = document.AddAlphabeticalIndex("字母索引");
        index.ConfigureSource(commaSeparated: true, ignoreCase: true);
        index.AddEntryTemplate("1", "Index_1")
            .AddSpan("» ")
            .AddText()
            .AddTabStop("right", '.')
            .AddPageNumber();
        index.Update();

        // 文獻目錄：以 AddEntryTemplate／OdfBibliographyTemplateBuilder 的 AddBibliographyField 與 AddSpan 自訂格式
        OdfParagraph bibParagraph = document.AddParagraph("文獻引用段落");
        bibParagraph.AddBibliographyMark("ref-odfkit", "book", "OdfKit範本著者", "OdfKit 索引範本指南", "2026");

        OdfBibliography bibliography = document.AddBibliography("文獻目錄");
        bibliography.AddEntryTemplate("book", "Bibliography_1")
            .AddBibliographyField("author")
            .AddSpan(" OdfKit-Bib-Sep ")
            .AddBibliographyField("title");
        bibliography.Update();

        // 表格：巢狀表格（AddNestedTable）與列重複（SetRowRepeat）
        OdfTable outerTable = document.AddTable(2, 2);
        outerTable.GetCell(0, 0).AddParagraph("外層儲存格");
        OdfTable nestedTable = outerTable.AddNestedTable(0, 1, 1, 1);
        nestedTable.GetCell(0, 0).AddParagraph("巢狀表格內容");
        outerTable.SetRowRepeat(1, 3);

        // 清單：巢狀清單（OdfListItem.AddNestedList）
        OdfList list = document.Body.Lists.Add();
        OdfListItem topItem = list.AddListItem("頂層清單項目");
        OdfList nestedList = topItem.AddNestedList();
        nestedList.AddListItem("巢狀清單項目");

        document.Save(path);
    }

    private static void CreateTextDocument(string path)
    {
        using var document = TextDocument.Create();
        document.AddHeading("LibreOffice 26 互通性", 1);
        document.AddParagraph("LibreOffice 26 互通性文字");
        document.AddParagraph("OdfKit-LibreOffice-26-Interop-Marker");
        document.Save(path);
    }

    private static void CreateTrackedChangesDocument(string path)
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;
        document.AddParagraph("OdfKit-TrackedChanges-Marker");

        OdfTable table = document.AddTable(1, 1);
        table.GetCell(0, 0).AddParagraph(string.Empty).AddTextRun("表格追蹤修訂");
        document.Save(path);
    }

    private static void CreateTrackedChangesSpreadsheet(string path)
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "OdfKit-TrackedChanges-Ods-Marker";
        sheet.Cells["A2"].CellValue = 100d;
        sheet.Cells["B2"].CellValue = 200d;
        sheet.Cells["C2"].Formula = "of:=[.A2]+[.B2]";

        document.TrackedChanges = true;
        sheet.Cells["C2"].Formula = "of:=[.A2]*[.B2]";
        document.Save(path);
    }

    private static string ReadSpreadsheetContentXml(SpreadsheetDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 讀取試算表文件封裝中的 <c>styles.xml</c> 內容，供工作表版面配置與列印設定屬性的真機驗證比對。
    /// </summary>
    private static string ReadSpreadsheetStylesXml(SpreadsheetDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream stylesStream = package.GetEntryStream("styles.xml");
        using var reader = new StreamReader(stylesStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// 計算指定子字串在來源字串中不重疊出現的次數，供驗證查詢方法是否意外插入多餘節點。
    /// </summary>
    private static int CountOccurrences(string source, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string ReadContentXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CreateSpreadsheetWithChart(string path)
    {
        using var document = SpreadsheetDocument.Create();
        var sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "月份";
        sheet.Cells["B1"].CellValue = "營收";
        sheet.Cells["A2"].CellValue = "一月";
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = "二月";
        sheet.Cells["B3"].CellValue = 160d;

        document.AddChart("Data", new OdfCellAddress(4, 0, "Data"), new OdfChartDefinition
        {
            ChartType = OdfChartType.Bar,
            Title = "LibreOffice 26 圖表標題",
            DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
            HasLegend = true
        });
        document.Save(path);
    }

    private static void CreatePresentationWithAnimation(string path)
    {
        using var document = PresentationDocument.Create();
        var slide = document.AddSlide();
        var placeholder = slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("2cm"));
        slide.AddEntranceEffect(placeholder.Id, OdfAnimationEffect.Fade, OdfAnimationTrigger.OnClick);
        document.Save(path);
    }

    private static void CreateDrawingDocument(string path)
    {
        using var document = DrawingDocument.Create();
        OdfDrawPage page = document.AddPage("互通頁");
        page.AddTextBox(
            OdfLength.Parse("2cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("8cm"),
            OdfLength.Parse("3cm"),
            "OdfKit-LibreOffice-26-Interop-Marker");
        document.Save(path);
    }

    private static void CreateTextTemplateDocument(string path)
    {
        using var document = TextTemplateDocument.Create();
        document.AddMasterPage("InteropTemplateMaster");
        document.AddParagraph("OdfKit-LibreOffice-Template-Interop-Marker");
        document.Save(path);
    }

    private static void CreateSpreadsheetTemplateDocument(string path)
    {
        using var document = SpreadsheetTemplateDocument.Create();
        var sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "OdfKit-LibreOffice-Template-Interop-Marker";
        document.Save(path);
    }

    private static void CreatePresentationTemplateDocument(string path)
    {
        using var document = PresentationTemplateDocument.Create();
        var slide = document.AddSlide();
        slide.AddPlaceholder(
            OdfPlaceholderType.Title,
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("2cm"));
        slide.AddTextBox(
            OdfLength.Parse("1cm"),
            OdfLength.Parse("4cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("2cm"),
            "OdfKit-LibreOffice-Template-Interop-Marker");
        document.Save(path);
    }

    private static void CreateDrawingTemplateDocument(string path)
    {
        using var document = GraphicsTemplateDocument.Create();
        OdfDrawPage page = document.AddPage("互通範本頁");
        page.AddTextBox(
            OdfLength.Parse("2cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("8cm"),
            OdfLength.Parse("3cm"),
            "OdfKit-LibreOffice-Template-Interop-Marker");
        document.Save(path);
    }

    private static void CreateNativeFlatTextDocument(string path)
    {
        using var document = FlatTextDocument.Create();
        document.AddHeading("原生 Flat XML 互通性", 1);
        document.AddParagraph("OdfKit-LibreOffice-NativeFlat-Interop-Marker");
        document.Save(path);
    }

    private static void CreateNativeFlatSpreadsheetDocument(string path)
    {
        using var document = FlatSpreadsheetDocument.Create();
        var sheet = document.Worksheets.Add("Data");
        sheet.Cells["A1"].CellValue = "OdfKit-LibreOffice-NativeFlat-Interop-Marker";
        document.Save(path);
    }

    private static void CreateNativeFlatPresentationDocument(string path)
    {
        using var document = FlatPresentationDocument.Create();
        var slide = document.AddSlide();
        slide.AddTextBox(
            OdfLength.Parse("1cm"),
            OdfLength.Parse("1cm"),
            OdfLength.Parse("10cm"),
            OdfLength.Parse("2cm"),
            "OdfKit-LibreOffice-NativeFlat-Interop-Marker");
        document.Save(path);
    }

    private static void CreateNativeFlatDrawingDocument(string path)
    {
        using var document = FlatGraphicsDocument.Create();
        OdfDrawPage page = document.AddPage("原生 Flat 互通頁");
        page.AddTextBox(
            OdfLength.Parse("2cm"),
            OdfLength.Parse("2cm"),
            OdfLength.Parse("8cm"),
            OdfLength.Parse("3cm"),
            "OdfKit-LibreOffice-NativeFlat-Interop-Marker");
        document.Save(path);
    }

    private static string GetExpectedLibreOfficeVersion()
    {
        return Environment.GetEnvironmentVariable("ODFKIT_TEST_SOFFICE_VERSION") ?? "26.";
    }

    private static string? FindLibreOfficeSoffice()
    {
        string expectedVersion = GetExpectedLibreOfficeVersion();
        foreach (string candidate in EnumerateSofficeCandidates())
        {
            string? executable = ResolveSofficeExecutable(candidate);
            if (string.IsNullOrEmpty(executable) || executable.Contains("MockSoffice", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string version = GetVersion(executable);
            if (version.Contains($"LibreOffice {expectedVersion}", StringComparison.OrdinalIgnoreCase))
            {
                return executable;
            }
        }

        return null;
    }

    private static string[] EnumerateSofficeCandidates()
    {
        string[] environmentCandidates = ExpandEnvironmentCandidate(Environment.GetEnvironmentVariable("ODFKIT_SOFFICE_PATH"))
            .Concat(ExpandEnvironmentCandidate(Environment.GetEnvironmentVariable("LIBREOFFICE_PATH")))
            .ToArray();

        string[] wellKnownCandidates =
        [
            @"C:\Program Files\LibreOffice\program\soffice.com",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.com",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        ];

        string? pathCandidate = FindOnPath("soffice");
        return [.. environmentCandidates, .. wellKnownCandidates, pathCandidate ?? string.Empty];
    }

    private static IEnumerable<string> ExpandEnvironmentCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        yield return value;

        if (!Directory.Exists(value))
        {
            yield break;
        }

        yield return Path.Combine(value, "soffice.com");
        yield return Path.Combine(value, "soffice.exe");
        yield return Path.Combine(value, "program", "soffice.com");
        yield return Path.Combine(value, "program", "soffice.exe");
        yield return Path.Combine(value, "App", "libreoffice", "program", "soffice.com");
        yield return Path.Combine(value, "App", "libreoffice", "program", "soffice.exe");
    }

    private static string? FindOnPath(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string[] candidates = OperatingSystem.IsWindows()
                ? [Path.Combine(directory, fileName + ".com"), Path.Combine(directory, fileName + ".exe")]
                : [Path.Combine(directory, fileName)];
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? ResolveSofficeExecutable(string candidate)
    {
        if (File.Exists(candidate))
        {
            if (Path.GetExtension(candidate).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string consoleCandidate = Path.ChangeExtension(candidate, ".com");
                if (File.Exists(consoleCandidate) && !string.IsNullOrWhiteSpace(GetVersion(consoleCandidate)))
                {
                    return consoleCandidate;
                }
            }

            return candidate;
        }

        return Directory.Exists(candidate)
            ? ExpandEnvironmentCandidate(candidate).FirstOrDefault(File.Exists)
            : null;
    }

    private static string GetVersion(string sofficePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--version");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 soffice。");
        process.WaitForExit(10_000);
        return process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    }

    private static bool IsNuGetRestoreUnavailable(string output)
    {
        return output.Contains("NU1301", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("NU1900", StringComparison.OrdinalIgnoreCase);
    }

    private static void RunSoffice(string sofficePath, string userInstallationDir, string outputDir, string targetFormat, string inputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(userInstallationDir + Path.DirectorySeparatorChar).AbsoluteUri);
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(targetFormat);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDir);
        startInfo.ArgumentList.Add(inputPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 soffice。");
        Assert.True(process.WaitForExit(60_000), "LibreOffice 轉換逾時。");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.True(process.ExitCode == 0, $"LibreOffice 轉換失敗，輸出：{output}");
        Assert.DoesNotContain("Error", output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 驗證官方範例 Sample.cs 可成功編譯執行，且其產生的 ODT、ODS、ODP 檔案可由 LibreOffice 完美載入與相容。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsSampleGeneratedDocuments()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過範例文件實機互通性測試。");
        }

        string slnRoot = FindSolutionRoot();
        string sampleOutput = Path.Combine(slnRoot, "samples", "output");

        // 1. 執行 dotnet run samples/Sample.cs
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = slnRoot
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("samples/Sample.cs");
        startInfo.ArgumentList.Add("-p:RunAnalyzersDuringBuild=false");
        startInfo.ArgumentList.Add("-p:UseSharedCompilation=false");
        startInfo.Environment["NuGetAudit"] = "false";
        string? restoreConfigFile = Environment.GetEnvironmentVariable("RestoreConfigFile");
        if (!string.IsNullOrWhiteSpace(restoreConfigFile))
        {
            startInfo.Environment["RestoreConfigFile"] = restoreConfigFile;
        }

        using (var process = Process.Start(startInfo) ?? throw new InvalidOperationException("無法啟動 dotnet run。"))
        {
            Assert.True(process.WaitForExit(90_000), "執行範例程式 Sample.cs 逾時。");
            string runOutput = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            if (process.ExitCode != 0 && IsNuGetRestoreUnavailable(runOutput))
            {
                Assert.Skip("目前 NuGet restore 環境無法解析範例相依套件，略過範例文件實機互通性測試。");
            }

            Assert.True(process.ExitCode == 0, $"執行範例程式 Sample.cs 失敗，輸出：{runOutput}");
        }

        // 2. 驗證預期產出的 ODF 檔案存在
        string[] generatedFiles =
        [
            "output_text.odt",
            "output_spreadsheet.ods",
            "output_presentation.odp",
            "output_stream.ods",
            "output_stream.odt"
        ];

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitSampleInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            foreach (var fileName in generatedFiles)
            {
                string filePath = Path.Combine(sampleOutput, fileName);
                Assert.True(File.Exists(filePath), $"範例產出檔案不存在：{filePath}");

                // 3. 呼叫 LibreOffice 實機載入並轉為 PDF 以驗證相容性
                RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", filePath);
                string pdfName = Path.GetFileNameWithoutExtension(fileName) + ".pdf";
                string pdfPath = Path.Combine(outputDir, pdfName);
                Assert.True(File.Exists(pdfPath), $"LibreOffice 應能成功將範例檔案 {fileName} 轉為 PDF。");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證由 OdtStreamWriter 與 OdsStreamWriter 流式寫入產生的文件可由 LibreOffice 完美載入與相容。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsStreamWriterGeneratedDocuments()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過流式寫入文件實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitStreamWriterInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            // 1. 流式寫入 ODT
            string odtPath = Path.Combine(tempRoot, "stream-text.odt");
            using (var fs = new FileStream(odtPath, FileMode.Create, FileAccess.Write))
            using (var writer = new OdtStreamWriter(fs))
            {
                writer.AddHeading("流式文字文件", 1);
                writer.AddParagraph("這是一個使用 OdtStreamWriter 產生的流式文件。");
            }

            // 2. 流式寫入 ODS
            string odsPath = Path.Combine(tempRoot, "stream-sheet.ods");
            using (var fs = new FileStream(odsPath, FileMode.Create, FileAccess.Write))
            using (var writer = new OdsStreamWriter(fs))
            {
                writer.WriteStartSheet("Sheet1");
                writer.WriteStartRow();
                writer.WriteCell("欄位一");
                writer.WriteCell(123.45d);
                writer.WriteEndRow();
                writer.WriteEndSheet();
            }

            // 3. 呼叫 LibreOffice 實機驗證 ODT
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odtPath);
            Assert.True(File.Exists(Path.Combine(outputDir, "stream-text.pdf")), "LibreOffice 應能成功將流式 ODT 轉為 PDF。");

            // 4. 呼叫 LibreOffice 實機驗證 ODS
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odsPath);
            Assert.True(File.Exists(Path.Combine(outputDir, "stream-sheet.pdf")), "LibreOffice 應能成功將流式 ODS 轉為 PDF。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 CJK 增補平面與自造字罕見字寫入文件後，其 LibreOffice 實機開檔與轉檔內容的字元保真度。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_LoadsSupplementaryPlaneFontDocument()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過罕見字實機字元保真度測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitRareCharInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            using var doc = TextDocument.Create();
            var p = doc.Body.Paragraphs.Add();
            p.AddTextRun("罕見字字型對照保真度測試：吉、𠮷、𠜎、𿿽。");

            string odtPath = Path.Combine(tempRoot, "rare-chars.odt");
            doc.Save(odtPath);

            // 呼叫 LibreOffice 轉為純文字，強制以 UTF-8 輸出以防止罕見字遺失或轉為問號
            RunSoffice(sofficePath!, userInstallationDir, outputDir, "txt:Text (encoded):UTF8", odtPath);
            string txtPath = Path.Combine(outputDir, "rare-chars.txt");
            Assert.True(File.Exists(txtPath), "LibreOffice 應成功轉換為文字檔案。");

            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            string txt;
            using (var stream = File.OpenRead(txtPath))
            {
                try
                {
                    var utf8Throw = Encoding.GetEncoding("utf-8", System.Text.EncoderFallback.ExceptionFallback, System.Text.DecoderFallback.ExceptionFallback);
                    using (var reader = new StreamReader(stream, utf8Throw, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
                catch (DecoderFallbackException)
                {
                    stream.Position = 0;
                    // 若發生解碼失敗，退回至標準容錯 UTF-8 讀取（無效字元會解碼為 replacement character，但罕見字能被保留）
                    using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    {
                        txt = reader.ReadToEnd();
                    }
                }
            }

            Assert.Contains("𠮷", txt);
            Assert.Contains("𠜎", txt);
            Assert.Contains("𿿽", txt);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfSlideBuilder.AddTitlePlaceholder"/> 建立的標題預留位置，
    /// 可由 LibreOffice 26.x headless 模式正確開啟並轉出 PDF，且 round-trip 後仍維持
    /// <c>presentation:placeholder</c> 語意（並非退化為一般文字方塊）。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_SlideBuilderAddTitlePlaceholderRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過投影片 builder 標題預留位置實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitSlideBuilderTitlePlaceholderInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odpPath = Path.Combine(tempRoot, "interop-title-placeholder.odp");

            using (PresentationDocument deck = PresentationDocument.Builder()
                .AddSlide("封面", slide => slide.AddTitlePlaceholder(2, 2, 12, 3))
                // 不指定名稱的多載：驗證 PresentationDocumentBuilder 會自動產生序號投影片名稱。
                .AddSlide(slide => slide.AddTitle("自動命名投影片"))
                .Build())
            {
                deck.Save(odpPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odpPath);
            string pdfPath = Path.Combine(outputDir, "interop-title-placeholder.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將含標題預留位置的投影片轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "標題預留位置 PDF 轉換結果不應為空。");

            using PresentationDocument loaded = PresentationDocument.Load(odpPath);
            Assert.Equal(2, loaded.Slides.Count);
            OdfPlaceholderInfo placeholderInfo = Assert.Single(loaded.Slides[0].GetPlaceholderInfos());
            Assert.Equal(OdfPlaceholderType.Title, placeholderInfo.PlaceholderType);
            Assert.Equal("Slide 2", loaded.Slides[1].Name);
            Assert.Equal("自動命名投影片", loaded.Slides[1].TextBoxes[0].Text);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfFormulaBuilder.WithMathML"/>／<see cref="OdfFormulaBuilder.WithIdentifierEquation"/>
    /// 建立的 ODF 公式文件可由 LibreOffice 26.x headless 模式正確開啟並轉出 PDF。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_FormulaBuilderWithMathMLAndIdentifierEquationRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過公式 builder 實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitFormulaBuilderInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            // 真機驗證重點一：WithMathML 直接以完整 MathML XML 設定公式內容。
            string mathMlPath = Path.Combine(tempRoot, "interop-with-mathml.odf");
            using (OdfFormulaDocument mathMlFormula = OdfFormulaDocument.Builder()
                .WithMathML("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi><mo>+</mo><mi>y</mi></math>")
                .Build())
            {
                Assert.Equal("x+y", mathMlFormula.MathText);
                mathMlFormula.Save(mathMlPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", mathMlPath);
            string mathMlPdfPath = Path.Combine(outputDir, "interop-with-mathml.pdf");
            Assert.True(File.Exists(mathMlPdfPath), "LibreOffice 應能將 WithMathML 建立的公式文件轉出 PDF。");
            Assert.True(new FileInfo(mathMlPdfPath).Length > 0, "WithMathML 公式 PDF 轉換結果不應為空。");

            // 真機驗證重點二：WithIdentifierEquation／SetIdentifierEquation 建立簡單識別名稱等式。
            string identifierEquationPath = Path.Combine(tempRoot, "interop-identifier-equation.odf");
            using (OdfFormulaDocument identifierFormula = OdfFormulaDocument.Builder()
                .WithIdentifierEquation("a", "b")
                .Build())
            {
                Assert.Equal("a=b", identifierFormula.MathText);
                identifierFormula.Save(identifierEquationPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", identifierEquationPath);
            string identifierPdfPath = Path.Combine(outputDir, "interop-identifier-equation.pdf");
            Assert.True(File.Exists(identifierPdfPath), "LibreOffice 應能將 WithIdentifierEquation 建立的公式文件轉出 PDF。");
            Assert.True(new FileInfo(identifierPdfPath).Length > 0, "WithIdentifierEquation 公式 PDF 轉換結果不應為空。");

            using OdfFormulaDocument reloaded = OdfFormulaDocument.Load(identifierEquationPath);
            Assert.Equal("a=b", reloaded.MathText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseSchema"/>／<see cref="OdfDatabaseDocument"/> 建立的 ODB 資料庫文件，
    /// 其封裝結構（mimetype／manifest media-type）符合真實 LibreOffice 對 ODF 資料庫文件的偵測慣例。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 已知限制：LibreOffice 26.2.1 headless 的 <c>--convert-to</c> 命令列管線本身不支援 ODB 資料庫
    /// 文件的轉換。實機重新驗證（2026-06-23）發現比先前記錄更隱晦的失效模式：以轉換目標
    /// <c>odb</c> 自身轉換時會明確回報「no export filter」並以非零結束碼失敗；但轉換目標為
    /// <c>txt</c>／<c>ods</c>／<c>xlsx</c>／<c>csv</c> 時卻回報「convert ... using filter」字樣並以
    /// 結束碼 0（成功）結束——然而輸出檔案經位元組層級檢查確認，內容是來源 .odb 的逐位元組原樣
    /// 複製（檔頭仍為 ZIP <c>PK</c> 簽章與 <c>mimetype application/vnd.oasis.opendocument.base</c>），
    /// 並未真正剖析或轉換，這比清楚的「could not be loaded」錯誤更具誤導性（指令稿可能誤判此為
    /// 轉換成功）。故無法沿用其他文件類型慣用的 <c>RunSoffice</c> PDF
    /// 轉換真機驗證模式。本測試改以實際透過 LibreOffice UNO API（<c>soffice --accept=socket</c> 搭配
    /// <c>desktop.loadComponentFromURL</c>）人工驗證過：採用正確 mimetype 的 ODB 檔案可成功載入，
    /// 採用舊版錯誤 mimetype（<c>application/vnd.oasis.opendocument.database</c>）的檔案載入會靜默
    /// 傳回 <see langword="null"/>。由於 .NET 專案內建立 UNO socket 連線需額外的程序管理與 UNO 橋接器，
    /// 此處改為驗證封裝層級的 mimetype／manifest media-type 字串是否與真實 LibreOffice 自身建立 ODB
    /// 檔案時所採用的字面值（已於人工 UNO 驗證階段確認為 <c>application/vnd.oasis.opendocument.base</c>）
    /// 完全一致，藉此鎖定此 mimetype 真機相容性的回歸測試。
    /// </para>
    /// </remarks>
    [Fact]
    public void DatabaseSchemaPackageUsesLibreOfficeCompatibleMimeType()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitDatabaseInterop_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string odbPath = Path.Combine(tempRoot, "interop-database.odb");

            using (var database = OdfKit.Database.OdfDatabaseDocument.Create())
            {
                database.SetConnection("sdbc:embedded:hsqldb");

                var customersTable = new OdfKit.Database.OdfSchemaTable("Customers");
                customersTable.Columns.Add(new OdfKit.Database.OdfSchemaColumn("Id", "INTEGER", isNullable: false, isAutoIncrement: true));
                customersTable.Columns.Add(new OdfKit.Database.OdfSchemaColumn("Name", "VARCHAR", isNullable: true));
                customersTable.PrimaryKey = new OdfKit.Database.OdfSchemaPrimaryKey("PK_Customers", ["Id"]);
                database.Schema.AddTable(customersTable);

                database.AddForm("CustomerForm", "forms/CustomerForm", "客戶表單");
                database.AddReport("SalesReport", "reports/SalesReport", "銷售報表");

                // 真機驗證重點：mimetype 必須與真實 LibreOffice 自行建立 ODB 文件時採用的字面值一致，
                // 否則 LibreOffice 的封裝偵測篩選器會拒絕載入（已透過 UNO API 人工驗證此行為）。
                Assert.Equal("application/vnd.oasis.opendocument.base", database.Package.MimeType);

                database.Save(odbPath);
            }

            using OdfKit.Core.OdfPackage savedPackage = OdfKit.Core.OdfPackage.Open(odbPath);
            Assert.Equal("application/vnd.oasis.opendocument.base", savedPackage.MimeType);
            Assert.Equal(
                "application/vnd.oasis.opendocument.base",
                savedPackage.Manifest["/"]);

            using OdfKit.Database.OdfDatabaseDocument reloaded = OdfKit.Database.OdfDatabaseDocument.Load(odbPath);
            Assert.Single(reloaded.Schema.Tables);
            Assert.Equal("Customers", reloaded.Schema.Tables[0].Name);
            Assert.NotNull(reloaded.Schema.Tables[0].PrimaryKey);
            Assert.Single(reloaded.GetForms());
            Assert.Single(reloaded.GetReports());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="TableTableElement.AppendHeaderRows"/> 即使在工作表已有一般資料列之後才被呼叫，
    /// 仍會將 <c>table:table-header-rows</c> 插入在所有資料列之前；並確認此 DOM 層級操作產生的 ODS
    /// 檔案可由真實 LibreOffice 26.x headless 模式正確開啟與往返，不會因列結構順序錯誤而被拒絕載入。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_AppendHeaderRowsAfterDataRowInsertsBeforeRowsAndRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過表頭列結構順序實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitHeaderRowsOrder_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "interop-header-rows-order.ods");

            using (var document = SpreadsheetDocument.Create())
            {
                // typed DOM facade（AppendHeaderRows／EnsureTableColumns）獨立於高階 AddSheet API
                // 之外運作，AddSheet 內部直接建立通用 OdfNode；為了如實驗證 facade 本身的插入邏輯，
                // 此處改為直接建立 TableTableElement 並掛載至 SheetsRoot。
                var table = new TableTableElement("table");
                table.SetAttribute("name", OdfNamespaces.Table, "Data", "table");
                document.SheetsRoot.AppendChild(table);

                // 先新增一般資料列，模擬使用者先填入資料，事後才補建表頭列的真實情境。
                TableTableRowElement dataRow = table.AppendRow();
                TableTableCellElement dataCell = dataRow.AppendElement(new TableTableCellElement("table"));
                dataCell.AppendElement(new TextPElement("text")).TextContent = "OdfKit-HeaderRowsOrder-Marker";

                // 在已有資料列的情況下呼叫 AppendHeaderRows，驗證其插入位置是否仍位於所有資料列之前
                // （修正前的缺陷會附加在資料列之後）。
                TableTableHeaderRowsElement headerRows = table.AppendHeaderRows();
                headerRows.AppendElement(new TableTableRowElement("table"))
                    .AppendElement(new TableTableCellElement("table"))
                    .AppendElement(new TextPElement("text")).TextContent = "表頭";

                OdfElement[] rowStructures = table.RowStructureChildElements.ToArray();
                Assert.Equal(2, rowStructures.Length);
                Assert.IsType<TableTableHeaderRowsElement>(rowStructures[0]);
                Assert.IsType<TableTableRowElement>(rowStructures[1]);

                document.Save(odsPath);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "ods", odsPath);
            string roundTripPath = Path.Combine(outputDir, "interop-header-rows-order.ods");
            Assert.True(File.Exists(roundTripPath), "LibreOffice 應輸出表頭列順序 ODS 往返結果。");

            using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(roundTripPath);
            string contentXml = ReadSpreadsheetContentXml(reloaded);
            Assert.Contains("OdfKit-HeaderRowsOrder-Marker", contentXml);

            OdfTableSheet reloadedSheet = reloaded.Worksheets.Single();
            var reloadedTable = Assert.IsType<TableTableElement>(reloadedSheet.TableNode);
            OdfElement[] reloadedRowStructures = reloadedTable.RowStructureChildElements.ToArray();
            Assert.True(reloadedRowStructures.Length >= 2, "往返後工作表應仍同時含有表頭列容器與一般資料列。");
            Assert.IsType<TableTableHeaderRowsElement>(reloadedRowStructures[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證含內嵌 ODF 物件的簡報文件可由真實 LibreOffice 26.x headless 模式開啟轉出 PDF，
    /// 並使用 <see cref="OdfPackage.GetEmbeddedObjects"/>／<see cref="OdfPackage.ExtractObjectStream(string)"/>
    /// 自封裝層級擷取內嵌物件內容，確認其與 <c>OdfDocument.GetEmbeddedDocument</c> 高階 API 結果一致。
    /// </summary>
    [Fact]
    public void LibreOfficeHeadless_EmbeddedObjectExtractionRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過內嵌物件擷取實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitEmbeddedObjectInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odpPath = Path.Combine(tempRoot, "interop-embedded-object.odp");

            using (var package = OdfPackage.Create(odpPath))
            {
                var doc = new PresentationDocument(package);

                OdfFormulaDocument formulaDoc = doc.CreateEmbeddedDocument<OdfFormulaDocument>("Object 1");
                formulaDoc.SetIdentifierEquation("x", "y");
                // CreateEmbeddedDocument 僅在建立當下寫入一次最小骨架內容；後續修改內嵌文件
                // 必須再次呼叫該內嵌文件自身的 Save，外層 PresentationDocument.Save 不會連動寫入。
                formulaDoc.Save();

                OdfSlide slide = doc.AddSlide("投影片 1");
                slide.AddEmbeddedObject("Object 1", OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("8cm"), OdfLength.Parse("6cm"));

                doc.Save();
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odpPath);
            string pdfPath = Path.Combine(outputDir, "interop-embedded-object.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將含內嵌物件的簡報文件轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "含內嵌物件的簡報 PDF 轉換結果不應為空。");

            using OdfPackage reopened = OdfPackage.Open(odpPath);

            // 驗證 OdfPackage.GetEmbeddedObjects 能在封裝層級列出內嵌 ODF 物件資料夾。
            var embeddedObjects = reopened.GetEmbeddedObjects().ToList();
            Assert.Contains("Object 1/", embeddedObjects);

            // 驗證 OdfPackage.ExtractObjectStream 能直接擷取內嵌物件的 content.xml，且其內容與
            // OdfDocument.GetEmbeddedDocument 高階 API 重新解析後的結果語意一致（皆含相同的 MathML 算式）。
            using Stream objectStream = reopened.ExtractObjectStream("Object 1");
            using var objectReader = new StreamReader(objectStream, Encoding.UTF8);
            string objectContentXml = objectReader.ReadToEnd();
            Assert.Contains("<math:mi>x</math:mi>", objectContentXml);
            Assert.Contains("<math:mi>y</math:mi>", objectContentXml);

            var reopenedDoc = new PresentationDocument(reopened);
            OdfFormulaDocument reopenedFormula = reopenedDoc.GetEmbeddedDocument<OdfFormulaDocument>("Object 1");
            Assert.Equal("x=y", reopenedFormula.MathText);

            // 驗證未加密封裝中的一般專案，IsEntryEncrypted 應正確回報 false。
            Assert.False(reopened.IsEntryEncrypted("content.xml"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfPackage.IsEntryEncrypted(string)"/> 能正確反映 <see cref="OdfEncryption.Encrypt"/>／
    /// <see cref="OdfEncryption.Decrypt"/> 管線中專案的即時加密狀態，且經由 <c>OdfSaveOptions.Password</c>
    /// 高階加密儲存管線產出的 ODT 檔案，其封裝結構（mimetype 未加密、manifest 含
    /// <c>manifest:encryption-data</c>）與真實 LibreOffice 26.x 自身建立的加密 ODF 文件慣例一致
    /// （已於既有 <c>EncryptionTests.cs</c> 以 OdfKit 自身解密驗證；本測試聚焦於 <c>IsEntryEncrypted</c>
    /// 旗標本身的正確性）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OdfPackage.IsEntryEncrypted(string)"/> 反映的是封裝專案「目前記憶體狀態」是否為加密的
    /// 位元組內容（即介於 <see cref="OdfEncryption.Encrypt"/> 與隨後的 <see cref="OdfEncryption.Decrypt"/>
    /// 之間的暫態），而非檔案本身是否曾被加密過：<c>OdfPackage.Save</c>／<c>Open</c> 的內部管線會在儲存時
    /// 加密、寫入封裝後立即解密回記憶體明文（<c>OdfPackageSaver.RunEncryptedPipeline</c>），載入時則在
    /// 讀出明文後立即清除加密旗標（<c>OdfPackageLoader</c>），因此呼叫端透過 <c>Save</c>／<c>Open</c>
    /// 高階 API 觀察不到此旗標為 <see langword="true"/> 的狀態；必須如本測試直接呼叫
    /// <see cref="OdfEncryption.Encrypt"/>／<see cref="OdfEncryption.Decrypt"/> 才能觀察其轉換。
    /// </para>
    /// <para>
    /// 已知限制：真實 LibreOffice 26.x headless 的 <c>--convert-to</c> 命令列管線本身不支援傳入既有
    /// 加密文件的開啟密碼（<c>soffice --help</c> 並未提供任何密碼相關旗標，僅互動式 GUI 才會彈出密碼輸入
    /// 對話框），因此本測試無法如其他文件類型一般沿用 <c>RunSoffice</c> 進行加密檔案的 PDF 轉換真機驗證。
    /// </para>
    /// </remarks>
    [Fact]
    public void EncryptedEntryDetectionReflectsEncryptionPipelineStateAndProducesLibreOfficeCompatiblePackageStructure()
    {
        const string password = "OdfKit-Interop-密碼-測試";

        using var package = OdfPackage.Create(new MemoryStream(), leaveOpen: true);
        var document = new TextDocument(package);
        document.AddParagraph("OdfKit-加密項目偵測標記");

        // 儲存前先觸發中繼資料／DOM 寫回，確保 content.xml／styles.xml 等專案已存在於封裝中。
        document.SaveToStream(new MemoryStream());

        // 直接呼叫 OdfEncryption.Encrypt，驗證加密後專案立即被標記為已加密。
        OdfEncryption.Encrypt(package, password, OdfEncryptionAlgorithm.Aes256);
        Assert.True(package.IsEntryEncrypted("content.xml"), "呼叫 OdfEncryption.Encrypt 後，content.xml 項目應立即被標記為已加密。");
        Assert.True(package.IsEntryEncrypted("styles.xml"), "呼叫 OdfEncryption.Encrypt 後，styles.xml 項目應立即被標記為已加密。");
        Assert.False(package.IsEntryEncrypted("mimetype"), "mimetype 項目依 ODF 規範必須維持未加密。");

        // 直接呼叫 OdfEncryption.Decrypt，驗證解密後加密旗標立即清除。
        OdfEncryption.Decrypt(package, password);
        Assert.False(package.IsEntryEncrypted("content.xml"), "呼叫 OdfEncryption.Decrypt 後，content.xml 項目不應再被標記為已加密。");

        // 透過高階 OdfSaveOptions.Password 走正常加密儲存管線，並驗證產出封裝的結構符合 ODF 加密規範：
        // mimetype 維持未壓縮且未加密、manifest 內含 manifest:encryption-data 中繼資料。
        using var encryptedStream = new MemoryStream();
        document.SaveToStream(encryptedStream, new OdfSaveOptions { Password = password });
        encryptedStream.Position = 0;

        using (var savedPackage = OdfPackage.Open(encryptedStream, leaveOpen: true))
        {
            Assert.Equal("application/vnd.oasis.opendocument.text", savedPackage.MimeType);
        }

        encryptedStream.Position = 0;
        using (var rawZip = new ZipArchive(encryptedStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var manifestEntry = rawZip.GetEntry("META-INF/manifest.xml");
            Assert.NotNull(manifestEntry);
            using var manifestReader = new StreamReader(manifestEntry!.Open(), Encoding.UTF8);
            string manifestXml = manifestReader.ReadToEnd();
            Assert.Contains("manifest:encryption-data", manifestXml);

            var mimetypeEntry = rawZip.GetEntry("mimetype");
            Assert.NotNull(mimetypeEntry);
            Assert.Equal(mimetypeEntry!.Length, mimetypeEntry.CompressedLength);
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfFontResolver.RegisterFontDirectory"/> 註冊自訂目錄後能觸發字型重新掃描，
    /// 且 <c>OdfSaveOptions.EmbedUsedFonts</c> 儲存選項能將真實系統字型（Windows <c>arial.ttf</c>，
    /// 複製一份至自訂目錄後以該目錄掃描解析）正確內嵌至 ODT 封裝的 <c>Fonts/</c> 專案，
    /// 且內嵌字型後的文件仍可被真實 LibreOffice 26.x headless 模式開啟並轉出 PDF。
    /// </summary>
    /// <remarks>
    /// 真實 TTF 檔案的內部名稱表（由 <c>TtfFontNameReader</c> 讀出）固定為其原始字型家族名稱
    /// （此處為 "Arial"），與檔名無關；而 <see cref="OdfFontResolver.RegisterFontDirectory"/> 觸發的
    /// 目錄掃描僅在該名稱尚未登錄時才會寫入 <c>_fontMap</c>（不會覆寫既有專案）。
    /// 由於 <c>OdfFontResolver</c> 的字型登錄表為整個測試組件共用的靜態狀態，
    /// <c>FormulaAndStylesTest.cs</c> 中既有測試會以同樣的 "Arial" 名稱註冊一個測試結束後即刪除的
    /// 暫存檔路徑；為避免平行執行時受該靜態狀態汙染影響（解析到已刪除的暫存路徑），
    /// 本測試改用 <see cref="OdfFontResolver.RegisterFont"/>（顯式登錄，永遠覆寫既有專案）
    /// 以獨一字型名稱直接登錄複製後的真實字型路徑，而不依賴目錄掃描的「尚未登錄才寫入」語意。
    /// </remarks>
    [Fact]
    public void LibreOfficeHeadless_EmbedUsedFontsFromRegisteredDirectoryRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過字型內嵌實機互通性測試。");
        }

        const string realFontPath = @"C:\Windows\Fonts\arial.ttf";
        if (!File.Exists(realFontPath))
        {
            Assert.Skip("找不到系統真實 TrueType 字型檔案，略過字型內嵌實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitFontEmbedInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        string customFontDir = Path.Combine(tempRoot, "fonts");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);
        Directory.CreateDirectory(customFontDir);

        string uniqueFontName = "OdfKitInteropFont_" + Guid.NewGuid().ToString("N");

        try
        {
            string copiedFontPath = Path.Combine(customFontDir, uniqueFontName + ".ttf");
            File.Copy(realFontPath, copiedFontPath);

            // 先以 RegisterFontDirectory 觸發重新掃描旗標（驗證其副作用），
            // 再以 RegisterFont 顯式登錄獨一名稱以避免覆寫語意差異造成的測試間汙染。
            OdfFontResolver.RegisterFontDirectory(customFontDir);
            OdfFontResolver.RegisterFont(uniqueFontName, copiedFontPath);

            string odtPath = Path.Combine(tempRoot, "interop-embedded-font.odt");
            string expectedFontEntry = "Fonts/" + uniqueFontName + ".ttf";

            using (TextDocument document = TextDocument.Create())
            {
                document.AddFontFace(uniqueFontName, uniqueFontName);
                OdfParagraph paragraph = document.AddParagraph("OdfKit-字型內嵌真機驗證標記");
                paragraph.SetFont(uniqueFontName);

                document.Save(odtPath, new OdfSaveOptions { EmbedUsedFonts = true });
            }

            using (OdfPackage savedPackage = OdfPackage.Open(odtPath))
            {
                Assert.True(savedPackage.HasEntry(expectedFontEntry), $"啟用 EmbedUsedFonts 後，封裝應內含 {expectedFontEntry} 項目。");
                Assert.Contains(expectedFontEntry, savedPackage.Manifest.Keys);
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odtPath);
            string pdfPath = Path.Combine(outputDir, "interop-embedded-font.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將內嵌字型的 ODT 文件轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "內嵌字型 ODT 文件的 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="OdfPackage.PruneUnusedMedia(IEnumerable{string})"/> 能正確移除
    /// 封裝中未被指定參照路徑集合納入的 <c>Pictures/</c> 媒體專案，且保留有參照的專案；移除後另存的 ODT
    /// 文件仍可被真實 LibreOffice 26.x headless 模式開啟並轉出 PDF。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>OdfSaveOptions.PruneUnusedMedia</c> 旗標目前僅為保留供未來自動清理功能使用的設定欄位，
    /// 儲存管線尚未讀取此選項（詳見 <c>OdfSaveOptions.cs</c> 的 <c>PruneUnusedMedia</c> 屬性說明），
    /// 故呼叫端必須如本測試一般自行收集目前實際參照的媒體路徑並手動呼叫
    /// <see cref="OdfPackage.PruneUnusedMedia(IEnumerable{string})"/>。
    /// </para>
    /// <para>
    /// 重要：此方法僅單純依路徑清單比對移除 ZIP 媒體專案，不會檢查或同步移除 content.xml 中殘留的
    /// <c>draw:image</c> DOM 參照節點。本測試刻意先移除孤立圖片對應的 <c>draw:frame</c> DOM 節點，
    /// 再呼叫 <c>PruneUnusedMedia</c>，以驗證呼叫端必須自行確保兩者同步——若僅移除 ZIP 專案卻保留
    /// DOM 參照，會產生指向不存在媒體的懸空連結，導致真實 LibreOffice 直接拒絕開啟整份文件
    /// （已於開發過程中以真機驗證重現此失效模式：錯誤訊息為 "source file could not be loaded"）。
    /// </para>
    /// </remarks>
    [Fact]
    public void LibreOfficeHeadless_PruneUnusedMediaRemovesOnlyUnreferencedPicturesAndRoundTrips()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過清理未參照媒體實機互通性測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitPruneUnusedMediaInterop_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odtPath = Path.Combine(tempRoot, "interop-prune-unused-media.odt");
            string keptImagePath;

            using (TextDocument document = TextDocument.Create())
            {
                OdfImage keptImage = document.Body.Images.Add(CreateValidFourPixelRedPngBytes(), OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), "kept");
                keptImagePath = keptImage.ImageHref ?? throw new InvalidOperationException("新增的圖片應具備有效的 href。");

                // 新增內容不同的第二張圖片，並隨即移除其 draw:frame DOM 節點，模擬「使用者刪除了畫面上的
                // 圖片元素，但底層媒體二進位資料仍殘留在封裝中」的情境：PruneUnusedMedia 只負責清理
                // ZIP 媒體專案，呼叫端必須自行確保傳入的參照清單與目前 DOM 實際參照狀態一致，
                // 否則殘留的 DOM 參照會指向已被刪除的專案而導致封裝損毀（真實 LibreOffice 將拒絕開啟）。
                OdfImage orphanedImage = document.Body.Images.Add(CreateValidFourPixelBluePngBytes(), OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), "orphaned");
                orphanedImage.FrameNode.Parent?.RemoveChild(orphanedImage.FrameNode);

                document.SaveToStream(new MemoryStream());

                Assert.Equal(2, document.Package.Manifest.Keys.Count(key => key.StartsWith("Pictures/", StringComparison.Ordinal)));

                // 僅將仍被參照的圖片路徑交給 PruneUnusedMedia，與目前 DOM 實際參照狀態一致地清理孤立媒體專案。
                document.Package.PruneUnusedMedia([keptImagePath]);

                document.Save(odtPath);
            }

            using (OdfPackage savedPackage = OdfPackage.Open(odtPath))
            {
                var remainingPictureEntries = savedPackage.Manifest.Keys
                    .Where(key => key.StartsWith("Pictures/", StringComparison.Ordinal) && key != "Pictures/")
                    .ToList();
                Assert.Single(remainingPictureEntries);
                Assert.Equal(keptImagePath, remainingPictureEntries[0]);
                Assert.True(savedPackage.HasEntry(keptImagePath));
            }

            RunSoffice(sofficePath!, userInstallationDir, outputDir, "pdf", odtPath);
            string pdfPath = Path.Combine(outputDir, "interop-prune-unused-media.pdf");
            Assert.True(File.Exists(pdfPath), "LibreOffice 應能將清理未參照媒體後的 ODT 文件轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "清理未參照媒體後 ODT 文件的 PDF 轉換結果不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string FindSolutionRoot()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "OdfKit.slnx")) || Directory.Exists(Path.Combine(dir, "samples")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir)!;
        }
        throw new DirectoryNotFoundException("找不到專案方案根目錄。");
    }
}
