using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

public partial class LibreOfficeInteropTests
{
    /// <summary>
    /// 驗證進階 DataPilot 可由 Apache OpenOffice headless 模式開啟、往返並轉出 PDF。
    /// </summary>
    [Fact]
    public void ApacheOpenOfficeHeadlessAdvancedPivotRoundTripsOdsAndPdf()
    {
        string? sofficePath = FindApacheOpenOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip("找不到真實 Apache OpenOffice soffice binary，略過進階 DataPilot 實機互通性測試。");
        }

        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "OdfKitApacheOpenOfficePivot_" + Guid.NewGuid().ToString("N"));
        string outputDir = Path.Combine(tempRoot, "out");
        string userInstallationDir = Path.Combine(tempRoot, "profile");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(userInstallationDir);

        try
        {
            string odsPath = Path.Combine(tempRoot, "apache-openoffice-pivot.ods");
            using (SpreadsheetDocument document = SpreadsheetDocument.Create())
            {
                OdfTableSheet sheet = document.Worksheets.Add("Data");
                sheet.Cells["A1"].SetValue("Date");
                sheet.Cells["B1"].SetValue("Region");
                sheet.Cells["C1"].SetValue("Sales");
                sheet.Cells["A2"].SetValue(new DateTime(2026, 1, 5));
                sheet.Cells["B2"].SetValue("North");
                sheet.Cells["C2"].SetValue(100d);
                sheet.Cells["A3"].SetValue(new DateTime(2026, 1, 20));
                sheet.Cells["B3"].SetValue("South");
                sheet.Cells["C3"].SetValue(200d);

                var builder = new OdfPivotTableBuilder(
                        "ApacheOpenOfficePivot",
                        new OdfCellRange(0, 0, 2, 2, "Data"),
                        new OdfCellAddress(5, 0, "Data"),
                        sheet)
                    .AddRowField("Date")
                    .GroupField("Date", new OdfPivotGroupingOptions
                    {
                        DateGroup = OdfPivotDateGroup.Months,
                    })
                    .AddColumnField("Region")
                    .AddDataField("Sales")
                    .ConfigureValueField("Sales", new OdfPivotValueOptions
                    {
                        ShowValuesAs = OdfPivotShowValuesAs.PercentageOfRowTotal,
                    })
                    .WithGrandTotals(OdfPivotGrandTotal.Both)
                    .WithLayout(OdfPivotLayout.OutlineSubtotalsTop)
                    .WithFilterButton(true)
                    .WithDrillDown(true);
                builder.Refresh();
                builder.Build();
                document.Save(odsPath);
            }

            string roundTripPath = Path.Combine(outputDir, "apache-openoffice-pivot.ods");
            string pdfPath = Path.Combine(outputDir, "apache-openoffice-pivot.pdf");
            RunApacheOpenOfficeUnoRoundTrip(
                sofficePath!,
                userInstallationDir,
                odsPath,
                roundTripPath,
                pdfPath);
            Assert.True(File.Exists(roundTripPath), "Apache OpenOffice 應輸出進階 DataPilot 的 ODS 往返結果。");
            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                OdfPivotTableInfo pivot = Assert.Single(loaded.GetPivotTables());
                Assert.Equal("ApacheOpenOfficePivot", pivot.Name);
                Assert.Contains(
                    pivot.Fields,
                    field => field.SourceFieldName == "Sales" && field.Orientation == "data");
            }

            Assert.True(File.Exists(pdfPath), "Apache OpenOffice 應能將進階 DataPilot ODS 轉出 PDF。");
            Assert.True(new FileInfo(pdfPath).Length > 0, "Apache OpenOffice 的 DataPilot PDF 不應為空。");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string? FindApacheOpenOfficeSoffice()
    {
        string? configured = Environment.GetEnvironmentVariable("ODFKIT_OPENOFFICE_PATH");
        string[] candidates =
        [
            configured ?? string.Empty,
            @"C:\Program Files\OpenOffice 4\program\soffice.exe",
            @"C:\Program Files (x86)\OpenOffice 4\program\soffice.exe",
        ];
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                continue;
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(candidate);
            if (version.FileDescription?.Contains("OpenOffice", StringComparison.OrdinalIgnoreCase) == true &&
                version.CompanyName?.Contains("Apache Software Foundation", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    private static void RunApacheOpenOfficeUnoRoundTrip(
        string sofficePath,
        string userInstallationDir,
        string inputPath,
        string odsOutputPath,
        string pdfOutputPath)
    {
        string programDirectory = Path.GetDirectoryName(sofficePath) ??
            throw new InvalidOperationException("Apache OpenOffice program directory is unavailable.");
        string pythonPath = Path.Combine(programDirectory, "python.exe");
        Assert.True(File.Exists(pythonPath), "Apache OpenOffice 的 bundled Python／UNO runtime 不存在。");

        string pipeName = "odfkit_" + Guid.NewGuid().ToString("N");
        string scriptPath = Path.Combine(Path.GetDirectoryName(inputPath)!, "aoo-pivot-interop.py");
        File.WriteAllText(scriptPath, """
            import sys
            import time
            sys.path.insert(0, sys.argv[5])
            import uno
            from com.sun.star.beans import PropertyValue

            def prop(name, value):
                item = PropertyValue()
                item.Name = name
                item.Value = value
                return item

            local_context = uno.getComponentContext()
            resolver = local_context.ServiceManager.createInstanceWithContext(
                "com.sun.star.bridge.UnoUrlResolver", local_context)
            context = None
            for attempt in range(80):
                try:
                    context = resolver.resolve(
                        "uno:pipe,name=%s;urp;StarOffice.ComponentContext" % sys.argv[1])
                    break
                except Exception:
                    time.sleep(0.25)
            if context is None:
                raise RuntimeError("Timed out connecting to the Apache OpenOffice UNO pipe")

            desktop = context.ServiceManager.createInstanceWithContext(
                "com.sun.star.frame.Desktop", context)
            document = desktop.loadComponentFromURL(
                uno.systemPathToFileUrl(sys.argv[2]),
                "_blank",
                0,
                (prop("Hidden", True),))
            if document is None:
                raise RuntimeError("Apache OpenOffice could not load the ODS document")
            try:
                document.storeAsURL(
                    uno.systemPathToFileUrl(sys.argv[3]),
                    (prop("FilterName", "calc8"), prop("Overwrite", True)))
                document.storeToURL(
                    uno.systemPathToFileUrl(sys.argv[4]),
                    (prop("FilterName", "calc_pdf_Export"), prop("Overwrite", True)))
            finally:
                document.close(True)
                desktop.terminate()
            """);

        var officeStart = new ProcessStartInfo
        {
            FileName = sofficePath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        officeStart.ArgumentList.Add(
            "-env:UserInstallation=" +
            new Uri(userInstallationDir + Path.DirectorySeparatorChar).AbsoluteUri);
        officeStart.ArgumentList.Add("-headless");
        officeStart.ArgumentList.Add("-invisible");
        officeStart.ArgumentList.Add("-nologo");
        officeStart.ArgumentList.Add("-nodefault");
        officeStart.ArgumentList.Add("-nolockcheck");
        officeStart.ArgumentList.Add("-nofirststartwizard");
        officeStart.ArgumentList.Add(
            "-accept=pipe,name=" + pipeName + ";urp;StarOffice.ComponentContext");

        using Process office = Process.Start(officeStart) ??
            throw new InvalidOperationException("Unable to start Apache OpenOffice.");
        try
        {
            var pythonStart = new ProcessStartInfo
            {
                FileName = pythonPath,
                WorkingDirectory = programDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            pythonStart.ArgumentList.Add(scriptPath);
            pythonStart.ArgumentList.Add(pipeName);
            pythonStart.ArgumentList.Add(inputPath);
            pythonStart.ArgumentList.Add(odsOutputPath);
            pythonStart.ArgumentList.Add(pdfOutputPath);
            pythonStart.ArgumentList.Add(programDirectory);

            using Process python = Process.Start(pythonStart) ??
                throw new InvalidOperationException("Unable to start Apache OpenOffice UNO Python.");
            Task<string> standardOutput = python.StandardOutput.ReadToEndAsync();
            Task<string> standardError = python.StandardError.ReadToEndAsync();
            if (!python.WaitForExit(90_000))
            {
                python.Kill(entireProcessTree: true);
                python.WaitForExit();
                Assert.Fail("Apache OpenOffice UNO round-trip timed out.");
            }

            string output = standardOutput.GetAwaiter().GetResult() +
                standardError.GetAwaiter().GetResult();
            Assert.True(
                python.ExitCode == 0,
                "Apache OpenOffice UNO round-trip failed: " + output);
        }
        finally
        {
            if (!office.HasExited && !office.WaitForExit(5_000))
            {
                office.Kill(entireProcessTree: true);
                office.WaitForExit();
            }
        }
    }
}
