using System;
using System.IO;
using System.Runtime.InteropServices;
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
    /// Verifies that Excel can open the representative ODS fixture and expose cell values.
    /// 驗證 Excel 可開啟代表性 ODS fixture 並讀取儲存格值。
    /// </summary>
    [Fact]
    public void Excel_OpensRepresentativeOds()
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
    public void Word_OpensRepresentativeOdtAndExposesText()
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
    public void PowerPoint_OpensRepresentativeOdp()
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
