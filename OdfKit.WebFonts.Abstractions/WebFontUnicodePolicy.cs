namespace OdfKit.WebFonts;

/// <summary>
/// Provides the shared Unicode policy used by WebFont coverage, manifests, and caches.
/// 提供 WebFont 覆蓋、manifest 與快取共用的 Unicode 政策。
/// </summary>
public static class WebFontUnicodePolicy
{
    /// <summary>
    /// Determines whether a mapped Unicode scalar affects inline spacing and should be retained with its source metrics.
    /// 判斷已對應的 Unicode 純量是否會影響行內間距，並應連同來源 metrics 一併保留。
    /// </summary>
    /// <param name="scalar">The Unicode scalar value. / Unicode 純量值。</param>
    /// <returns><see langword="true"/> when a mapped scalar should be retained for layout fidelity; otherwise, <see langword="false"/>. / 已對應的純量應為版面忠實度保留時為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public static bool ShouldPreserveWhenMapped(int scalar)
        => scalar is 0x0020
            or 0x00A0
            or 0x1680
            or 0x2028
            or 0x2029
            or 0x202F
            or 0x205F
            or 0x3000
            or >= 0x2000 and <= 0x200A;

    /// <summary>
    /// Determines whether a Unicode scalar requires a standalone glyph in a subset character map.
    /// 判斷 Unicode 純量是否需要在子集字型字元對照表中具有獨立 glyph。
    /// </summary>
    /// <param name="scalar">The Unicode scalar value. / Unicode 純量值。</param>
    /// <returns><see langword="true"/> when the scalar requires a standalone glyph; otherwise, <see langword="false"/>. / 純量需要獨立 glyph 時為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public static bool RequiresStandaloneGlyph(int scalar)
        => scalar != 0x0020
            && scalar != 0x00A0
            && scalar != 0x00AD
            && scalar != 0x034F
            && scalar != 0x061C
            && scalar != 0x1680
            && scalar != 0x2028
            && scalar != 0x2029
            && scalar != 0x202F
            && scalar != 0x205F
            && scalar != 0x3000
            && scalar != 0xFEFF
            && scalar is not (>= 0x180B and <= 0x180F)
            && scalar is not (>= 0x2000 and <= 0x200A)
            && scalar is not (>= 0x200B and <= 0x200F)
            && scalar is not (>= 0x202A and <= 0x202E)
            && scalar is not (>= 0x2060 and <= 0x206F)
            && scalar is not (>= 0xFE00 and <= 0xFE0F)
            && scalar is not (>= 0xFFF9 and <= 0xFFFB)
            && scalar is not (>= 0x1BCA0 and <= 0x1BCAF)
            && scalar is not (>= 0x1D173 and <= 0x1D17A)
            && scalar is not (>= 0xE0000 and <= 0xE0FFF)
            && scalar is not (>= 0x0000 and <= 0x001F)
            && scalar is not (>= 0x007F and <= 0x009F);
}
