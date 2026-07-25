using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Extensions.Scripting;
using Xunit;

namespace OdfKit.Tests;

public sealed class OdfScriptingExtensionTests
{
    [Theory]
    [InlineData("Err_OdfScriptManager_ArgumentNull")]
    [InlineData("Err_OdfScriptManager_InvalidArgument")]
    [InlineData("Err_OdfScriptManager_UnsupportedOperation")]
    [InlineData("Err_OdfScriptManager_InvalidDocumentStructure")]
    [InlineData("Err_OdfScriptManager_IndexOutOfRange")]
    [InlineData("Err_OdfScriptManager_UnsupportedVersion")]
    public void ScriptingExceptionMessagesAreLocalizedAcrossSupportedCultures(string key)
    {
        string english = OdfLocalizer.GetMessage(
            key,
            System.Globalization.CultureInfo.GetCultureInfo("en"),
            "value");
        foreach (string cultureName in new[]
                 {
                     "zh-TW", "da", "de", "fr", "it", "ko", "ms", "nb", "nl", "pt", "sk",
                     "ja", "es", "cs", "pl", "pt-BR"
                 })
        {
            string localized = OdfLocalizer.GetMessage(
                key,
                System.Globalization.CultureInfo.GetCultureInfo(cultureName),
                "value");
            Assert.NotEqual(english, localized);
        }
    }

    [Theory]
    [InlineData(OdfVersion.Odf10, "1.0")]
    [InlineData(OdfVersion.Odf11, "1.1")]
    [InlineData(OdfVersion.Odf12, "1.2")]
    [InlineData(OdfVersion.Odf13, "1.3")]
    [InlineData(OdfVersion.Odf14, "1.4")]
    public void StandardScriptCrudSupportsEveryKnownOdfVersion(OdfVersion version, string versionText)
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            version,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();

        Assert.Equal(version, manager.Capabilities.Version);
        Assert.True(manager.Capabilities.SupportsInlineScripts);
        int index = manager.AddInlineScript("example:language", $"script-{versionText}");

        OdfInlineScript script = Assert.Single(manager.GetInlineScripts());
        Assert.Equal(0, index);
        Assert.Equal("example:language", script.Language);
        Assert.Equal($"script-{versionText}", script.Source);

        manager.UpdateInlineScript(index, "example:updated", "updated <source>");
        script = Assert.Single(manager.GetInlineScripts());
        Assert.Equal("example:updated", script.Language);
        Assert.Equal("updated <source>", script.Source);

        XDocument content = XDocument.Parse(Encoding.UTF8.GetString(package.ReadEntry("content.xml")));
        XNamespace office = OdfNamespaces.Office;
        XNamespace scripting = OdfNamespaces.Script;
        XElement xmlScript = Assert.Single(content.Root!.Element(office + "scripts")!.Elements(office + "script"));
        Assert.Equal("example:updated", (string?)xmlScript.Attribute(scripting + "language"));

        manager.RemoveInlineScript(index);
        Assert.Empty(manager.GetInlineScripts());
    }

    [Fact]
    public void DocumentEventBindingsSupportMacroNamesAndUris()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Spreadsheet,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();

        manager.AddDocumentEventBinding(
            "dom:load",
            "ooo:script",
            "Standard.Module1.Main",
            OdfScriptTargetKind.MacroName);
        manager.AddDocumentEventBinding(
            "dom:click",
            "ooo:script",
            "vnd.sun.star.script:Standard.Module1.Click?language=Basic&location=document",
            OdfScriptTargetKind.Uri);

        IReadOnlyList<OdfScriptEventBinding> bindings = manager.GetDocumentEventBindings();
        Assert.Equal(2, bindings.Count);
        Assert.Equal(OdfScriptTargetKind.MacroName, bindings[0].TargetKind);
        Assert.Equal(OdfScriptTargetKind.Uri, bindings[1].TargetKind);

        manager.UpdateDocumentEventBinding(
            0,
            "dom:load",
            "ooo:script",
            "Standard.Module2.Main",
            OdfScriptTargetKind.MacroName);
        Assert.Equal("Standard.Module2.Main", manager.GetDocumentEventBindings()[0].Target);

        manager.RemoveDocumentEventBinding(1);
        Assert.Single(manager.GetDocumentEventBindings());
    }

    [Fact]
    public void ScriptMutationRemovesDocumentAndMacroSignatures()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf13,
            leaveOpen: true);
        package.WriteEntry("META-INF/macrosignatures.xml", Encoding.UTF8.GetBytes("<signatures/>"), "text/xml");
        package.WriteEntry("META-INF/documentsignatures.xml", Encoding.UTF8.GetBytes("<signatures/>"), "text/xml");

        package.Scripting().AddInlineScript("example:language", "source");

        Assert.False(package.HasEntry("META-INF/macrosignatures.xml"));
        Assert.False(package.HasEntry("META-INF/documentsignatures.xml"));
    }

    [Fact]
    public void LibreOfficeBasicModuleCrudMaintainsContainerAndLibraryMetadata()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();

        manager.AddOrUpdateLibreOfficeBasicModule(
            "Standard",
            "Module1",
            "Sub Main\nPrint \"Hello\"\nEnd Sub");

        Assert.True(package.HasEntry("Basic/script-lc.xml"));
        Assert.True(package.HasEntry("Basic/Standard/script-lb.xml"));
        Assert.True(package.HasEntry("Basic/Standard/Module1.xml"));
        OdfPackageScriptEntry module = Assert.Single(manager.GetPackageScripts());
        Assert.Equal(OdfPackageScriptKind.LibreOfficeBasicModule, module.Kind);
        Assert.Contains("Print \"Hello\"", manager.ReadPackageScript(module.Path), StringComparison.Ordinal);

        XNamespace library = "http://openoffice.org/2000/library";
        XDocument container = XDocument.Parse(Encoding.UTF8.GetString(package.ReadEntry("Basic/script-lc.xml")));
        XDocument moduleList = XDocument.Parse(Encoding.UTF8.GetString(package.ReadEntry("Basic/Standard/script-lb.xml")));
        Assert.Equal("Standard", (string?)Assert.Single(container.Root!.Elements(library + "library")).Attribute(library + "name"));
        Assert.Equal("Module1", (string?)Assert.Single(moduleList.Root!.Elements(library + "element")).Attribute(library + "name"));

        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        Assert.Single(manager.GetPackageScripts());
        Assert.True(manager.RemoveLibreOfficeBasicModule("Standard", "Module1"));
        Assert.Empty(manager.GetPackageScripts());
        Assert.False(manager.RemoveLibreOfficeBasicModule("Standard", "Module1"));
        Assert.True(manager.RemoveLibreOfficeBasicLibrary("Standard"));
    }

    [Fact]
    public void LibreOfficePythonModuleCrudRoundTripsThroughPackageSave()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Spreadsheet,
            OdfVersion.Odf12,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();

        string path = manager.AddOrUpdateLibreOfficePythonModule(
            "Automation/hello.py",
            "def hello():\n    return 'hello'");
        Assert.Equal("Scripts/python/Automation/hello.py", path);

        using var saved = new MemoryStream();
        package.Save(saved);
        saved.Position = 0;
        using OdfPackage reopened = OdfPackage.Open(saved, leaveOpen: true);
        OdfScriptManager reopenedManager = reopened.Scripting();
        OdfPackageScriptEntry entry = Assert.Single(reopenedManager.GetPackageScripts());
        Assert.Equal(OdfPackageScriptKind.LibreOfficePythonModule, entry.Kind);
        Assert.Contains("def hello", reopenedManager.ReadPackageScript(entry.Path), StringComparison.Ordinal);
        Assert.True(reopenedManager.RemoveLibreOfficePythonModule("Automation/hello.py"));
    }

    [Fact]
    public void LibreOfficeBasicMetadataRejectsDtdDeclarations()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        package.WriteEntry(
            "Basic/script-lc.xml",
            Encoding.UTF8.GetBytes(
                "<!DOCTYPE libraries [<!ENTITY payload 'blocked'>]>" +
                "<library:libraries xmlns:library='http://openoffice.org/2000/library'>" +
                "&payload;</library:libraries>"),
            "text/xml");

        Assert.Throws<System.Xml.XmlException>(() =>
            package.Scripting().AddOrUpdateLibreOfficeBasicModule(
                "Standard",
                "Module1",
                "Sub Main\nEnd Sub"));
    }

    [Fact]
    public void LibreOfficeBasicRemovalRejectsMalformedMetadataWithoutPartialMutation()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        package.WriteEntry(
            "Basic/Standard/script-lb.xml",
            Encoding.UTF8.GetBytes("<invalid-root/>"),
            "text/xml");

        Assert.Throws<System.Xml.XmlException>(() =>
            manager.RemoveLibreOfficeBasicModule("Standard", "Module1"));
        Assert.True(package.HasEntry("Basic/Standard/Module1.xml"));
    }

    [Fact]
    public void LibreOfficeBasicLibraryRemovalUsesCaseSensitivePackagePaths()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        manager.AddOrUpdateLibreOfficeBasicModule("standard", "Module1", "Sub Main\nEnd Sub");

        Assert.True(manager.RemoveLibreOfficeBasicLibrary("Standard"));
        Assert.False(package.HasEntry("Basic/Standard/Module1.xml"));
        Assert.True(package.HasEntry("Basic/standard/Module1.xml"));
    }

    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public void LibreOfficePackageProfilesSupportEveryKnownOdfVersion(OdfVersion version)
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            version,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();

        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        manager.AddOrUpdateLibreOfficePythonModule("hello.py", "def hello(): pass");

        Assert.Equal(version, manager.Capabilities.Version);
        Assert.Equal(2, manager.GetPackageScripts().Count);
    }

    [Theory]
    [InlineData(OdfVersion.Odf10)]
    [InlineData(OdfVersion.Odf11)]
    [InlineData(OdfVersion.Odf12)]
    [InlineData(OdfVersion.Odf13)]
    [InlineData(OdfVersion.Odf14)]
    public async Task LibreOfficeMacroSignaturesRoundTripAcrossOdfVersions(OdfVersion version)
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            version,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        manager.AddOrUpdateLibreOfficePythonModule("hello.py", "def hello():\n    return 'hello'");
        using X509Certificate2 certificate = CreateMacroSigningCertificate();

        await manager.SignLibreOfficeMacrosAsync(certificate, TestContext.Current.CancellationToken);

        Assert.True(package.HasEntry("META-INF/macrosignatures.xml"));
        var policy = new OdfMacroTrustPolicy { Mode = OdfMacroTrustMode.CustomRoot };
        policy.CustomRoots.Add(certificate);
        OdfMacroSignatureValidationResult result = await manager.VerifyLibreOfficeMacroSignaturesAsync(
            policy,
            TestContext.Current.CancellationToken);
        Assert.True(result.CryptographicValidation.IsValid);
        Assert.True(result.IsTrusted);
        Assert.False(result.IsCodeSafetyEvaluated);
        OdfSingleSignatureValidationResult signature = Assert.Single(result.CryptographicValidation.Signatures);
        Assert.Contains("Basic/Standard/Module1.xml", signature.CheckedReferences);
        Assert.Contains("Scripts/python/hello.py", signature.CheckedReferences);
        Assert.True(manager.RemoveLibreOfficeMacroSignatures());
        Assert.False(package.HasEntry("META-INF/macrosignatures.xml"));
        Assert.False(manager.RemoveLibreOfficeMacroSignatures());
    }

    [Fact]
    public async Task MacroCertificatePinningRejectsUnknownFingerprint()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        using X509Certificate2 certificate = CreateMacroSigningCertificate();
        await manager.SignLibreOfficeMacrosAsync(certificate, TestContext.Current.CancellationToken);
        var policy = new OdfMacroTrustPolicy { Mode = OdfMacroTrustMode.PinnedCertificate };
        policy.PinnedCertificateSha256.Add(new string('0', 64));

        OdfMacroSignatureValidationResult result = await manager.VerifyLibreOfficeMacroSignaturesAsync(
            policy,
            TestContext.Current.CancellationToken);

        Assert.True(result.CryptographicValidation.IsValid);
        Assert.Equal(OdfMacroTrustStatus.Untrusted, result.TrustStatus);
        Assert.True(result.TrustFailures.HasFlag(OdfMacroTrustFailure.CertificatePin));
    }

    [Fact]
    public async Task RotatingPinAndSignerIdentityPolicyUseExplicitEvaluationTime()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        using X509Certificate2 certificate = CreateMacroSigningCertificate();
        await manager.SignLibreOfficeMacrosAsync(certificate, TestContext.Current.CancellationToken);
        string fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
        DateTimeOffset evaluationTime = DateTimeOffset.UtcNow;
        var pin = new OdfMacroSignerPin(fingerprint)
        {
            ActiveFrom = evaluationTime.AddMinutes(-1),
            ActiveUntil = evaluationTime.AddMinutes(1)
        };
        var policy = new OdfMacroTrustPolicy
        {
            Mode = OdfMacroTrustMode.PinnedCertificate,
            VerificationTime = evaluationTime
        };
        policy.RotatingCertificatePins.Add(pin);
        policy.AllowedSubjects.Add(certificate.Subject);
        policy.AllowedIssuers.Add(certificate.Issuer);

        OdfMacroSignatureValidationResult trusted = await manager.VerifyLibreOfficeMacroSignaturesAsync(
            policy,
            TestContext.Current.CancellationToken);
        Assert.True(trusted.IsTrusted);

        policy.VerificationTime = evaluationTime.AddMinutes(2);
        OdfMacroSignatureValidationResult retired = await manager.VerifyLibreOfficeMacroSignaturesAsync(
            policy,
            TestContext.Current.CancellationToken);
        Assert.Equal(OdfMacroTrustStatus.Untrusted, retired.TrustStatus);
        Assert.True(retired.TrustFailures.HasFlag(OdfMacroTrustFailure.CertificatePin));
    }

    [Fact]
    public async Task SignerPolicyRejectsMissingEnhancedKeyUsageAndWrongSubject()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        using X509Certificate2 certificate = CreateMacroSigningCertificate();
        await manager.SignLibreOfficeMacrosAsync(certificate, TestContext.Current.CancellationToken);
        string fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
        var policy = new OdfMacroTrustPolicy { Mode = OdfMacroTrustMode.PinnedCertificate };
        policy.PinnedCertificateSha256.Add(fingerprint);
        policy.AllowedSubjects.Add("CN=Different Signer");
        policy.AllowedEnhancedKeyUsages.Add("1.3.6.1.5.5.7.3.3");

        OdfMacroSignatureValidationResult result = await manager.VerifyLibreOfficeMacroSignaturesAsync(
            policy,
            TestContext.Current.CancellationToken);

        Assert.Equal(OdfMacroTrustStatus.Untrusted, result.TrustStatus);
        Assert.True(result.TrustFailures.HasFlag(OdfMacroTrustFailure.Subject));
        Assert.True(result.TrustFailures.HasFlag(OdfMacroTrustFailure.EnhancedKeyUsage));
    }

    [Fact]
    public void PackageSyntaxDiagnosticsFindBasicAndPythonStructuralErrors()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf14,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nPrint \"unterminated\n");
        manager.AddOrUpdateLibreOfficePythonModule("broken.py", "def broken()\n \treturn (1");

        IReadOnlyList<OdfPackageScriptDiagnostics> results = manager.DiagnosePackageScripts();

        Assert.Equal(2, results.Count);
        Assert.Contains(
            results.Single(result => result.Language == OdfScriptSyntaxLanguage.LibreOfficeBasic).Diagnostics,
            diagnostic => diagnostic.Code == "ODFSCRIPT_BASIC_UNCLOSED_BLOCK");
        IReadOnlyList<OdfScriptSyntaxDiagnostic> python = results.Single(
            result => result.Language == OdfScriptSyntaxLanguage.Python).Diagnostics;
        Assert.Contains(python, diagnostic => diagnostic.Code == "ODFSCRIPT_PYTHON_MISSING_COLON");
        Assert.Contains(python, diagnostic => diagnostic.Code == "ODFSCRIPT_PYTHON_MIXED_INDENTATION");
        Assert.Contains(python, diagnostic => diagnostic.Code == "ODFSCRIPT_PYTHON_UNCLOSED_DELIMITER");
    }

    [Fact]
    public async Task ScannerPipelinePreservesIndependentProviderDecisions()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf10,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddOrUpdateLibreOfficeBasicModule("Standard", "Module1", "Sub Main\nEnd Sub");
        manager.AddOrUpdateLibreOfficePythonModule("main.py", "def main():\n    pass");
        var pipeline = new OdfScriptScannerPipeline(
        [
            new FixedScanner("Enterprise AV", OdfScriptScanVerdict.Clean),
            new FixedScanner("Sandbox", OdfScriptScanVerdict.NotDetected)
        ]);

        IReadOnlyList<OdfPackageScriptScanReport> reports = await manager.ScanPackageScriptsAsync(
            pipeline,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, reports.Count);
        Assert.All(reports, report => Assert.Equal(2, report.Results.Count));
        Assert.All(reports, report => Assert.Contains(
            report.Results,
            result => result.ProviderName == "Enterprise AV" && result.Verdict == OdfScriptScanVerdict.Clean));
    }

    [Fact]
    public void MacroPolicyFlagsEventsAndHighRiskBasicAndPythonCapabilities()
    {
        using var backing = new MemoryStream();
        using OdfPackage package = OdfDocumentFactory.CreatePackage(
            backing,
            OdfDocumentKind.Text,
            OdfVersion.Odf11,
            leaveOpen: true);
        OdfScriptManager manager = package.Scripting();
        manager.AddDocumentEventBinding(
            "dom:load",
            "ooo:script",
            "Standard.Module1.Main",
            OdfScriptTargetKind.MacroName);
        manager.AddOrUpdateLibreOfficeBasicModule(
            "Standard",
            "Module1",
            "Sub Main\nShell(\"tool\")\nOpen \"data\" For Input As #1\n" +
            "CreateUnoService(\"com.sun.star.system.SystemShellExecute\")\nEnd Sub");
        manager.AddOrUpdateLibreOfficePythonModule(
            "main.py",
            "import socket, subprocess\ndef main():\n    eval('1 + 1')\n");

        OdfMacroPolicyResult result = manager.EvaluateMacroPolicy(new OdfMacroSecurityPolicy());

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.AutoExecution);
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.ProcessExecution);
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.FileSystem);
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.Network);
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.UnoService);
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.DynamicCode);
    }

    [Fact]
    public async Task ExternalCompilerReportsUnavailableExecutableWithoutFallback()
    {
        var options = new OdfScriptCompilerOptions
        {
            PythonExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "python")
        };

        OdfScriptCompilationResult result = await OdfExternalScriptCompiler.DiagnoseAsync(
            "def main(): pass",
            OdfScriptCompilerBackend.PythonAst,
            options,
            TestContext.Current.CancellationToken);

        Assert.Equal(OdfScriptCompilationStatus.Unavailable, result.Status);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void FlatDocumentsSupportInlineScriptsButRejectPackageScripts()
    {
        using OdfDocument document = OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatText);
        OdfScriptManager manager = document.Scripting();

        Assert.True(manager.Capabilities.SupportsInlineScripts);
        Assert.False(manager.Capabilities.SupportsPackageScripts);
        manager.AddInlineScript("example:language", "source");
        Assert.Single(manager.GetInlineScripts());
        Assert.Throws<NotSupportedException>(() =>
            manager.AddOrUpdateLibreOfficePythonModule("hello.py", "def hello(): pass"));
        Assert.Throws<NotSupportedException>(() => manager.GetPackageScripts());
    }

    private static X509Certificate2 CreateMacroSigningCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=OdfKit Macro Test",
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
        return new X509Certificate2(
            pfx,
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
#endif
    }

    private sealed class FixedScanner(string providerName, OdfScriptScanVerdict verdict) : IOdfScriptScanner
    {
        public Task<OdfScriptScanResult> ScanAsync(
            OdfScriptScanRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(string.IsNullOrEmpty(request.Source));
            return Task.FromResult(new OdfScriptScanResult(providerName, verdict, nativeResult: null));
        }
    }
}
