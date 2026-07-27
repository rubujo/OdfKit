using System;
using System.IO;
using System.Runtime.InteropServices;
using OdfKit.Compliance;
using OdfKit.Extensions.Scripting;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Uses local Microsoft Office applications to smoke-test representative ODF documents.
/// 使用本機 Microsoft Office 應用程式對代表性 ODF 文件進行煙霧驗收。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Interop)]
public sealed class OfficeGuiSmokeTests
{
    /// <summary>
    /// Verifies that Word opens ODF 1.0 through 1.4 documents containing LibreOffice scripts without executing them.
    /// 驗證 Word 可開啟含 LibreOffice 指令碼的 ODF 1.0 至 1.4 文件，且不會執行指令碼。
    /// </summary>
    /// <param name="version">The ODF version to open. / 要開啟的 ODF 版本。</param>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void WordOpensScriptedOdfWithoutExecutingLibreOfficeMacros(OdfVersion version)
    {
        Type wordType = FindOfficeComType("Word.Application", "找不到 Microsoft Word COM，略過指令碼 ODF 驗收。");
        string root = CreateTempDirectory("OdfKitOfficeScriptSmoke");
        string documentPath = Path.Combine(root, $"scripted-{version}.odt");
        string markerPath = Path.Combine(root, "macro-executed.marker");
        using (TextDocument source = TextDocument.Create())
        {
            source.TargetVersion = version;
            source.AddParagraph($"OdfKit Office scripted ODF {version}");
            OdfScriptManager scripting = source.Scripting();
            string escaped = markerPath.Replace("\"", "\"\"", StringComparison.Ordinal);
            scripting.AddOrUpdateLibreOfficeBasicModule(
                "Standard",
                "Module1",
                $"Sub Main{Environment.NewLine}Open \"{escaped}\" For Output As #1{Environment.NewLine}" +
                $"Print #1, \"executed\"{Environment.NewLine}Close #1{Environment.NewLine}End Sub");
            scripting.AddOrUpdateLibreOfficePythonModule(
                "main.py",
                "def main():\n    raise RuntimeError('must not execute')\n");
            source.Save(documentPath);
        }

        dynamic? word = null;
        dynamic? documents = null;
        dynamic? document = null;
        dynamic? content = null;
        try
        {
            word = Activator.CreateInstance(wordType);
            if (word is null)
                Assert.Skip("無法啟動 Microsoft Word，略過指令碼 ODF 驗收。");
            word.Visible = false;
            word.DisplayAlerts = 0;
            word.AutomationSecurity = 3;
            documents = word.Documents;
            document = documents.Open(
                FileName: documentPath,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: false,
                OpenAndRepair: true,
                NoEncodingDialog: true);
            content = document.Content;

            Assert.Contains("OdfKit Office scripted ODF", Convert.ToString(content.Text), StringComparison.Ordinal);
            Assert.False(File.Exists(markerPath));
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft Word COM，略過指令碼 ODF 驗收。");
        }
        finally
        {
            try
            {
                document?.Close(false);
            }
            finally
            {
                word?.Quit(false);
                ReleaseComObject(content);
                ReleaseComObject(document);
                ReleaseComObject(documents);
                ReleaseComObject(word);
                CollectComReferences();
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that Excel can open the representative ODS fixture and expose cell values.
    /// 驗證 Excel 可開啟代表性 ODS fixture 並讀取儲存格值。
    /// </summary>
    [Fact]
    public void ExcelOpensRepresentativeOds()
    {
        Type excelType = FindOfficeComType("Excel.Application", "找不到 Microsoft Excel COM，略過 Excel GUI 煙霧驗收。");
        string path = ResolveFixturePath("complex-financial-model.ods");

        dynamic? excel = null;
        dynamic? workbooks = null;
        dynamic? workbook = null;
        dynamic? worksheets = null;
        dynamic? worksheet = null;
        dynamic? a1 = null;
        try
        {
            excel = Activator.CreateInstance(excelType);
            if (excel is null)
            {
                Assert.Skip("無法啟動 Microsoft Excel，略過 Excel GUI 煙霧驗收。");
            }

            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbooks = excel.Workbooks;
            workbook = workbooks.Open(path, 0, true);
            worksheets = workbook.Worksheets;
            worksheet = worksheets.Item[1];
            a1 = worksheet.Range("A1");

            Assert.Equal("月份", Convert.ToString(a1.Value2));
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft Excel COM，略過 Excel GUI 煙霧驗收。");
        }
        finally
        {
            try
            {
                workbook?.Close(false);
            }
            finally
            {
                excel?.Quit();
                ReleaseComObject(a1);
                ReleaseComObject(worksheet);
                ReleaseComObject(worksheets);
                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(excel);
                CollectComReferences();
            }
        }
    }

    /// <summary>
    /// Verifies that Word can recover and expose text from the representative ODT fixture.
    /// 驗證 Word 可復原並讀取代表性 ODT fixture 的文字。
    /// </summary>
    [Fact]
    public void WordOpensRepresentativeOdtAndExposesText()
    {
        Type wordType = FindOfficeComType("Word.Application", "找不到 Microsoft Word COM，略過 Word GUI 煙霧驗收。");
        string path = ResolveFixturePath("complex-annual-report.odt");

        dynamic? word = null;
        dynamic? documents = null;
        dynamic? document = null;
        dynamic? content = null;
        try
        {
            word = Activator.CreateInstance(wordType);
            if (word is null)
            {
                Assert.Skip("無法啟動 Microsoft Word，略過 Word GUI 煙霧驗收。");
            }

            word.Visible = false;
            word.DisplayAlerts = 0;
            documents = word.Documents;
            document = documents.Open(
                FileName: path,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: false,
                OpenAndRepair: true,
                NoEncodingDialog: true);
            content = document.Content;
            string text = Convert.ToString(content.Text) ?? string.Empty;

            Assert.Contains("年度報告", text, StringComparison.Ordinal);
            Assert.Contains("營運摘要", text, StringComparison.Ordinal);
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft Word COM，略過 Word GUI 煙霧驗收。");
        }
        finally
        {
            try
            {
                document?.Close(false);
            }
            finally
            {
                word?.Quit(false);
                ReleaseComObject(content);
                ReleaseComObject(document);
                ReleaseComObject(documents);
                ReleaseComObject(word);
                CollectComReferences();
            }
        }
    }

    /// <summary>
    /// Verifies that PowerPoint can open the representative ODP fixture and expose slides.
    /// 驗證 PowerPoint 可開啟代表性 ODP fixture 並讀取投影片。
    /// </summary>
    [Fact]
    public void PowerPointOpensRepresentativeOdp()
    {
        Type powerPointType = FindOfficeComType("PowerPoint.Application", "找不到 Microsoft PowerPoint COM，略過 PowerPoint GUI 煙霧驗收。");
        string path = ResolveFixturePath("complex-business-deck.odp");

        dynamic? powerPoint = null;
        dynamic? presentations = null;
        dynamic? presentation = null;
        dynamic? slides = null;
        try
        {
            powerPoint = Activator.CreateInstance(powerPointType);
            if (powerPoint is null)
            {
                Assert.Skip("無法啟動 Microsoft PowerPoint，略過 PowerPoint GUI 煙霧驗收。");
            }

            presentations = powerPoint.Presentations;
            presentation = presentations.Open(path, -1, 0, 0);
            slides = presentation.Slides;

            Assert.True((int)slides.Count >= 2, "PowerPoint 應可讀取代表性 ODP 的投影片。");
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft PowerPoint COM，略過 PowerPoint GUI 煙霧驗收。");
        }
        finally
        {
            try
            {
                presentation?.Close();
            }
            finally
            {
                powerPoint?.Quit();
                ReleaseComObject(slides);
                ReleaseComObject(presentation);
                ReleaseComObject(presentations);
                ReleaseComObject(powerPoint);
                CollectComReferences();
            }
        }
    }

    /// <summary>
    /// Verifies Excel can modify, save as ODS, reopen, and produce an OdfKit-readable document.
    /// 驗證 Excel 可修改、另存為 ODS、重新開啟，並產生 OdfKit 可讀取的文件。
    /// </summary>
    [Fact]
    public void ExcelModifiesSavesAndReloadsRepresentativeOds()
    {
        Type excelType = FindOfficeComType("Excel.Application", "找不到 Microsoft Excel COM，略過 Excel GUI 修改驗收。");
        string sourcePath = ResolveFixturePath("complex-financial-model.ods");
        string tempRoot = CreateTempDirectory("OdfKitOfficeExcel");
        string inputPath = Path.Combine(tempRoot, "input.ods");
        string outputPath = Path.Combine(tempRoot, "saved.ods");
        File.Copy(sourcePath, inputPath, overwrite: true);

        dynamic? excel = null;
        dynamic? workbooks = null;
        dynamic? workbook = null;
        dynamic? worksheets = null;
        dynamic? worksheet = null;
        dynamic? markerCell = null;
        try
        {
            excel = Activator.CreateInstance(excelType);
            if (excel is null)
                Assert.Skip("無法啟動 Microsoft Excel，略過 Excel GUI 修改驗收。");
            excel.Visible = false;
            excel.DisplayAlerts = false;
            workbooks = excel.Workbooks;
            workbook = workbooks.Open(inputPath, 0, false);
            worksheets = workbook.Worksheets;
            worksheet = worksheets.Item[1];
            markerCell = worksheet.Range("A2");
            markerCell.Value2 = "OdfKit-Excel-Edit-Marker";
            workbook.SaveAs(outputPath, 60);
            workbook.Close(false);
            ReleaseComObject(workbook);
            workbook = workbooks.Open(outputPath, 0, true);
            worksheets = workbook.Worksheets;
            worksheet = worksheets.Item[1];
            markerCell = worksheet.Range("A2");
            Assert.Equal("OdfKit-Excel-Edit-Marker", Convert.ToString(markerCell.Value2));

            using SpreadsheetDocument loaded = SpreadsheetDocument.Load(outputPath);
            Assert.Equal("OdfKit-Excel-Edit-Marker", loaded.Worksheets[0].Cells[1, 0].DisplayText);
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft Excel COM，略過 Excel GUI 修改驗收。");
        }
        finally
        {
            try
            {
                workbook?.Close(false);
            }
            finally
            {
                excel?.Quit();
                ReleaseComObject(markerCell);
                ReleaseComObject(worksheet);
                ReleaseComObject(worksheets);
                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(excel);
                CollectComReferences();
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies Word can modify, save as ODT, reopen, and produce an OdfKit-readable document.
    /// 驗證 Word 可修改、另存為 ODT、重新開啟，並產生 OdfKit 可讀取的文件。
    /// </summary>
    [Fact]
    public void WordModifiesSavesAndReloadsRepresentativeOdt()
    {
        Type wordType = FindOfficeComType("Word.Application", "找不到 Microsoft Word COM，略過 Word GUI 修改驗收。");
        string sourcePath = ResolveFixturePath("complex-annual-report.odt");
        string tempRoot = CreateTempDirectory("OdfKitOfficeWord");
        string inputPath = Path.Combine(tempRoot, "input.odt");
        string outputPath = Path.Combine(tempRoot, "saved.odt");
        File.Copy(sourcePath, inputPath, overwrite: true);

        dynamic? word = null;
        dynamic? documents = null;
        dynamic? document = null;
        dynamic? content = null;
        try
        {
            word = Activator.CreateInstance(wordType);
            if (word is null)
                Assert.Skip("無法啟動 Microsoft Word，略過 Word GUI 修改驗收。");
            word.Visible = false;
            word.DisplayAlerts = 0;
            documents = word.Documents;
            document = documents.Open(
                FileName: inputPath,
                ReadOnly: false,
                AddToRecentFiles: false,
                Visible: false,
                OpenAndRepair: true,
                NoEncodingDialog: true);
            content = document.Content;
            content.InsertAfter("\r\nOdfKit-Word-Edit-Marker");
            document.SaveAs2(outputPath, 23);
            document.Close(false);
            ReleaseComObject(content);
            ReleaseComObject(document);
            document = documents.Open(
                FileName: outputPath,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: false,
                OpenAndRepair: true,
                NoEncodingDialog: true);
            content = document.Content;
            Assert.Contains("OdfKit-Word-Edit-Marker", Convert.ToString(content.Text) ?? string.Empty, StringComparison.Ordinal);

            document.Close(false);
            ReleaseComObject(content);
            ReleaseComObject(document);
            content = null;
            document = null;

            using TextDocument loaded = TextDocument.Load(outputPath);
            Assert.Contains("OdfKit-Word-Edit-Marker", loaded.ContentRoot.TextContent, StringComparison.Ordinal);
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft Word COM，略過 Word GUI 修改驗收。");
        }
        finally
        {
            try
            {
                document?.Close(false);
            }
            finally
            {
                word?.Quit(false);
                ReleaseComObject(content);
                ReleaseComObject(document);
                ReleaseComObject(documents);
                ReleaseComObject(word);
                CollectComReferences();
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies PowerPoint can modify, save as ODP, reopen, and produce an OdfKit-readable document.
    /// 驗證 PowerPoint 可修改、另存為 ODP、重新開啟，並產生 OdfKit 可讀取的文件。
    /// </summary>
    [Fact]
    public void PowerPointModifiesSavesAndReloadsRepresentativeOdp()
    {
        Type powerPointType = FindOfficeComType("PowerPoint.Application", "找不到 Microsoft PowerPoint COM，略過 PowerPoint GUI 修改驗收。");
        string sourcePath = ResolveFixturePath("complex-business-deck.odp");
        string tempRoot = CreateTempDirectory("OdfKitOfficePowerPoint");
        string inputPath = Path.Combine(tempRoot, "input.odp");
        string outputPath = Path.Combine(tempRoot, "saved.odp");
        File.Copy(sourcePath, inputPath, overwrite: true);

        dynamic? powerPoint = null;
        dynamic? presentations = null;
        dynamic? presentation = null;
        dynamic? slides = null;
        dynamic? addedSlide = null;
        dynamic? textBox = null;
        try
        {
            powerPoint = Activator.CreateInstance(powerPointType);
            if (powerPoint is null)
                Assert.Skip("無法啟動 Microsoft PowerPoint，略過 PowerPoint GUI 修改驗收。");
            presentations = powerPoint.Presentations;
            presentation = presentations.Open(inputPath, 0, 0, 0);
            slides = presentation.Slides;
            int expectedCount = (int)slides.Count + 1;
            addedSlide = slides.Add(expectedCount, 12);
            textBox = addedSlide.Shapes.AddTextbox(1, 20f, 20f, 500f, 50f);
            textBox.TextFrame.TextRange.Text = "OdfKit-PowerPoint-Edit-Marker";
            presentation.SaveAs(outputPath, 35);
            presentation.Close();
            ReleaseComObject(presentation);
            presentation = presentations.Open(outputPath, -1, 0, 0);
            slides = presentation.Slides;
            Assert.Equal(expectedCount, (int)slides.Count);

            using PresentationDocument loaded = PresentationDocument.Load(outputPath);
            Assert.Equal(expectedCount, loaded.Slides.Count);
        }
        catch (COMException ex) when (IsOfficeSessionUnavailable(ex))
        {
            Assert.Skip("目前 Windows 工作階段無法啟動 Microsoft PowerPoint COM，略過 PowerPoint GUI 修改驗收。");
        }
        finally
        {
            try
            {
                presentation?.Close();
            }
            finally
            {
                powerPoint?.Quit();
                ReleaseComObject(textBox);
                ReleaseComObject(addedSlide);
                ReleaseComObject(slides);
                ReleaseComObject(presentation);
                ReleaseComObject(presentations);
                ReleaseComObject(powerPoint);
                CollectComReferences();
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static Type FindOfficeComType(string progId, string skipMessage)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Office COM 僅支援 Windows，略過 Office GUI 煙霧驗收。");
        }

#pragma warning disable CA1416
        Type? type = Type.GetTypeFromProgID(progId);
#pragma warning restore CA1416
        if (type is null)
        {
            Assert.Skip(skipMessage);
        }

        return type!;
    }

    private static string ResolveFixturePath(string fileName)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "fixtures", "corpus", "generated", "complex", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                break;
            }

            directory = directory.Parent;
        }

        Assert.Skip("找不到代表性 ODF fixture，略過 Office GUI 煙霧驗收。");
        return string.Empty;
    }

    private static string CreateTempDirectory(string prefix)
    {
        string path = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
#pragma warning disable CA1416
            Marshal.FinalReleaseComObject(instance);
#pragma warning restore CA1416
        }
    }

    private static void CollectComReferences()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static bool IsOfficeSessionUnavailable(COMException exception)
    {
        const int ErrorNoLogonSession = unchecked((int)0x80070520);
        return exception.HResult == ErrorNoLogonSession;
    }
}
