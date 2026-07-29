using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Extensions.Scripting;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

[Trait(TestCategories.Kind, TestCategories.Interop)]
public partial class LibreOfficeInteropTests
{
    /// <summary>
    /// Verifies that LibreOffice opens and re-saves an OdfKit OpenPGP document with an ephemeral real key.
    /// 驗證 LibreOffice 能以臨時真實金鑰開啟並重新儲存 OdfKit OpenPGP 文件。
    /// </summary>
    [Fact]
    public void LibreOfficeUnoOpenPgpRealKeyBidirectionalRoundTrip()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 OpenPGP 實機互通性測試。");

        string pythonPath = Path.Combine(Path.GetDirectoryName(sofficePath!)!, "python.exe");
        Assert.True(
            File.Exists(pythonPath),
            "已啟用 LibreOffice 實機互通，但安裝未包含 Python UNO runtime，無法驗證 OpenPGP 雙向互通。");

        string gpg = Environment.GetEnvironmentVariable("ODFKIT_GPG_PATH") ?? "gpg";
        const string userId = "OdfKit LibreOffice Interop <odfkit-lo-interop@example.invalid>";
        const string expectedText = "OdfKit OpenPGP LibreOffice real-key interop";
        string tempRoot = Path.Combine(
            Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? Path.GetTempPath(),
            global::OdfKit.Internal.OdfStringHelper.CreatePrefixedGuid("olo-"));
        string gpgHome = Path.Combine(tempRoot, "gnupg");
        string profileDirectory = Path.Combine(tempRoot, "profile");
        string sourcePath = Path.Combine(tempRoot, "odfkit-openpgp.odt");
        string roundTripPath = Path.Combine(tempRoot, "libreoffice-openpgp.odt");
        string resultPath = Path.Combine(tempRoot, "result.txt");
        string bridgePath = Path.Combine(tempRoot, "openpgp-roundtrip.py");
        Directory.CreateDirectory(gpgHome);
        Directory.CreateDirectory(Path.Combine(profileDirectory, "user"));

        Process? soffice = null;
        try
        {
            RunLibreOfficeInteropGpg(
                gpg,
                gpgHome,
                "--batch",
                "--yes",
                "--pinentry-mode",
                "loopback",
                "--passphrase",
                string.Empty,
                "--quick-generate-key",
                userId,
                "rsa2048",
                "encr",
                "1d");
            string listing = RunLibreOfficeInteropGpg(
                gpg,
                gpgHome,
                "--batch",
                "--with-colons",
                "--fingerprint",
                "--list-keys",
                userId);
            string fingerprint = listing
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(':'))
                .Where(fields => fields.Length > 9 && fields[0] == "fpr")
                .Select(fields => fields[9])
                .First();

            string publicKeyPath = Path.Combine(tempRoot, "public.gpg");
            string secretKeyPath = Path.Combine(tempRoot, "secret.gpg");
            RunLibreOfficeInteropGpg(gpg, gpgHome, "--batch", "--yes", "--output", publicKeyPath, "--export", fingerprint);
            RunLibreOfficeInteropGpg(
                gpg,
                gpgHome,
                "--batch",
                "--yes",
                "--pinentry-mode",
                "loopback",
                "--passphrase",
                string.Empty,
                "--output",
                secretKeyPath,
                "--export-secret-keys",
                fingerprint);

            var keyProvider = new OdfBouncyCastleOpenPgpProvider(
                File.ReadAllBytes(secretKeyPath),
                _ => []);
            var saveOptions = new OdfSaveOptions
            {
                EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp,
                OpenPgpKeyProvider = keyProvider
            };
            saveOptions.OpenPgpRecipients.Add(new OdfOpenPgpRecipient
            {
                KeyId = fingerprint,
                Recipient = userId,
                PublicKey = File.ReadAllBytes(publicKeyPath)
            });
            using (TextDocument document = TextDocument.Create())
            {
                document.Body.Paragraphs.Add(expectedText);
                document.Save(sourcePath, saveOptions);
            }

            File.WriteAllText(bridgePath, CreateOpenPgpRoundTripUnoBridgeScript(), new UTF8Encoding(false));
            File.Copy(sourcePath, roundTripPath);
            int port = Random.Shared.Next(20_000, 40_000);
            soffice = StartUnoSoffice(sofficePath!, profileDirectory, port, gpgHome);
            string output = InvokeOpenPgpRoundTrip(
                pythonPath,
                bridgePath,
                port,
                roundTripPath,
                roundTripPath,
                resultPath);

            Assert.True(File.Exists(resultPath), $"LibreOffice 未產生 OpenPGP 解密結果。UNO 輸出：{output}");
            Assert.Contains(expectedText, File.ReadAllText(resultPath), StringComparison.Ordinal);
            Assert.True(File.Exists(roundTripPath), $"LibreOffice 未產生 OpenPGP 往返文件。UNO 輸出：{output}");

            using OdfPackage roundTrip = OdfPackage.Open(
                roundTripPath,
                new OdfLoadOptions { OpenPgpKeyProvider = keyProvider });
            Assert.Contains(
                expectedText,
                Encoding.UTF8.GetString(roundTrip.ReadEntry("content.xml")),
                StringComparison.Ordinal);
            Assert.Contains(
                "LibreOffice-resaved",
                Encoding.UTF8.GetString(roundTrip.ReadEntry("content.xml")),
                StringComparison.Ordinal);
            using var archive = ZipFile.OpenRead(roundTripPath);
            using var manifestReader = new StreamReader(
                archive.GetEntry("META-INF/manifest.xml")!.Open(),
                Encoding.UTF8);
            Assert.Contains("encrypted-key", manifestReader.ReadToEnd(), StringComparison.Ordinal);
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
                DeleteScriptingInteropDirectory(tempRoot);
        }
    }

    /// <summary>
    /// Verifies that LibreOffice can decrypt and read an OdfKit-authored wholesome encrypted document.
    /// 驗證 LibreOffice 可解密並讀取 OdfKit 產生的 wholesome 加密文件。
    /// </summary>
    [Fact]
    public void LibreOfficeUnoOpensOdfKitWholesomeEncryptedDocument()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過 wholesome 加密實機互通性測試。");

        string pythonPath = Path.Combine(Path.GetDirectoryName(sofficePath!)!, "python.exe");
        Assert.True(
            File.Exists(pythonPath),
            "已啟用 LibreOffice 實機互通，但安裝未包含 Python UNO runtime，無法驗證 wholesome 反向互通。");

        const string password = "OdfKit-Wholesome-Interop";
        const string expectedText = "OdfKit wholesome encryption LibreOffice interop";
        string tempRoot = Path.Combine(Path.GetTempPath(), "OdfKitWholesomeInterop_" + Guid.NewGuid().ToString("N"));
        string profileDirectory = Path.Combine(tempRoot, "profile");
        string documentPath = Path.Combine(tempRoot, "wholesome.odt");
        string resultPath = Path.Combine(tempRoot, "result.txt");
        string bridgePath = Path.Combine(tempRoot, "open-encrypted-document.py");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(profileDirectory, "user"));

        Process? soffice = null;
        try
        {
            using (TextDocument document = TextDocument.Create())
            {
                document.Body.Paragraphs.Add(expectedText);
                document.Save(documentPath, new OdfSaveOptions
                {
                    Password = password,
                    EncryptionAlgorithm = OdfEncryptionAlgorithm.Aes256Gcm
                });
            }

            File.WriteAllText(bridgePath, CreateEncryptedDocumentUnoBridgeScript(), new UTF8Encoding(false));
            int port = Random.Shared.Next(20_000, 40_000);
            soffice = StartUnoSoffice(sofficePath!, profileDirectory, port);
            string output = InvokeEncryptedDocumentRead(
                pythonPath,
                bridgePath,
                port,
                documentPath,
                password,
                resultPath);

            Assert.True(File.Exists(resultPath), $"LibreOffice 未產生 wholesome 解密結果。UNO 輸出：{output}");
            Assert.Contains(expectedText, File.ReadAllText(resultPath), StringComparison.Ordinal);
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
                DeleteScriptingInteropDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task ExternalCompilerWorkersUsePythonAstAndProbeLibreOfficeBasic()
    {
        string? sofficePath = FindLibreOfficeSoffice();
        if (string.IsNullOrEmpty(sofficePath))
            Assert.Skip($"找不到真實 LibreOffice {GetExpectedLibreOfficeVersion()}x soffice binary，略過編譯器診斷測試。");

        string pythonPath = Path.Combine(Path.GetDirectoryName(sofficePath!)!, "python.exe");
        if (!File.Exists(pythonPath))
            Assert.Skip("LibreOffice 安裝未包含 Python UNO runtime，略過編譯器診斷測試。");

        var options = new OdfScriptCompilerOptions
        {
            PythonExecutablePath = pythonPath,
            LibreOfficeExecutablePath = sofficePath,
            LibreOfficePythonExecutablePath = pythonPath,
            Timeout = TimeSpan.FromSeconds(60)
        };
        OdfScriptCompilationResult pythonValid = await OdfExternalScriptCompiler.DiagnoseAsync(
            "def main():\n    return 1\n",
            OdfScriptCompilerBackend.PythonAst,
            options,
            TestContext.Current.CancellationToken);
        OdfScriptCompilationResult pythonInvalid = await OdfExternalScriptCompiler.DiagnoseAsync(
            "def main(\n    return 1\n",
            OdfScriptCompilerBackend.PythonAst,
            options,
            TestContext.Current.CancellationToken);
        OdfScriptCompilationResult basicSafeProbe = await OdfExternalScriptCompiler.DiagnoseAsync(
            "Sub Main\nDim Value As Integer\nValue = 1\nEnd Sub\n",
            OdfScriptCompilerBackend.LibreOfficeBasic,
            options,
            TestContext.Current.CancellationToken);
        Assert.Equal(OdfScriptCompilationStatus.Valid, pythonValid.Status);
        Assert.Equal(OdfScriptCompilationStatus.Invalid, pythonInvalid.Status);
        Assert.True(Assert.Single(pythonInvalid.Diagnostics).Line > 0);
        Assert.Equal(OdfScriptCompilationStatus.Indeterminate, basicSafeProbe.Status);
    }

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
    public async Task LibreOfficeHeadlessExecutesManagedDocumentMacros(OdfVersion version)
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
                using X509Certificate2 certificate = CreateInteropMacroSigningCertificate();
                await scripting.SignLibreOfficeMacrosAsync(
                    certificate,
                    TestContext.Current.CancellationToken);
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
                DeleteScriptingInteropDirectory(tempRoot);
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

    private static void DeleteScriptingInteropDirectory(string path)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 49)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 49)
            {
                Thread.Sleep(100);
            }
        }
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

    private static X509Certificate2 CreateInteropMacroSigningCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=OdfKit LibreOffice Interop",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign,
            true));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        byte[] pfx = certificate.Export(X509ContentType.Pfx);
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(
            pfx,
            password: null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
#else
        return new X509Certificate2(pfx);
#endif
    }

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

    private static string CreateEncryptedDocumentUnoBridgeScript() =>
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
        password = sys.argv[3]
        result_path = sys.argv[4]
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
                property_value("ReadOnly", True),
                property_value("Password", password),
            ))
        if document is None:
            raise RuntimeError("LibreOffice did not load the encrypted ODF document")

        text = document.getText().getString()
        with open(result_path, "w", encoding="utf-8") as result:
            result.write(text)
        print(f"characters={len(text)}")
        document.close(True)
        desktop.terminate()
        """;

    private static string CreateOpenPgpRoundTripUnoBridgeScript() =>
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
        roundtrip_path = sys.argv[3]
        result_path = sys.argv[4]
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
            ))
        if document is None:
            raise RuntimeError("LibreOffice did not load the OpenPGP ODF document")

        text = document.getText().getString()
        with open(result_path, "w", encoding="utf-8") as result:
            result.write(text)
        document.getText().insertString(
            document.getText().getEnd(),
            " LibreOffice-resaved",
            False)
        document.store()
        print(f"characters={len(text)}")
        document.close(True)
        desktop.terminate()
        """;

    private static Process StartUnoSoffice(
        string sofficePath,
        string profileDirectory,
        int port,
        string? gpgHome = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sofficePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (!string.IsNullOrWhiteSpace(gpgHome))
            startInfo.Environment["GNUPGHOME"] = gpgHome;
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

    private static string InvokeOpenPgpRoundTrip(
        string pythonPath,
        string bridgePath,
        int port,
        string documentPath,
        string roundTripPath,
        string resultPath)
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
        startInfo.ArgumentList.Add(roundTripPath);
        startInfo.ArgumentList.Add(resultPath);

        using Process? process = Process.Start(startInfo);
        Assert.NotNull(process);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.True(process.WaitForExit(60_000), "LibreOffice OpenPGP UNO bridge 執行逾時。");
        string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"LibreOffice OpenPGP UNO bridge 執行失敗：{output}");
        return output;
    }

    private static string RunLibreOfficeInteropGpg(
        string executable,
        string home,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["GNUPGHOME"] = home;
        // Git for Windows 提供的 MSYS GnuPG 不會轉換繼承的 Windows GNUPGHOME，
        // 但會轉換命令列路徑。明確傳入 --homedir 可同時相容 native 與 MSYS GnuPG。
        startInfo.ArgumentList.Add("--homedir");
        startInfo.ArgumentList.Add(home);
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("無法啟動 GnuPG。");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"GnuPG 結束碼 {process.ExitCode}。{Environment.NewLine}{stderr}");
        return stdout;
    }

    private static string InvokeEncryptedDocumentRead(
        string pythonPath,
        string bridgePath,
        int port,
        string documentPath,
        string password,
        string resultPath)
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
        startInfo.ArgumentList.Add(password);
        startInfo.ArgumentList.Add(resultPath);

        using Process? process = Process.Start(startInfo);
        Assert.NotNull(process);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.True(process.WaitForExit(60_000), "LibreOffice wholesome UNO bridge 執行逾時。");
        string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"LibreOffice wholesome UNO bridge 執行失敗：{output}");
        return output;
    }
}
