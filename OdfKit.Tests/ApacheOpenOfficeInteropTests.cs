using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Drawing;
using OdfKit.Presentation;
using OdfKit.Spreadsheet;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 使用真實 Apache OpenOffice binary 驗證 OdfKit 文件互通性。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Interop)]
public sealed class ApacheOpenOfficeInteropTests
{
    private const int ProcessTimeoutMilliseconds = 90_000;

    /// <summary>
    /// 驗證 Apache OpenOffice 可開啟並往返 ODT、ODS、ODP 與 ODG 核心文件。
    /// </summary>
    [Fact]
    public void ApacheOpenOfficeHeadlessRoundTripsCoreDocumentKinds()
    {
        string sofficePath = GetRequiredOrSkipSoffice();
        string tempRoot = CreateTempRoot("Core");
        try
        {
            var cases = new List<(string Path, string Extension, string Marker)>
            {
                CreateTextDocument(tempRoot),
                CreateSpreadsheetDocument(tempRoot),
                CreatePresentationDocument(tempRoot),
                CreateDrawingDocument(tempRoot),
            };

            foreach ((string inputPath, string extension, string marker) in cases)
            {
                string caseName = Path.GetFileNameWithoutExtension(inputPath);
                string outputDirectory = Path.Combine(tempRoot, "out-" + caseName);
                string profileDirectory = Path.Combine(tempRoot, "profile-" + caseName);

                string outputPath = RunConversion(
                    sofficePath,
                    profileDirectory,
                    outputDirectory,
                    extension,
                    inputPath);
                AssertPackageContainsMarker(outputPath, marker);
            }
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// 驗證進階 DataPilot 可由 Apache OpenOffice headless 模式開啟、往返並轉出 PDF。
    /// </summary>
    [Fact]
    public void ApacheOpenOfficeHeadlessAdvancedPivotRoundTripsOdsAndPdf()
    {
        string sofficePath = GetRequiredOrSkipSoffice();
        string tempRoot = CreateTempRoot("Pivot");
        try
        {
            string odsPath = Path.Combine(tempRoot, "apache-openoffice-pivot.ods");
            CreatePivotDocument(odsPath);

            string roundTripPath = RunConversion(
                sofficePath,
                Path.Combine(tempRoot, "profile-roundtrip"),
                Path.Combine(tempRoot, "roundtrip"),
                "ods",
                odsPath);
            string pdfPath = RunConversion(
                sofficePath,
                Path.Combine(tempRoot, "profile-pdf"),
                Path.Combine(tempRoot, "pdf"),
                "pdf",
                roundTripPath);

            using (SpreadsheetDocument loaded = SpreadsheetDocument.Load(roundTripPath))
            {
                OdfPivotTableInfo pivot = Assert.Single(loaded.GetPivotTables());
                Assert.Equal("ApacheOpenOfficePivot", pivot.Name);
                Assert.Contains(
                    pivot.Fields,
                    field => field.SourceFieldName == "Sales" && field.Orientation == "data");
            }

            Assert.True(new FileInfo(pdfPath).Length > 0, "Apache OpenOffice 的 DataPilot PDF 不應為空。");
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static string GetRequiredOrSkipSoffice()
    {
        string? configured = Environment.GetEnvironmentVariable("ODFKIT_OPENOFFICE_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string configuredPath = Path.GetFullPath(configured);
            Assert.True(File.Exists(configuredPath), "ODFKIT_OPENOFFICE_PATH 指向不存在的檔案。");
            AssertApacheOpenOfficeBinary(configuredPath);
            return configuredPath;
        }

        string[] candidates =
        [
            @"C:\Program Files\OpenOffice 4\program\soffice.exe",
            @"C:\Program Files (x86)\OpenOffice 4\program\soffice.exe",
        ];
        foreach (string candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            AssertApacheOpenOfficeBinary(candidate);
            return Path.GetFullPath(candidate);
        }

        if (IsRequired())
        {
            Assert.Fail("ODFKIT_REQUIRE_OPENOFFICE 已啟用，但找不到真實 Apache OpenOffice soffice binary。");
        }

        Assert.Skip("找不到真實 Apache OpenOffice soffice binary，略過實機互通性測試。");
        return string.Empty;
    }

    private static void AssertApacheOpenOfficeBinary(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
        Assert.Contains("OpenOffice", version.FileDescription ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Apache Software Foundation",
            version.CompanyName ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRequired() =>
        string.Equals(
            Environment.GetEnvironmentVariable("ODFKIT_REQUIRE_OPENOFFICE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static string RunConversion(
        string sofficePath,
        string profileDirectory,
        string outputDirectory,
        string outputExtension,
        string inputPath)
    {
        Directory.CreateDirectory(outputDirectory);
        string pythonPath = Path.Combine(Path.GetDirectoryName(sofficePath)!, "python.exe");
        Assert.True(File.Exists(pythonPath), "Apache OpenOffice 安裝未包含官方 Python UNO runtime。");

        int port = GetAvailableTcpPort();
        string bridgePath = Path.Combine(outputDirectory, "odfkit-aoo-bridge.py");
        File.WriteAllText(bridgePath, CreateUnoConversionScript(), new UTF8Encoding(false));
        string outputPath = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(inputPath) + "." + outputExtension);
        string filterName = GetUnoFilterName(inputPath, outputExtension);

        var officeStartInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        officeStartInfo.ArgumentList.Add(
            "-env:UserInstallation=" +
            new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri);
        officeStartInfo.ArgumentList.Add("-headless");
        officeStartInfo.ArgumentList.Add("-invisible");
        officeStartInfo.ArgumentList.Add("-nologo");
        officeStartInfo.ArgumentList.Add("-nodefault");
        officeStartInfo.ArgumentList.Add("-nolockcheck");
        officeStartInfo.ArgumentList.Add("-nofirststartwizard");
        officeStartInfo.ArgumentList.Add("-norestore");
        officeStartInfo.ArgumentList.Add(
            $"-accept=socket,host=127.0.0.1,port={port};urp;StarOffice.ServiceManager");

        using Process officeProcess = Process.Start(officeStartInfo) ??
            throw new InvalidOperationException("Unable to start Apache OpenOffice.");
        Task<string> officeOutput = officeProcess.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> officeError = officeProcess.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        var bridgeStartInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            WorkingDirectory = Path.GetDirectoryName(pythonPath),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        bridgeStartInfo.Environment["PYTHONPATH"] = Path.GetDirectoryName(pythonPath);
        bridgeStartInfo.ArgumentList.Add(bridgePath);
        bridgeStartInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        bridgeStartInfo.ArgumentList.Add(inputPath);
        bridgeStartInfo.ArgumentList.Add(outputPath);
        bridgeStartInfo.ArgumentList.Add(filterName);

        using Process bridgeProcess = Process.Start(bridgeStartInfo) ??
            throw new InvalidOperationException("Unable to start Apache OpenOffice Python UNO bridge.");
        Task<string> bridgeOutput = bridgeProcess.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> bridgeError = bridgeProcess.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        bool bridgeExited = bridgeProcess.WaitForExit(ProcessTimeoutMilliseconds);
        if (!bridgeExited)
        {
            bridgeProcess.Kill(entireProcessTree: true);
            bridgeProcess.WaitForExit();
        }

        if (!officeProcess.WaitForExit(5_000))
        {
            officeProcess.Kill(entireProcessTree: true);
            officeProcess.WaitForExit();
        }

        string diagnostics = bridgeOutput.GetAwaiter().GetResult() +
            bridgeError.GetAwaiter().GetResult() +
            officeOutput.GetAwaiter().GetResult() +
            officeError.GetAwaiter().GetResult();
        Assert.True(bridgeExited, "Apache OpenOffice Python UNO bridge timed out: " + diagnostics);
        Assert.True(
            bridgeProcess.ExitCode == 0,
            "Apache OpenOffice Python UNO bridge failed: " + diagnostics);
        Assert.True(
            File.Exists(outputPath),
            "Apache OpenOffice 未產生預期輸出：" + outputPath + Environment.NewLine + diagnostics);
        return outputPath;
    }

    private static int GetAvailableTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string GetUnoFilterName(string inputPath, string outputExtension)
    {
        string inputExtension = Path.GetExtension(inputPath);
        if (string.Equals(outputExtension, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return inputExtension.ToLowerInvariant() switch
            {
                ".odt" => "writer_pdf_Export",
                ".ods" => "calc_pdf_Export",
                ".odp" => "impress_pdf_Export",
                ".odg" => "draw_pdf_Export",
                _ => throw new ArgumentException("Unsupported Apache OpenOffice PDF input.", nameof(inputPath)),
            };
        }

        return outputExtension.ToLowerInvariant() switch
        {
            "odt" => "writer8",
            "ods" => "calc8",
            "odp" => "impress8",
            "odg" => "draw8",
            _ => throw new ArgumentException("Unsupported Apache OpenOffice output format.", nameof(outputExtension)),
        };
    }

    private static string CreateUnoConversionScript() =>
        """
        # -*- coding: utf-8 -*-
        import sys
        import time
        import uno

        def property_value(name, value):
            item = uno.createUnoStruct("com.sun.star.beans.PropertyValue")
            item.Name = name
            item.Value = value
            return item

        port = int(sys.argv[1])
        input_path = sys.argv[2]
        output_path = sys.argv[3]
        filter_name = sys.argv[4]
        local_context = uno.getComponentContext()
        resolver = local_context.ServiceManager.createInstanceWithContext(
            "com.sun.star.bridge.UnoUrlResolver", local_context)
        context = None
        last_error = None
        for unused in range(300):
            try:
                context = resolver.resolve(
                    "uno:socket,host=127.0.0.1,port=%d;urp;StarOffice.ComponentContext" % port)
                break
            except Exception as error:
                last_error = error
                time.sleep(0.1)
        if context is None:
            raise RuntimeError("Unable to connect to Apache OpenOffice UNO: %s" % last_error)

        desktop = context.ServiceManager.createInstanceWithContext(
            "com.sun.star.frame.Desktop", context)
        document = desktop.loadComponentFromURL(
            uno.systemPathToFileUrl(input_path),
            "_blank",
            0,
            (
                property_value("Hidden", True),
                property_value("ReadOnly", False),
            ))
        if document is None:
            raise RuntimeError("Apache OpenOffice did not load the ODF document")

        output_properties = (
            property_value("FilterName", filter_name),
            property_value("Overwrite", True),
        )
        if filter_name.endswith("_pdf_Export"):
            document.storeToURL(uno.systemPathToFileUrl(output_path), output_properties)
        else:
            document.storeAsURL(uno.systemPathToFileUrl(output_path), output_properties)
        document.close(True)
        desktop.terminate()
        print("stored=%s" % output_path)
        """;

    private static (string Path, string Extension, string Marker) CreateTextDocument(string tempRoot)
    {
        const string marker = "OdfKit-AOO-ODT-Marker";
        string path = Path.Combine(tempRoot, "core-text.odt");
        using TextDocument document = TextDocument.Create();
        document.AddParagraph(marker);
        document.Save(path);
        return (path, "odt", marker);
    }

    private static (string Path, string Extension, string Marker) CreateSpreadsheetDocument(string tempRoot)
    {
        const string marker = "OdfKit-AOO-ODS-Marker";
        string path = Path.Combine(tempRoot, "core-spreadsheet.ods");
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        document.Worksheets.Add("Data").Cells["A1"].SetValue(marker);
        document.Save(path);
        return (path, "ods", marker);
    }

    private static (string Path, string Extension, string Marker) CreatePresentationDocument(string tempRoot)
    {
        const string marker = "OdfKit-AOO-ODP-Marker";
        string path = Path.Combine(tempRoot, "core-presentation.odp");
        using PresentationDocument document = PresentationDocument.Create();
        document.AddSlide(marker);
        document.Save(path);
        return (path, "odp", marker);
    }

    private static (string Path, string Extension, string Marker) CreateDrawingDocument(string tempRoot)
    {
        const string marker = "OdfKit-AOO-ODG-Marker";
        string path = Path.Combine(tempRoot, "core-drawing.odg");
        using DrawingDocument document = DrawingDocument.Create();
        document.AddPage(marker);
        document.Save(path);
        return (path, "odg", marker);
    }

    private static void CreatePivotDocument(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
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
        document.Save(path);
    }

    private static void AssertPackageContainsMarker(string path, string marker)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        ZipArchiveEntry? entry = archive.GetEntry("content.xml");
        Assert.NotNull(entry);
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        Assert.Contains(marker, reader.ReadToEnd(), StringComparison.Ordinal);
    }

    private static string CreateTempRoot(string suffix)
    {
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "OdfKitApacheOpenOffice" + suffix + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!Directory.Exists(tempRoot))
            {
                return;
            }

            try
            {
                Directory.Delete(tempRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(200);
            }
        }
    }
}
