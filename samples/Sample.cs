#:project ../OdfKit/OdfKit.csproj
#:project ../OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj
#:project ../OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.Text;
using OdfKit.Spreadsheet;
using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Export;

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
/// 示範建立文字文件 (ODT) 的各項核心功能，包含段落、標題、樣式、清單、表格與圖片。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoTextDocument(string outputDir)
{
    Console.WriteLine(" 1. 正在建立文字文件 (ODT) ...");
    
    using var document = TextDocument.Create();
    
    // 設定中介資料
    document.Metadata.Title = "OdfKit 示範報告";
    document.Metadata.Creator = "OdfKit 小組";
    document.Metadata.Description = "這是一份使用 OdfKit 高階 API 產生的示範文字文件。";

    // 新增標題 1
    OdfHeading h1 = document.Body.Headings.Add("OdfKit 文字處理功能示範", 1);
    h1.StyleName = "Heading_20_1";

    // 新增一般段落與文字樣式
    OdfParagraph p1 = document.Body.Paragraphs.Add("本文件展示了 OdfKit 核心程式庫的文字排版功能。您可以在段落中加入不同的文字區段，並套用多種字型樣式，例如：");
    
    OdfTextRun boldText = p1.AddTextRun(" 這是粗體文字 ");
    boldText.IsBold = true;
    
    OdfTextRun italicText = p1.AddTextRun(" 這是斜體文字 ");
    italicText.IsItalic = true;

    // 新增標題 2
    OdfHeading h2 = document.Body.Headings.Add("清單與表格展示", 2);
    h2.StyleName = "Heading_20_2";

    // 新增有序清單
    OdfList list = document.Body.Lists.Add();
    list.AddListItem("第一項：輕鬆建立豐富格式的 ODF 文件。");
    list.AddListItem("第二項：原生支援雙目標 Framework 編譯。");
    list.AddListItem("第三項：完美的效能與低記憶體設計。");

    // 新增一個 3x2 的表格
    OdfTable table = document.Body.Tables.Add(3, 2);
    
    // 設定表頭
    table.GetCell(0, 0).AddParagraph("功能模組");
    table.GetCell(0, 1).AddParagraph("支援狀態");
    
    // 填入表格資料
    table.GetCell(1, 0).AddParagraph("ODT 讀寫");
    table.GetCell(1, 1).AddParagraph("完整支援");
    table.GetCell(2, 0).AddParagraph("ODS 讀寫");
    table.GetCell(2, 1).AddParagraph("完整支援");

    // 插入圖片 (此處使用隨機產生的 1 像素 PNG 資料)
    byte[] imageBytes = CreatePngBytes();
    document.Body.Images.Add(imageBytes, "3cm", "3cm", "OdfKitLogo");

    string outputPath = Path.Combine(outputDir, "output_text.odt");
    document.Save(outputPath);
    Console.WriteLine($"   已儲存文字文件至：{outputPath}");
}

/// <summary>
/// 示範建立試算表文件 (ODS) 的各項核心功能，包含工作表、儲存格資料、公式運算與儲存格樣式。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoSpreadsheetDocument(string outputDir)
{
    Console.WriteLine(" 2. 正在建立試算表 (ODS) ...");

    using var workbook = SpreadsheetDocument.Create();
    
    // 取得或建立工作表
    OdfTableSheet sheet = workbook.Worksheets.Add("銷售數據");

    // 填入儲存格資料
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

    // 設定公式：計算銷售額的總和 (SUM)
    sheet.Cells["A5"].CellValue = "總計";
    sheet.Cells["B5"].Formula = "of:=SUM([.B2:.B4])";
    
    // 設定第二個工作表示範
    OdfTableSheet metaSheet = workbook.Worksheets.Add("說明");
    metaSheet.Cells["A1"].CellValue = "此試算表是由 OdfKit 自動產生。";

    string outputPath = Path.Combine(outputDir, "output_spreadsheet.ods");
    workbook.Save(outputPath);
    Console.WriteLine($"   已儲存試算表至：{outputPath}");
}

/// <summary>
/// 示範採用 OdfKit 專屬的 Fluent Builder 模式建立簡報文件 (ODP)，包含投影片、幾何形狀與轉場特效。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoPresentationDocument(string outputDir)
{
    Console.WriteLine(" 3. 正在建立簡報 (ODP) ...");

    // 使用 Fluent Builder 建立簡報
    using PresentationDocument deck = PresentationDocument.Builder()
        .WithMetadata(metadata => metadata
            .Title("OdfKit 產品簡報")
            .Author("OdfKit 開發小組"))
        .AddSlide("開場投影片", slide => slide
            .AddTitle("歡迎使用 OdfKit")
            .WithSpeakerNotes("向聽眾打招呼，並引導至主題")
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
        "OdfKit 主要核心技術特點：");

    // 新增矩形幾何形狀
    extraSlide.AddShape(
        OdfShapeType.Rectangle,
        OdfLength.FromCentimeters(1),
        OdfLength.FromCentimeters(4),
        OdfLength.FromCentimeters(5),
        OdfLength.FromCentimeters(3));

    // 插入圖片到投影片
    extraSlide.AddPicture(
        CreatePngBytes(),
        OdfLength.FromCentimeters(7),
        OdfLength.FromCentimeters(4),
        OdfLength.FromCentimeters(4),
        OdfLength.FromCentimeters(3));

    // 設定轉場特效與講者備忘錄
    extraSlide.SpeakerNotes = "介紹 ODP 與多種幾何形狀繪製功能";
    extraSlide.SetTransition(OdfTransitionType.Zoom, OdfLength.FromPoints(36));

    string outputPath = Path.Combine(outputDir, "output_presentation.odp");
    deck.Save(outputPath);
    Console.WriteLine($"   已儲存簡報至：{outputPath}");
}

/// <summary>
/// 示範高效能低記憶體的 OdsStreamWriter API，適用於產生大數據報表以避免 OOM 崩潰。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoOdsStreamWriter(string outputDir)
{
    Console.WriteLine(" 4. 正在執行低記憶體高流速寫入 (OdsStreamWriter) ...");

    string outputPath = Path.Combine(outputDir, "output_stream.ods");
    FileStream fileStream;
    try
    {
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }
    catch (IOException)
    {
        Console.WriteLine($"   [警告] 無法寫入 {outputPath}，該檔案可能正被 LibreOffice 開啟佔用。");
        Console.WriteLine("   已自動切換至備用輸出路徑：output_stream_backup.ods");
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
            writer.WriteStartRow();
            writer.WriteCell($"TXN-{i:D5}");
            writer.WriteCell(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc).AddHours(i));
            writer.WriteCell(99.9d * i);
            writer.WriteCell(true);
            writer.WriteEndRow();
        }

        writer.WriteEndSheet();
    }

    Console.WriteLine($"   已透過串流成功寫入大量資料至：{outputPath}");

    // 進行 ODF 規格合規性驗證
    try
    {
        var report = OdfKit.Compliance.OdfValidator.Validate(outputPath);
        Console.WriteLine($"   [驗證結果] 是否合規：{report.IsValid}");
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
        Console.WriteLine($"   [驗證失敗] 呼叫驗證器時發生錯誤：{ex.Message}");
    }
}

/// <summary>
/// 示範如何讀寫文件 Metadata 中介資料。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoMetadataAndSecurity(string outputDir)
{
    Console.WriteLine(" 5. 正在示範中介資料 (Metadata) 讀寫設定 ...");

    string testOdtPath = Path.Combine(outputDir, "output_text.odt");
    if (!File.Exists(testOdtPath))
    {
        return;
    }

    // 載入文件並讀取中介資料
    using (var loadedDoc = TextDocument.Load(testOdtPath))
    {
        Console.WriteLine($"   [讀取成功] 文件標題：{loadedDoc.Metadata.Title}");
        Console.WriteLine($"   [讀取成功] 建立者：{loadedDoc.Metadata.Creator}");
        
        // 變更中介資料並儲存新版
        loadedDoc.Metadata.Title = "OdfKit 示範報告 (更新版)";
        loadedDoc.Metadata.Creator = "Antigravity Agent";
        
        string updatedPath = Path.Combine(outputDir, "output_text_updated.odt");
        loadedDoc.Save(updatedPath);
        Console.WriteLine($"   已更新中介資料並儲存至：{updatedPath}");
    }
}

/// <summary>
/// 示範 OdfKit 擴充套件的高階匯出功能，將 ODT 文件直接匯出為 PDF 檔案以及 HTML 網頁。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoExtensions(string outputDir)
{
    Console.WriteLine(" 6. 正在執行 PDF 與 HTML 轉換匯出展示 ...");

    // 建立臨時文字文件做為轉換來源
    using var tempDoc = TextDocument.Create();
    tempDoc.Body.Headings.Add("OdfKit 跨平台格式轉換展示", 1);
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
    Console.WriteLine($"   已成功將 ODT 匯出至 PDF 檔案：{pdfPath}");

    // 匯出成 HTML (使用 OdfKit.Extensions.Html 提供的 OdfHtmlExporter)
    string htmlPath = Path.Combine(outputDir, "output_html.html");
    string htmlContent = OdfHtmlExporter.Export(tempDoc);
    File.WriteAllText(htmlPath, htmlContent, System.Text.Encoding.UTF8);
    Console.WriteLine($"   已成功將 ODT 匯出至 HTML 網頁：{htmlPath}");
}

/// <summary>
/// 輔助方法：產生一個最基本的 1 像素 PNG 圖片 Byte 陣列，以供範例測試插入圖片。
/// </summary>
/// <returns> PNG 圖片二進位資料 </returns>
static byte[] CreatePngBytes()
{
    return Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}

/// <summary>
/// 示範高效能低記憶體的 OdtStreamWriter API，適用於以資料流寫入大型文字文件。
/// </summary>
/// <param name="outputDir"> 輸出的目標目錄 </param>
static void DemoOdtStreamWriter(string outputDir)
{
    Console.WriteLine(" 7. 正在執行低記憶體高流速寫入 (OdtStreamWriter) ...");

    string outputPath = Path.Combine(outputDir, "output_stream.odt");
    FileStream fileStream;
    try
    {
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }
    catch (IOException)
    {
        Console.WriteLine($"   [警告] 無法寫入 {outputPath}，該檔案可能正被 LibreOffice 開啟佔用。");
        Console.WriteLine("   已自動切換至備用輸出路徑：output_stream_backup.odt");
        outputPath = Path.Combine(outputDir, "output_stream_backup.odt");
        fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
    }

    // 初始化串流寫入器，此模式記憶體佔用小於 1 MB
    using (fileStream)
    using (var writer = new OdtStreamWriter(fileStream))
    {
        writer.AddHeading("OdtStreamWriter 流式寫入示範報告", 1);
        writer.AddParagraph("本文件展示了使用 OdtStreamWriter 進行高效能、低記憶體耗用的文字文件串流寫入。");

        writer.AddHeading("章節一：清單寫入", 2);
        writer.BeginList();
        writer.AddListItem("這是串流寫入的清單項目一。");
        writer.AddListItem("這是串流寫入的清單項目二。");
        writer.EndList();

        writer.AddPageBreak();

        writer.AddHeading("章節二：分頁後內容", 2);
        writer.AddParagraph("這是在強制分頁之後的段落文字。");
    }

    Console.WriteLine($"   已透過串流成功寫入大量文字至：{outputPath}");

    // 進行 ODF 規格合規性驗證
    try
    {
        var report = OdfKit.Compliance.OdfValidator.Validate(outputPath);
        Console.WriteLine($"   [驗證結果] 是否合規：{report.IsValid}");
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
        Console.WriteLine($"   [驗證失敗] 呼叫驗證器時發生錯誤：{ex.Message}");
    }
}
