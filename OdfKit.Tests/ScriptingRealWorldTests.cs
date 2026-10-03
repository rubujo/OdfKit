using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.Extensions.Scripting;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以 LibreOffice 26.2 實際寫出的巨集文件為準：Basic 模組、程式庫檔案都帶 DOCTYPE 宣告，
/// 讀取曾因禁止 DTD 而擲出 XmlException；文件層級的事件繫結則在 <c>doc.Save()</c> 時被 DOM 蓋掉。
/// </summary>
public sealed class ScriptingRealWorldTests
{
    // LibreOffice 寫出的 Basic 模組（含 DOCTYPE 宣告）。
    private const string LibreOfficeModule =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<!DOCTYPE script:module PUBLIC \"-//OpenOffice.org//DTD OfficeDocument 1.0//EN\" \"module.dtd\">\n" +
        "<script:module xmlns:script=\"http://openoffice.org/2000/script\" script:name=\"Module1\" script:language=\"StarBasic\">Sub Hello\n    Shell(&quot;calc.exe&quot;)\nEnd Sub\n</script:module>";

    private const string LibreOfficeLibrary =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<!DOCTYPE library:library PUBLIC \"-//OpenOffice.org//DTD OfficeDocument 1.0//EN\" \"library.dtd\">\n" +
        "<library:library xmlns:library=\"http://openoffice.org/2000/library\" library:name=\"Standard\" library:readonly=\"false\" library:passwordprotected=\"false\">\n" +
        " <library:element library:name=\"Module1\"/>\n</library:library>";

    /// <summary>
    /// 帶 DOCTYPE 的 LibreOffice Basic 模組可以列出、讀取與診斷，並被巨集政策找到危險呼叫。
    /// </summary>
    [Fact]
    public void LibreOfficeBasicModuleWithDoctypeCanBeReadAndScanned()
    {
        using TextDocument document = TextDocument.Create();
        document.Package.WriteEntry("Basic/Standard/Module1.xml", Encoding.UTF8.GetBytes(LibreOfficeModule), "text/xml");
        document.Package.WriteEntry("Basic/Standard/script-lb.xml", Encoding.UTF8.GetBytes(LibreOfficeLibrary), "text/xml");
        OdfScriptManager manager = document.Scripting();

        Assert.Contains(manager.GetPackageScripts(), script => script.Path == "Basic/Standard/Module1.xml");
        Assert.Contains("Shell(&quot;calc.exe&quot;)", manager.ReadPackageScript("Basic/Standard/Module1.xml"), System.StringComparison.Ordinal);
        Assert.Single(manager.DiagnosePackageScripts());
        OdfMacroPolicyResult result = manager.EvaluateMacroPolicy(new OdfMacroSecurityPolicy());
        Assert.Contains(result.Findings, finding => finding.Capability == OdfMacroCapability.ProcessExecution);
    }

    /// <summary>
    /// 文件的 <c>Scripting()</c> 加的事件繫結要在儲存、重新載入後仍在。
    /// </summary>
    [Fact]
    public void DocumentEventBindingSurvivesSaveAndReload()
    {
        using var stream = new MemoryStream();
        using (TextDocument document = TextDocument.Create())
        {
            document.Scripting().AddDocumentEventBinding(
                "dom:load",
                "ooo:script",
                "vnd.sun.star.script:Standard.Module1.Hello?language=Basic&location=document",
                OdfScriptTargetKind.Uri);
            document.Scripting().AddInlineScript("example:lang", "source");
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using TextDocument reloaded = TextDocument.Load(stream);
        OdfScriptEventBinding binding = Assert.Single(reloaded.Scripting().GetDocumentEventBindings());
        Assert.Equal("dom:load", binding.EventName);
        Assert.Equal(OdfScriptTargetKind.Uri, binding.TargetKind);
        Assert.Single(reloaded.Scripting().GetInlineScripts());
    }
}
