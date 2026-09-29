using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：不受信任的 ODF 超連結匯出為 RTF 後，不得破壞 RTF 群組結構。
/// 修正前，<c>HYPERLINK "…"</c> 欄位指令只跳脫 <c>\</c> 與 <c>"</c>，未跳脫 <c>{</c> 與 <c>}</c>；
/// RTF 的大括號是群組界線，目標中的 <c>}</c> 會提早結束欄位群組，
/// 使後續內容脫離欄位、整份文件的括號不再平衡。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class RtfExportInjectionTests
{
    /// <summary>計算未跳脫的大括號是否平衡，且深度不會提早降到 0 以下。</summary>
    private static (bool Balanced, int MinDepth, int MaxDepth) AnalyzeGroups(string rtf)
    {
        int depth = 0;
        int min = 0;
        int max = 0;
        for (int i = 0; i < rtf.Length; i++)
        {
            char c = rtf[i];
            if (c == '\\')
            {
                // 跳過被跳脫的字元（\\、\{、\}）；控制字的字母不影響大括號計數。
                i++;
                continue;
            }

            if (c == '{')
            {
                depth++;
                max = System.Math.Max(max, depth);
            }
            else if (c == '}')
            {
                depth--;
                min = System.Math.Min(min, depth);
            }
        }

        return (depth == 0 && min >= 0, min, max);
    }

    private static string Export(string href)
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("before ");
        paragraph.AddHyperlink(href, "click");
        document.AddParagraph("after");
        return document.ToRtf();
    }

    [Theory]
    [InlineData("http://x/a}b")]
    [InlineData("http://x/a{b")]
    [InlineData("http://x/a}}}}}}")]
    [InlineData("http://x/{{{{{{")]
    [InlineData("http://x/}{\\field{\\*\\fldinst DDEAUTO}}")]
    [InlineData("http://x/a\"}b\\")]
    public void Hyperlink_WithBracesOrControlCharacters_KeepsGroupsBalanced(string href)
    {
        string rtf = Export(href);

        (bool balanced, int minDepth, _) = AnalyzeGroups(rtf);
        Assert.True(balanced, $"RTF 大括號不平衡（最小深度 {minDepth}）。");
    }

    [Fact]
    public void Hyperlink_WithControlWordAttempt_DoesNotProduceControlWord()
    {
        string rtf = Export("http://x/}{\\field{\\*\\fldinst DDEAUTO calc}}");

        // 反斜線被跳脫為 \\，不可能形成控制字。
        Assert.DoesNotContain("{\\field{\\*\\fldinst DDEAUTO", rtf);
    }

    [Fact]
    public void Hyperlink_WithBenignTarget_IsUnchanged()
    {
        string rtf = Export("https://example.com/a?b=1&c=2");

        Assert.Contains("HYPERLINK \"https://example.com/a?b=1&c=2\"", rtf);
        (bool balanced, _, _) = AnalyzeGroups(rtf);
        Assert.True(balanced);
    }
}
