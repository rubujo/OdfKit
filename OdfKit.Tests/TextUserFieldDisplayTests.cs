using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies user-field declaration and display-field synchronization.
/// 驗證使用者欄位宣告與顯示欄位同步。
/// </summary>
public class TextUserFieldDisplayTests
{
    /// <summary>
    /// Verifies that SetFieldValue updates user-field-get and user-field-input displays across a round trip.
    /// 驗證 SetFieldValue 可更新 user-field-get 與 user-field-input 顯示內容並完成往返。
    /// </summary>
    [Fact]
    public void SetFieldValue_UserFields_UpdatesDisplaysAcrossRoundTrip()
    {
        using var template = TextTemplateDocument.Create();
        template.AddUserFieldDeclaration("發文日期", "string", "待填入");
        AddUserField(template, "user-field-get", "發文日期", "舊日期");
        AddUserField(template, "user-field-input", "發文日期", "舊日期");

        using var document = TextDocument.CreateFromTemplate(template);
        OdtMutationReport report = document.SetFieldValue("發文日期", "中華民國 115 年 7 月 13 日");

        Assert.Equal(3, report.UpdatedCount);
        Assert.Empty(report.AmbiguousTargets);
        Assert.Equal(
            "中華民國 115 年 7 月 13 日",
            document.FindUserFieldDeclaration("發文日期")?.Value);
        Assert.All(
            document.GetTextFields().Where(field => field.Identifier == "發文日期"),
            field => Assert.Equal("中華民國 115 年 7 月 13 日", field.DisplayText));
        Assert.Contains(document.GetTextFields(), field => field.Kind == OdfTextFieldKind.UserFieldGet);
        Assert.Contains(document.GetTextFields(), field => field.Kind == OdfTextFieldKind.UserFieldInput);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var reloaded = TextDocument.Load(stream);
        Assert.Equal(
            "中華民國 115 年 7 月 13 日",
            reloaded.FindUserFieldDeclaration("發文日期")?.Value);
        Assert.All(
            reloaded.GetTextFields().Where(field => field.Identifier == "發文日期"),
            field => Assert.Equal("中華民國 115 年 7 月 13 日", field.DisplayText));
    }

    private static void AddUserField(TextDocument document, string localName, string name, string displayText)
    {
        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        var field = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Text, "text");
        field.SetAttribute("name", OdfNamespaces.Text, name, "text");
        field.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = displayText });
        paragraph.AppendChild(field);
        document.BodyTextRoot.AppendChild(paragraph);
    }
}
