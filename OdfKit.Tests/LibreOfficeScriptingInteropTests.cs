using System.Diagnostics;
using System.Globalization;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Extensions.Scripting;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

public partial class LibreOfficeInteropTests
{
    /// <summary>
    /// Executes OdfKit-authored Basic and Python document macros for every supported ODF version.
    /// 透過真實 LibreOffice 指令碼提供者，執行每個支援 ODF 版本的 Basic 與 Python 文件巨集。
    /// </summary>
    /// <param name="version">The ODF version to execute. / 要執行的 ODF 版本。</param>
    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void LibreOfficeHeadless_ExecutesManagedDocumentMacros(OdfVersion version)
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
        {
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過文件巨集執行測試。");
        }

        string programDirectory = Path.GetDirectoryName(sofficePath!)!;
        string pythonPath = Path.Combine(programDirectory, "python.exe");
        if (!File.Exists(pythonPath))
        {
            Assert.Skip("LibreOffice 安裝未包含 Python UNO runtime，略過文件巨集執行測試。");
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitLibreOfficeScripting_" + Guid.NewGuid().ToString("N"));
        string profileDirectory = Path.Combine(tempRoot, "profile");
        string basicMarkerPath = Path.Combine(tempRoot, "basic.marker");
        string pythonMarkerPath = Path.Combine(tempRoot, "python.marker");
        string documentPath = Path.Combine(tempRoot, "scripting-interop.odt");
        string bridgePath = Path.Combine(tempRoot, "invoke-document-scripts.py");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(profileDirectory, "user"));

        Process? soffice = null;
        try
        {
            using (TextDocument document = TextDocument.Create())
            {
                document.TargetVersion = version;
                document.AddParagraph($"OdfKit LibreOffice scripting interoperability {version}");
                OdfScriptManager scripting = document.Scripting();
                scripting.AddOrUpdateLibreOfficeBasicModule(
                    "Standard",
                    "Module1",
                    CreateBasicMarkerScript(basicMarkerPath));
                scripting.AddOrUpdateLibreOfficePythonModule(
                    "interop.py",
                    CreatePythonMarkerScript(pythonMarkerPath));
                document.Save(documentPath);
            }

            File.WriteAllText(
                Path.Combine(profileDirectory, "user", "registrymodifications.xcu"),
                CreateMacroSecurityProfile(),
                new UTF8Encoding(false));
            File.WriteAllText(bridgePath, CreateUnoBridgeScript(), new UTF8Encoding(false));

            int port = Random.Shared.Next(20_000, 40_000);
            soffice = StartUnoSoffice(sofficePath!, profileDirectory, port);
            string output = InvokeDocumentScripts(pythonPath, bridgePath, port, documentPath);

            Assert.True(File.Exists(basicMarkerPath), $"LibreOffice 未執行 Basic 文件巨集。UNO 輸出：{output}");
            Assert.True(File.Exists(pythonMarkerPath), $"LibreOffice 未執行 Python 文件巨集。UNO 輸出：{output}");
            Assert.Equal("OdfKit-Basic-Executed", File.ReadAllText(basicMarkerPath).Trim());
            Assert.Equal("OdfKit-Python-Executed", File.ReadAllText(pythonMarkerPath).Trim());
        }
        finally
        {
            if (soffice is not null)
            {
                if (!soffice.HasExited)
                {
                    soffice.Kill(entireProcessTree: true);
                    soffice.WaitForExit(10_000);
                }

                soffice.Dispose();
            }

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string CreateBasicMarkerScript(string markerPath)
    {
        string escaped = markerPath.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"Sub Main{Environment.NewLine}" +
            $"    Open \"{escaped}\" For Output As #1{Environment.NewLine}" +
            $"    Print #1, \"OdfKit-Basic-Executed\"{Environment.NewLine}" +
            $"    Close #1{Environment.NewLine}" +
            $"End Sub{Environment.NewLine}";
    }

    private static string CreatePythonMarkerScript(string markerPath)
    {
        string escaped = markerPath.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        return $"def main():{Environment.NewLine}" +
            $"    with open('{escaped}', 'w', encoding='utf-8') as marker:{Environment.NewLine}" +
            $"        marker.write('OdfKit-Python-Executed'){Environment.NewLine}{Environment.NewLine}" +
            $"g_exportedScripts = (main,){Environment.NewLine}";
    }

    private static string CreateMacroSecurityProfile() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<oor:items xmlns:oor=\"http://openoffice.org/2001/registry\">" +
        "<item oor:path=\"/org.openoffice.Office.Common/Security/Scripting\">" +
        "<prop oor:name=\"MacroSecurityLevel\" oor:op=\"fuse\"><value>0</value></prop>" +
        "</item></oor:items>";

    private static string CreateUnoBridgeScript() =>
        """
        import sys
        import time
        import uno

        def property_value(name, value):
            item = uno.createUnoStruct("com.sun.star.beans.PropertyValue")
            item.Name = name
            item.Value = value
            return item

        port = int(sys.argv[1])
        document_path = sys.argv[2]
        local_context = uno.getComponentContext()
        resolver = local_context.ServiceManager.createInstanceWithContext(
            "com.sun.star.bridge.UnoUrlResolver", local_context)
        context = None
        last_error = None
        for _ in range(100):
            try:
                context = resolver.resolve(
                    f"uno:socket,host=127.0.0.1,port={port};urp;StarOffice.ComponentContext")
                break
            except Exception as error:
                last_error = error
                time.sleep(0.1)
        if context is None:
            raise RuntimeError(f"Unable to connect to LibreOffice UNO: {last_error}")

        desktop = context.ServiceManager.createInstanceWithContext(
            "com.sun.star.frame.Desktop", context)
        document = desktop.loadComponentFromURL(
            uno.systemPathToFileUrl(document_path),
            "_blank",
            0,
            (
                property_value("Hidden", True),
                property_value("ReadOnly", False),
                property_value("MacroExecutionMode", 4),
            ))
        if document is None:
            raise RuntimeError("LibreOffice did not load the ODF document")

        provider = document.getScriptProvider()
        uris = (
            "vnd.sun.star.script:Standard.Module1.Main?language=Basic&location=document",
            "vnd.sun.star.script:interop.py$main?language=Python&location=document",
        )
        for uri in uris:
            script = provider.getScript(uri)
            script.invoke((), (), ())
            print(f"executed={uri}")

        document.close(True)
        desktop.terminate()
        """;

    private static Process StartUnoSoffice(string sofficePath, string profileDirectory, int port)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-env:UserInstallation=" + new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri);
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add($"--accept=socket,host=127.0.0.1,port={port};urp;StarOffice.ServiceManager");
        Process? process = Process.Start(startInfo);
        Assert.NotNull(process);
        return process;
    }

    private static string InvokeDocumentScripts(string pythonPath, string bridgePath, int port, string documentPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(bridgePath);
        startInfo.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(documentPath);

        using Process? process = Process.Start(startInfo);
        Assert.NotNull(process);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.True(process.WaitForExit(60_000), "LibreOffice Python UNO bridge 執行逾時。");
        string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"LibreOffice Python UNO bridge 執行失敗：{output}");
        return output;
    }
}
