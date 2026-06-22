#:project ../OdfKit/OdfKit.csproj
#:project ../OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj
#:project ../OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj
#:project ../OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj
#:project ../OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj
#:project ../OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj
#:project ../OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj

using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Export;
using OdfKit.Csv;
using OdfKit.Conversion;
using OdfKit.Collaboration;
using OdfKit.Extensions.Rdf;

// =====================================================================
// OdfKit .NET 10.0 全功能單檔 Script 示範程式碼
// =====================================================================

Console.WriteLine("==================================================");
Console.WriteLine(" OdfKit .NET 10.0 全功能單檔 Script 示範");
Console.WriteLine("==================================================");

// 建立輸出目錄
string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "samples", "output");
if (!Directory.Exists(outputDir))
{
    Directory.CreateDirectory(outputDir);
}

// 執行各項功能示範
DemoTextDocument(outputDir);
DemoSpreadsheetDocument(outputDir);
DemoPresentationDocument(outputDir);
DemoOdsStreamWriter(outputDir);
DemoMetadataAndSecurity(outputDir);
DemoExtensions(outputDir);
DemoOdtStreamWriter(outputDir);

Console.WriteLine("==================================================");
Console.WriteLine($" 所有示範文件已成功建立，請查看 {outputDir} 目錄！");
Console.WriteLine("==================================================");

/// <summary>
/// 示範建立文字文件 (ODT) 的各項核心功能，包含段落、標題、樣式、清單、表格、圖片、封面、目錄與頁碼。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoTextDocument(string outputDir)
{
    Console.WriteLine(" 1. 正在建立文字文件 (ODT) ⋯⋯");
    
    using var document = TextDocument.Create();
    
    // 設定中介資料
    document.Metadata.Title = "OdfKit 軟體元件全功能示範報告";
    document.Metadata.Creator = "OdfKit 開發小組";
    document.Metadata.Subject = "OdfKit 全功能展示範例";
    document.Metadata.Description = "這是一份使用 OdfKit 高階 API 產生的示範文字文件。";

    // 1. 設定預設頁面樣式 (Standard) 的頁碼與首頁頁尾設定
    var defaultSetup = document.GetDefaultPageSetup();
    // 在一般頁尾加上頁碼欄位
    defaultSetup.Footer.AddPageNumberField();
    // 確保首頁頁尾是獨立的且留空，如此第一頁 (封面) 就不會有頁尾頁碼
    defaultSetup.FooterFirst.Text = "";

    // 2. 建立封面段落
    OdfParagraph titlePara = document.Body.Paragraphs.Add();
    titlePara.HorizontalAlignment = "center";
    titlePara.AddTextRun(" OdfKit 軟體元件 ").WithBold().WithFontSize("24pt").WithColor("#2C3E50");

    OdfParagraph subtitlePara = document.Body.Paragraphs.Add();
    subtitlePara.HorizontalAlignment = "center";
    subtitlePara.AddTextRun(" 支援 .NET 10.0 雙平台編譯與 CNS15251 國家標準驗證 ").WithItalic().WithFontSize("12pt").WithColor("#7F8C8D");

    OdfParagraph coverNotePara = document.Body.Paragraphs.Add();
    coverNotePara.HorizontalAlignment = "center";
    coverNotePara.AddTextRun(" (第一頁：封面，不顯示頁尾頁碼) ").WithFontSize("10pt").WithColor("#95A5A6");

    // 3. 強迫分頁，進入第二頁 (目錄頁)
    OdfParagraph tocHeading = document.Body.Paragraphs.Add();
    tocHeading.BreakPageBefore(); // 強迫分頁
    
    // 插入目錄 (TOC)
    document.AddTableOfContents("文件目錄 (TOC)", 2);

    // 設定其它自動更新選項：外部連結更新模式（2: 載入時確認/詢問）
    document.LinkUpdateMode = 2;


    // 4. 強迫分頁，進入第三頁 (內容本文 - 直向)
    OdfParagraph bodyPara = document.Body.Paragraphs.Add();
    bodyPara.BreakPageBefore(); // 強迫分頁
    
    OdfHeading h1 = document.Body.Headings.Add(" 第一章：基本排版功能示範 ", 1);
    h1.StyleName = "Heading_20_1";
    
    OdfParagraph p1 = document.Body.Paragraphs.Add("本文件展示了 OdfKit 核心程式庫的文字排版功能。您可以在段落中加入不同的文字區段，並套用多種字型樣式，例如：");
    
    p1.AddTextRun(" 這是粗體字色為藍色的文字。 ").WithBold().WithColor("#3498DB");
    p1.AddTextRun(" 這是斜體字色為紅色的文字。 ").WithItalic().WithColor("#E74C3C");

    // 示範 CJK 增補平面與自造字字型分段對照
    OdfParagraph pCjk = document.Body.Paragraphs.Add();
    pCjk.AddTextRun(" 罕見字字型對照展示：吉 (常規字) 、 𠮷 (CJK Ext-B) 、 𠜎 (CJK Ext-B) 與 PUA 區域自造字 𿿽 。 ").WithBold();

    // 新增有序清單
    OdfList list = document.Body.Lists.Add();
    list.AddListItem("第一項：輕鬆建立豐富格式的 ODF 文件。");
    list.AddListItem("第二項：原生支援雙目標 Framework 編譯。");
    list.AddListItem("第三項：完美的效能與低記憶體設計。");

    // 5. 新增具名頁面樣式 (橫向 - Landscape)
    document.AddPageStyle("Landscape", setup =>
    {
        setup.PageWidth = 29.7;
        setup.PageHeight = 21.0;
        setup.HeaderText = " OdfKit 橫向頁首展示 ";
        setup.Footer.AddPageNumberField(); // 橫向頁尾也加入頁碼
    });

    // 6. 強迫分頁並切換成 Landscape 橫向頁面 (第四頁)
    OdfParagraph landscapeHeading = document.Body.Paragraphs.Add();
    landscapeHeading.BreakPageBefore("Landscape"); // 切換成 Landscape
    
    OdfHeading h2 = document.Body.Headings.Add(" 第二章：橫向頁面展示與表格設計 ", 1);
    h2.StyleName = "Heading_20_1";

    // 在橫向頁面插入一個 3x2 表格
    OdfTable table = document.Body.Tables.Add(3, 2);
    // 設定表頭
    table.GetCell(0, 0).AddParagraph(" 平台名稱 ");
    table.GetCell(0, 1).AddParagraph(" 支援度與相容性說明 ");
    
    // 填入表格資料
    table.GetCell(1, 0).AddParagraph(" .NET 10.0 ");
    table.GetCell(1, 1).AddParagraph(" 完整支援 C# 14 語法與單檔 Script 執行 ");
    table.GetCell(2, 0).AddParagraph(" .NET Standard 2.0 ");
    table.GetCell(2, 1).AddParagraph(" 支援舊版與跨平台 Framework 相容性 ");

    // 7. 強迫分頁並切換回直向頁面 (第五頁)
    OdfParagraph backPara = document.Body.Paragraphs.Add();
    backPara.BreakPageBefore("Standard"); // 切換回直向
    
    OdfHeading h3 = document.Body.Headings.Add(" 第三章：郵件合併與 Foreach 迴圈展開 ", 1);
    h3.StyleName = "Heading_20_1";

    // 使用 OdfMailMergeEngine 來展示郵件合併與迴圈展開
    OdfParagraph mergeIntro = document.Body.Paragraphs.Add(" 以下展示了郵件合併引擎 (Mail Merge) 自動展開巢狀迴圈： ");
    
    document.Body.Paragraphs.Add("{{TableStart:Users}}");
    document.Body.Paragraphs.Add("   使用者姓名： {{Name}} ，所屬群組： {{Group}} ");
    document.Body.Paragraphs.Add("{{TableEnd:Users}}");

    var mergeData = new Dictionary<string, object?>
    {
        ["Users"] = new[]
        {
            new Dictionary<string, object?> { ["Name"] = "張小明", ["Group"] = "系統管理員" },
            new Dictionary<string, object?> { ["Name"] = "李小華", ["Group"] = "一般使用者" },
            new Dictionary<string, object?> { ["Name"] = "王大同", ["Group"] = "訪客群組" }
        }
    };
    
    // 執行郵件合併
    document.MailMerge(mergeData);

    // 插入圖片 (此處使用隨機產生的 1 像素 PNG 資料)
    byte[] imageBytes = CreatePngBytes();
    document.Body.Images.Add(imageBytes, "3cm", "3cm", "OdfKitLogo");

    string outputPath = Path.Combine(outputDir, "output_text.odt");
    document.Save(outputPath);
    Console.WriteLine($"   已儲存文字文件至： {outputPath} ");
}

/// <summary>
/// 示範建立試算表文件 (ODS) 的各項核心功能，包含工作表、儲存格資料、公式運算、儲存格樣式、列印設定與自動欄寬行高。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoSpreadsheetDocument(string outputDir)
{
    Console.WriteLine(" 2. 正在建立試算表 (ODS) ⋯⋯");

    using var workbook = SpreadsheetDocument.Create();
    
    // 取得或建立工作表
    OdfTableSheet sheet = workbook.Worksheets.Add("銷售數據");

    // 1. 填入儲存格資料
    sheet.Cells["A1"].CellValue = "月份";
    sheet.Cells["B1"].CellValue = "銷售額 (千元)";
    sheet.Cells["A1"].StyleName = "HeadingCell";
    sheet.Cells["B1"].StyleName = "HeadingCell";

    sheet.Cells["A2"].CellValue = "一月";
    sheet.Cells["B2"].CellValue = 150d;

    sheet.Cells["A3"].CellValue = "二月";
    sheet.Cells["B3"].CellValue = 220d;

    sheet.Cells["A4"].CellValue = "三月";
    sheet.Cells["B4"].CellValue = 310d;

    // 2. 設定公式：計算銷售額的總和 (SUM)
    sheet.Cells["A5"].CellValue = "總計";
    sheet.Cells["B5"].Formula = "of:=SUM([.B2:.B4])";

    // 3. 設定列印範圍與列印標題列
    // 設定列印範圍為 A1:B5
    sheet.SetPrintArea(new OdfCellRange(0, 0, 4, 1));
    // 設定第一列為列印重複標題列
    sheet.SetPrintTitleRows(0, 0);

    // 4. 示範 System.Drawing.Color 與 OdfColor 之間的對接
    System.Drawing.Color systemColor = System.Drawing.Color.Goldenrod;
    OdfColor odfColor = OdfColor.FromRgb(systemColor.R, systemColor.G, systemColor.B);
    Console.WriteLine($"   已成功透過 System.Drawing.Color 轉成 OdfColor 建立色彩： {odfColor} ");

    // 5. 設定列最佳高度與欄自動欄寬
    // 將第 1 到 5 列設為最佳高度
    for (int r = 0; r < 5; r++)
    {
        sheet.Rows[r].OptimalHeight = true;
    }

    // 自動調整 A 欄與 B 欄的欄寬
    sheet.Columns[1].AutoFit();

    // 7. 新增嵌入圖表 (Chart) 展示
    var chartDef = new OdfChartDefinition
    {
        ChartType = OdfChartType.Bar,
        Title = "每月銷售額柱狀圖",
        DataRange = new OdfCellRange(0, 0, 4, 1, "銷售數據"), // A1:B5
        HasLegend = true
    };
    workbook.AddChart("銷售數據", new OdfCellAddress(0, 3, "銷售數據"), chartDef);

    // 8. 新增資料篩選器 (AutoFilter) 展示
    // 針對 A1:B5 的範圍，新增名為「銷售自動篩選」的篩選器，篩選條件為第 2 欄（索引 1）大於等於 200
    sheet.Ranges["A1:B5"].AddFilter("銷售自動篩選", (1, ">=", "200"));

    // 9. 設定其它自動更新選項：公式自動計算與外部連結自動更新（1: 自動）
    workbook.AutoCalculate = true;
    workbook.LinkUpdateMode = 1;



    // 6. 設定第二個工作表示範
    OdfTableSheet metaSheet = workbook.Worksheets.Add("說明頁面");
    metaSheet.Cells["A1"].CellValue = "此試算表是由 OdfKit 自動產生。";
    metaSheet.Columns[0].AutoFit();

    string outputPath = Path.Combine(outputDir, "output_spreadsheet.ods");
    workbook.Save(outputPath);
    Console.WriteLine($"   已儲存試算表至： {outputPath} ");
}

/// <summary>
/// 示範採用 OdfKit 專屬的 Fluent Builder 模式建立簡報文件 (ODP)，包含投影片、幾何形狀、轉場特效與 CNS15251 驗證。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoPresentationDocument(string outputDir)
{
    Console.WriteLine(" 3. 正在建立簡報 (ODP) ⋯⋯");

    // 使用 Fluent Builder 建立簡報
    using PresentationDocument deck = PresentationDocument.Builder()
        .WithMetadata(metadata => metadata
            .Title("OdfKit 產品簡報")
            .Author("OdfKit 開發小組")
            .Subject("CNS15251 國家標準驗證簡報"))
        .AddSlide("開場投影片", slide => slide
            .AddTitle(" 歡迎使用 OdfKit ")
            .WithSpeakerNotes(" 向聽眾打招呼，並引導至主題 ")
            .WithTransition(OdfTransitionType.Fade))
        .Build();

    // 取得投影片並新增幾何形狀與圖片
    OdfSlide extraSlide = deck.Slides.Add("技術特點");
    
    // 新增文字框
    extraSlide.AddTextBox(
        OdfLength.FromCentimeters(1),
        OdfLength.FromCentimeters(1),
        OdfLength.FromCentimeters(12),
        OdfLength.FromCentimeters(2),
        " OdfKit 主要核心技術特點： ");

    // 新增矩形幾何形狀並設定背景填色、邊框與時序動畫
    OdfShape shape = extraSlide.AddShape(
        OdfShapeType.Rectangle,
        OdfLength.FromCentimeters(1),
        OdfLength.FromCentimeters(4),
        OdfLength.FromCentimeters(5),
        OdfLength.FromCentimeters(3));
    // 示範 System.Drawing.Color 與 OdfColor 之間的對接
    System.Drawing.Color shapeColor = System.Drawing.Color.Goldenrod;
    OdfColor odfShapeColor = OdfColor.FromRgb(shapeColor.R, shapeColor.G, shapeColor.B);
    shape.FillColor = odfShapeColor.ToString(); // 設定填滿顏色 (使用 OdfColor.ToString())
    shape.StrokeColor = new OdfColor("#E67E22").ToString(); // 橘色邊框
    shape.StrokeWidth = "2.5pt";
    shape.StrokeStyle = "solid";
    
    // 設定淡入動畫：持續 1 秒，延遲 0.5 秒
    shape.Animate(OdfAnimationType.FadeIn, OdfLength.FromPoints(72), OdfLength.FromPoints(36));

    // 插入圖片到投影片
    extraSlide.AddPicture(
        CreatePngBytes(),
        OdfLength.FromCentimeters(7),
        OdfLength.FromCentimeters(4),
        OdfLength.FromCentimeters(4),
        OdfLength.FromCentimeters(3));

    // 設定轉場特效與講者備忘錄
    extraSlide.SpeakerNotes = " 介紹 ODP 與多種幾何形狀繪製功能 ";
    extraSlide.SetTransition(OdfTransitionType.Zoom, OdfLength.FromPoints(36));

    string outputPath = Path.Combine(outputDir, "output_presentation.odp");
    deck.Save(outputPath);
    Console.WriteLine($"   已儲存簡報至： {outputPath} ");

    // 進行 ODF 規格合規性驗證 (CNS15251)
    try
    {
        var profile = OdfKit.Compliance.OdfComplianceProfiles.RocTaiwanOdfCns15251;
        var report = OdfKit.Compliance.OdfValidator.Validate(outputPath, profile: profile);
        Console.WriteLine($"   [驗證結果 - ROC CNS15251] 是否合規： {report.IsValid} ");
        if (!report.IsValid)
        {
            foreach (var error in report.Issues)
            {
                Console.WriteLine($"     - [{error.Severity}] {error.Message} (位置: {error.PackagePath})");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   [驗證失敗] 呼叫驗證器時發生錯誤： {ex.Message} ");
    }
}

/// <summary>
/// 示範高效能低記憶體的 OdsStreamWriter API，適用於產生大數據報表以避免 OOM 崩潰。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoOdsStreamWriter(string outputDir)
{
    Console.WriteLine(" 4. 正在執行低記憶體高流速寫入 (OdsStreamWriter) ⋯⋯");

    string outputPath = Path.Combine(outputDir, "output_stream.ods");
    FileStream fileStream;
    try
    {
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }
    catch (IOException)
    {
        Console.WriteLine($"   [警告] 無法寫入 {outputPath} ，該檔案可能正被 LibreOffice 開啟佔用。");
        Console.WriteLine("   已自動切換至備用輸出路徑： output_stream_backup.ods ");
        outputPath = Path.Combine(outputDir, "output_stream_backup.ods");
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }

    // 初始化串流寫入器，此模式記憶體佔用小於 1 MB
    using (fileStream)
    using (var writer = new OdsStreamWriter(fileStream))
    {
        writer.WriteStartSheet("大量銷售明細表");

        // 宣告欄位定義以符合 ODF XML 規格，並設定自訂欄寬
        writer.WriteColumn(OdfLength.FromCentimeters(3.0)); // 交易編號
        writer.WriteColumn(OdfLength.FromCentimeters(4.5)); // 交易日期
        writer.WriteColumn(OdfLength.FromCentimeters(3.0)); // 產品金額
        writer.WriteColumn(OdfLength.FromCentimeters(2.5)); // 交易狀態

        // 寫入表頭列
        writer.WriteStartRow();
        writer.WriteCell("交易編號");
        writer.WriteCell("交易日期");
        writer.WriteCell("產品金額");
        writer.WriteCell("交易狀態");
        writer.WriteEndRow();

        // 模擬寫入大量測試數據列
        for (int i = 1; i <= 100; i++)
        {
            writer.WriteStartRow(useOptimalHeight: true); // 展示最佳高度
            writer.WriteCell($"TXN-{i:D5}");
            writer.WriteCell(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc).AddHours(i));
            writer.WriteCell(99.9d * i);
            writer.WriteCell(true);
            writer.WriteEndRow();
        }

        writer.WriteEndSheet();
    }

    Console.WriteLine($"   已透過串流成功寫入大量資料至： {outputPath} ");

    // 進行 ODF 規格合規性驗證
    try
    {
        var report = OdfKit.Compliance.OdfValidator.Validate(outputPath);
        Console.WriteLine($"   [驗證結果] 是否合規： {report.IsValid} ");
        if (!report.IsValid)
        {
            foreach (var error in report.Issues)
            {
                Console.WriteLine($"     - [{error.Severity}] {error.Message} (位置: {error.PackagePath})");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   [驗證失敗] 呼叫驗證器時發生錯誤： {ex.Message} ");
    }

    // 以低記憶體串流方式讀回剛寫入的資料，驗證 OdsStreamReader 與 OdsStreamWriter 的往返正確性
    Console.WriteLine("   正在以 OdsStreamReader 串流讀回剛寫入的資料 ⋯⋯");
    using (var readStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
    using (var reader = new OdsStreamReader(readStream))
    {
        Console.WriteLine($"     工作表名稱： {string.Join(", ", reader.SheetNames)} ");
        int rowCount = 0;
        while (reader.Read())
        {
            rowCount++;
            if (rowCount <= 2)
            {
                var cells = new object?[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    cells[i] = reader.GetValue(i);
                }
                Console.WriteLine($"     第 {rowCount} 列： {string.Join(" | ", cells)} ");
            }
        }
        Console.WriteLine($"     共讀回 {rowCount} 列（含表頭）。");
    }
}

/// <summary>
/// 示範如何讀寫文件 Metadata 中介資料。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoMetadataAndSecurity(string outputDir)
{
    Console.WriteLine(" 5. 正在示範中介資料 (Metadata) 讀寫設定 ⋯⋯");

    string testOdtPath = Path.Combine(outputDir, "output_text.odt");
    if (!File.Exists(testOdtPath))
    {
        return;
    }

    // 載入文件並讀取中介資料
    using (var loadedDoc = TextDocument.Load(testOdtPath))
    {
        Console.WriteLine($"   [讀取成功] 文件標題： {loadedDoc.Metadata.Title} ");
        Console.WriteLine($"   [讀取成功] 建立者： {loadedDoc.Metadata.Creator} ");
        Console.WriteLine($"   [讀取成功] 主旨： {loadedDoc.Metadata.Subject} ");
        
        // 變更中介資料並儲存新版
        loadedDoc.Metadata.Title = "OdfKit 示範報告 (更新版)";
        loadedDoc.Metadata.Creator = "Antigravity Agent";
        loadedDoc.Metadata.Subject = "OdfKit 郵件合併與複雜功能示範";
        
        string updatedPath = Path.Combine(outputDir, "output_text_updated.odt");
        loadedDoc.Save(updatedPath);
        Console.WriteLine($"   已更新中介資料並儲存至： {updatedPath} ");
    }
}

static void DemoExtensions(string outputDir)
{
    Console.WriteLine(" 6. 正在執行 PDF、HTML、CSV、OOXML、協同編輯、RDF 與影像渲染等轉換匯出展示 ⋯⋯");

    // 建立臨時文字文件做為轉換來源
    using var tempDoc = TextDocument.Create();
    tempDoc.Body.Headings.Add(" OdfKit 跨平台格式轉換展示 ", 1);
    tempDoc.Body.Paragraphs.Add("本文件將會被分別轉譯為 PDF 以及 HTML 格式，展示 OdfKit 的擴充套件強大渲染實力。");
    
    OdfList list = tempDoc.Body.Lists.Add();
    list.AddListItem("支援 PDF 格式完美重現。");
    list.AddListItem("支援 HTML 網頁流暢排版。");

    // 匯出成 PDF (使用 OdfKit.Extensions.Pdf 提供的 OdfPdfExporter)
    string pdfPath = Path.Combine(outputDir, "output_pdf.pdf");
    using (var pdfStream = new FileStream(pdfPath, FileMode.Create, FileAccess.Write))
    {
        OdfPdfExporter.Export(tempDoc, pdfStream);
    }
    Console.WriteLine($"   已成功將 ODT 匯出至 PDF 檔案： {pdfPath} ");

    // 匯出成 HTML (使用 OdfKit.Extensions.Html 提供的 OdfHtmlExporter)
    string htmlPath = Path.Combine(outputDir, "output_html.html");
    string htmlContent = OdfHtmlExporter.Export(tempDoc);
    File.WriteAllText(htmlPath, htmlContent, Encoding.UTF8);
    Console.WriteLine($"   已成功將 ODT 匯出至 HTML 網頁： {htmlPath} ");

    // 1. 建立臨時試算表以供轉換與影像渲染示範
    using var tempWorkbook = SpreadsheetDocument.Create();
    var tempSheet = tempWorkbook.Worksheets.Add("Sheet1");
    tempSheet.Cells["A1"].CellValue = "姓名";
    tempSheet.Cells["B1"].CellValue = "成績";
    tempSheet.Cells["A2"].CellValue = "張小明";
    tempSheet.Cells["B2"].CellValue = 95d;
    tempSheet.Cells["A3"].CellValue = "李小華";
    tempSheet.Cells["B3"].CellValue = 88d;

    // 2. 匯出成 CSV (使用核心庫 OdfCsvExporter)
    string csvPath = Path.Combine(outputDir, "output_csv.csv");
    OdfCsvExporter.ExportToFile(tempWorkbook, csvPath);
    Console.WriteLine($"   已成功將 ODS 匯出至 CSV 檔案： {csvPath} ");

    // 3. ODF 轉 OOXML 雙向互轉展示 (Ooxml.Extensions)
    string docxPath = Path.Combine(outputDir, "output_docx.docx");
    using (var docxStream = new FileStream(docxPath, FileMode.Create, FileAccess.ReadWrite))
    {
        OdfToDocxConverter.Convert(tempDoc, docxStream);
    }
    Console.WriteLine($"   已成功將 ODT 轉換為 Word DOCX： {docxPath} ");

    string xlsxPath = Path.Combine(outputDir, "output_xlsx.xlsx");
    using (var xlsxStream = new FileStream(xlsxPath, FileMode.Create, FileAccess.ReadWrite))
    {
        OdfToXlsxConverter.Convert(tempWorkbook, xlsxStream);
    }
    Console.WriteLine($"   已成功將 ODS 轉換為 Excel XLSX： {xlsxPath} ");

    // 4. 協同編輯 (Collaboration) JSON 操作軌跡匯出與匯入
    string opsJson = OdtOperationsExporter.ExportToJson(tempDoc);
    Console.WriteLine($"   已成功將 ODT 本文內容匯出為協同編輯操作 JSON。長度： {opsJson.Length} 字元。");
    using var importedDoc = OdtOperationsImporter.Merge(opsJson);
    string importedOdtPath = Path.Combine(outputDir, "output_collaboration_imported.odt");
    importedDoc.Save(importedOdtPath);
    Console.WriteLine($"   已成功將操作 JSON 重新讀入並儲存為 ODT 檔案： {importedOdtPath} ");

    // 5. RDF 語意中介資料注入與 SPARQL 查詢
    string docUri = "http://example.org/documents/demo";
    tempDoc.Package.RdfMetadata.AddTriple(docUri, "http://purl.org/dc/terms/title", "OdfKit RDF Demo Document", isLiteral: true);
    tempDoc.Package.RdfMetadata.AddTriple(docUri, "http://purl.org/dc/terms/creator", "http://example.org/creator/antigravity", isLiteral: false);

    string sparqlQuery = @"
        SELECT ?p ?o
        WHERE {
            <http://example.org/documents/demo> ?p ?o .
        }";
    var resultSet = tempDoc.Package.RdfMetadata.SelectSparql(sparqlQuery);
    Console.WriteLine($"   [RDF SPARQL 查詢結果] 共有 {resultSet.Count} 筆符合條件：");
    foreach (var resultRow in resultSet.Results)
    {
        Console.WriteLine($"     - Predicate: {resultRow["p"]} | Object: {resultRow["o"]}");
    }

    // 6. Skia 影像渲染匯出為 PNG
    string pngPath = Path.Combine(outputDir, "output_sheet_rendering.png");
    using (var pngStream = new FileStream(pngPath, FileMode.Create, FileAccess.Write))
    {
        var imgOptions = new OdfImageExportOptions
        {
            ColumnCount = 2,
            RowCount = 3,
            CellWidthPx = 100,
            CellHeightPx = 30
        };
        OdfImageExporter.ExportToPng(tempSheet, pngStream, imgOptions);
    }
    Console.WriteLine($"   已成功將 ODS 工作表格線渲染匯出為 PNG 圖片： {pngPath} ");
}

/// <summary>
/// 輔助方法：產生一個最基本的 1 像素 PNG 圖片 Byte 陣列，以供範例測試插入圖片。
/// </summary>
/// <returns> PNG 圖片二進位資料 </returns>
static byte[] CreatePngBytes()
{
    return Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
}

/// <summary>
/// 示範高效能低記憶體的 OdtStreamWriter API，適用於以資料流寫入大型文字文件。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoOdtStreamWriter(string outputDir)
{
    Console.WriteLine(" 7. 正在執行低記憶體高流速寫入 (OdtStreamWriter) ⋯⋯");

    string outputPath = Path.Combine(outputDir, "output_stream.odt");
    FileStream fileStream;
    try
    {
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }
    catch (IOException)
    {
        Console.WriteLine($"   [警告] 無法寫入 {outputPath} ，該檔案可能正被 LibreOffice 開啟佔用。");
        Console.WriteLine("   已自動切換至備用輸出路徑： output_stream_backup.odt ");
        outputPath = Path.Combine(outputDir, "output_stream_backup.odt");
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }

    // 初始化串流寫入器，此模式記憶體佔用小於 1 MB
    using (fileStream)
    using (var writer = new OdtStreamWriter(fileStream))
    {
        writer.AddHeading(" OdtStreamWriter 流式寫入示範報告 ", 1);
        writer.AddParagraph("本文件展示了使用 OdtStreamWriter 進行高效能、低記憶體耗用的文字文件串流寫入。");

        writer.AddHeading(" 章節一：清單寫入 ", 2);
        writer.BeginList();
        writer.AddListItem("這是串流寫入的清單項目一。");
        writer.AddListItem("這是串流寫入的清單項目二。");
        writer.EndList();

        writer.AddPageBreak();

        writer.AddHeading(" 章節二：分頁後內容 ", 2);
        writer.AddParagraph("這是在強制分頁之後的段落文字。");
    }

    Console.WriteLine($"   已透過串流成功寫入大量文字至： {outputPath} ");

    // 進行 ODF 規格合規性驗證
    try
    {
        var report = OdfKit.Compliance.OdfValidator.Validate(outputPath);
        Console.WriteLine($"   [驗證結果] 是否合規： {report.IsValid} ");
        if (!report.IsValid)
        {
            foreach (var error in report.Issues)
            {
                Console.WriteLine($"     - [{error.Severity}] {error.Message} (位置: {error.PackagePath})");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   [驗證失敗] 呼叫驗證器時發生錯誤： {ex.Message} ");
    }

    // 以低記憶體串流方式讀回剛寫入的內容，驗證 OdtStreamReader 與 OdtStreamWriter 的往返正確性
    Console.WriteLine("   正在以 OdtStreamReader 串流讀回剛寫入的內容 ⋯⋯");
    using (var readStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read))
    using (var reader = new OdtStreamReader(readStream))
    {
        while (reader.Read())
        {
            string preview = reader.Text.Length > 30 ? reader.Text.Substring(0, 30) + "…" : reader.Text;
            Console.WriteLine($"     {reader.NodeType,-9}： {preview} ");
        }
    }
}
