using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Tests scalar value objects against their declared ODF lexical forms.
/// 測試純量值物件是否符合其宣告的 ODF 詞法形式。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public class OdfScalarLexicalConformanceTests
{
    /// <summary>
    /// A character is one XML character rather than one displayed grapheme cluster.
    /// 字元是單一 XML 字元，而非單一顯示字素叢集。
    /// </summary>
    [Fact]
    public void CharacterModelsUseOneXmlCharacter()
    {
        foreach (string value in new[] { "A", "中", "𠀀", "😀", " ", "\t", "\n", "\r", "\u0301", "\uFFFD" })
        {
            Assert.True(OdfCharacter.TryParse(value, out OdfCharacter character));
            Assert.Equal(value, character.Value);
            Assert.Equal(value, new OdfCharacter(value).ToString());
        }
        foreach (string value in new[] { "", "ab", "e\u0301", "🇹🇼", "👨‍👩‍👧‍👦", "\0", "\u0001", new string('\uD800', 1), new string('\uDC00', 1), "\uFFFE", "\uFFFF" })
        {
            Assert.False(OdfCharacter.TryParse(value, out _));
            Assert.Throws<ArgumentException>(() => new OdfCharacter(value));
        }
    }

    /// <summary>
    /// Bounded percentage objects preserve valid spellings and reject invalid or rounded-outside values.
    /// 有界百分比物件保留合法拼寫，並拒絕不合法或經捨入掩蓋的越界值。
    /// </summary>
    [Fact]
    public void BoundedPercentModelsKeepTheSchemaLexicalForm()
    {
        foreach (string value in new[] { "0%", "00%", "01%", ".5%", "99.9%", "100.00%", "1.%", "99.9999999999999999999999999999999%" })
        {
            Assert.True(OdfPercent.TryParse(value, out OdfPercent percent));
            Assert.Equal(value, percent.Value);
            Assert.Equal(value, new OdfPercent(value).ToString());
        }
        foreach (string value in new[] { "-0%", "-.5%", "-100.000%" })
        {
            Assert.False(OdfPercent.TryParse(value, out _));
            Assert.True(OdfPercent.TryParse(value, allowNegative: true, out OdfPercent percent));
            Assert.Equal(value, percent.Value);
        }
        foreach (string value in new[] { "001%", "000%", "+1%", "100.0000000000000000000000000000001%", "-100.0000000000000000000000000000001%", "101%", "1e2%", " 1%", "1% ", "%", ".%" })
        {
            Assert.False(OdfPercent.TryParse(value, allowNegative: true, out _));
            Assert.Throws<ArgumentException>(() => new OdfPercent(value));
        }
    }
}
