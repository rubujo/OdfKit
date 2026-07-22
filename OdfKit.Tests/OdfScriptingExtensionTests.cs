using System.Text;
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
}
