using FsCheck.Xunit;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Exercises public parsers with generated, adversarial inputs.
/// 以自動產生的對抗性輸入驗證公開解析器。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class SecurityPropertyTests
{
    /// <summary>
    /// Verifies arbitrary text cannot escape the non-throwing length parser contract.
    /// 驗證任意文字都不會破壞長度解析器的非擲回契約。
    /// </summary>
    /// <param name="input">The generated input. / 自動產生的輸入。</param>
    /// <returns><see langword="true"/> when the parser contract holds. / 解析器契約成立時傳回 <see langword="true"/>。</returns>
    [Property(MaxTest = 1000)]
    public bool OdfLengthTryParse_NeverThrows(string? input)
    {
        _ = OdfLength.TryParse(input, out _);
        return true;
    }

    /// <summary>
    /// Verifies arbitrary text cannot produce a cell address with invalid indexes.
    /// 驗證任意文字都不會產生索引無效的儲存格位址。
    /// </summary>
    /// <param name="input">The generated input. / 自動產生的輸入。</param>
    /// <returns><see langword="true"/> when parsing fails safely or produces valid indexes. / 安全解析失敗或產生有效索引時傳回 <see langword="true"/>。</returns>
    [Property(MaxTest = 1000)]
    public bool OdfCellAddressTryParse_ProducesNonNegativeIndexes(string? input)
    {
        return !OdfCellAddress.TryParse(input ?? string.Empty, out OdfCellAddress address)
            || (address.Row >= 0 && address.Column >= 0);
    }
}
