using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

/// <summary>
/// 簡單字圖旗標的保留位元（bit 7）：Windows 的新細明體 mingliu.ttc 帶有這個位元，瀏覽器的字型檢查會以
/// 「reserved bit 7 must be set to zero」拒絕整個 glyf 表，因此輸出時必須清除；其餘位元與座標不動。
/// </summary>
public sealed class GlyphFlagSanitizerTests
{
    /// <summary>
    /// 含保留位元的旗標（包含重複旗標與其後的重複次數位元組）被清除，座標與其他位元不變。
    /// </summary>
    [Fact]
    public void ReservedBitIsClearedAndEverythingElseIsKept()
    {
        // 1 個輪廓、4 個點（endPts = 3）、無指令；旗標：0x91、0x89（重複 1 次，涵蓋 2 點）、0x01；再接 4 個座標位元組。
        byte[] glyph =
        [
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x0A,
            0x00, 0x03,
            0x00, 0x00,
            0x91, 0x89, 0x01, 0x01,
            0x11, 0x22, 0x33, 0x44,
        ];

        byte[]? sanitized = SfntFont.ClearReservedSimpleGlyphFlagBit(glyph);

        Assert.NotNull(sanitized);
        byte[] expected = (byte[])glyph.Clone();
        expected[14] = 0x11;
        expected[15] = 0x09;
        Assert.Equal(expected, sanitized);
        Assert.Equal(0x91, glyph[14]);
    }

    /// <summary>
    /// 沒有保留位元、複合字圖與空字圖都不複製（傳回 null）。
    /// </summary>
    [Fact]
    public void GlyphsWithoutReservedBitAreNotCopied()
    {
        byte[] clean =
        [
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x0A,
            0x00, 0x01,
            0x00, 0x00,
            0x11, 0x01,
            0x11, 0x22,
        ];
        byte[] composite = [0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0x80, 0x00, 0x00, 0x01, 0, 0];

        Assert.Null(SfntFont.ClearReservedSimpleGlyphFlagBit(clean));
        Assert.Null(SfntFont.ClearReservedSimpleGlyphFlagBit(composite));
        Assert.Null(SfntFont.ClearReservedSimpleGlyphFlagBit([]));
    }
}
