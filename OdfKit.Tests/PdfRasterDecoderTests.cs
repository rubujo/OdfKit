using System;
using OdfKit.Export.PdfInternal;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 GIF（含交錯與透明色）與 BMP 解碼出的像素；測試資料由獨立的影像函式庫產生，預期像素逐一比對。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class PdfRasterDecoderTests
{
    // 9x5、四色調色盤、索引 3 為透明色、交錯儲存。
    private const string InterlacedGifBase64 =
        "R0lGODlhCQAFAIEAAP8AAAD/AAAA/wkJCSH5BAEAAAMALAAAAAAJAAUAAAgXAAEMEBBAIEEABwcWHGhw4cGEEBtKDAgAOw==";

    private const string InterlacedGifRgbHex =
        "ff00000000000000ff00ff00ff00000000000000ff00ff00ff00000000ff00ff00ff00000000000000ff00ff00ff00000000000000ffff00000000000000ff00ff00ff00000000000000ff00ff00ff00000000ff00ff00ff00000000000000ff00ff00ff00000000000000ffff00000000000000ff00ff00ff00000000000000ff00ff00ff0000";

    private const string InterlacedGifAlphaHex =
        "ff00ffffff00ffffffffffff00ffffff00ffff00ffffff00ffffffffffff00ffffff00ffff00ffffff00ffffff";

    private const string Bmp24Base64 =
        "Qk1OAAAAAAAAADYAAAAoAAAAAwAAAAIAAAABABgAAAAAABgAAADEDgAAxA4AAAAAAAAAAAAAHhQKMmTIAwIBAAAAAAD/AP8A/wAAAAAA";

    [Fact]
    public void InterlacedGifWithTransparentColorDecodesEveryPixel()
    {
        PdfImage? image = PdfImage.TryLoad(Convert.FromBase64String(InterlacedGifBase64));

        Assert.NotNull(image);
        Assert.Equal(9, image.Width);
        Assert.Equal(5, image.Height);
        Assert.NotNull(image.Alpha);
        Assert.Equal(InterlacedGifAlphaHex, Convert.ToHexString(image.Alpha).ToLowerInvariant());

        // 透明像素的 RGB 不影響顯示，只比對不透明像素。
        byte[] expected = Convert.FromHexString(InterlacedGifRgbHex);
        for (int pixel = 0; pixel < image.Alpha.Length; pixel++)
        {
            if (image.Alpha[pixel] == 0)
            {
                continue;
            }

            for (int channel = 0; channel < 3; channel++)
            {
                Assert.Equal(expected[(pixel * 3) + channel], image.Data[(pixel * 3) + channel]);
            }
        }
    }

    [Fact]
    public void Bmp24DecodesBottomUpRows()
    {
        PdfImage? image = PdfImage.TryLoad(Convert.FromBase64String(Bmp24Base64));

        Assert.NotNull(image);
        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Null(image.Alpha);
        Assert.Equal("ff000000ff000000ff0a141ec86432010203", Convert.ToHexString(image.Data).ToLowerInvariant());
    }

    [Fact]
    public void Bmp8WithPaletteDecodes()
    {
        // 5x2、四色調色盤、由下而上、每列補到 4 位元組倍數。
        byte[] pixels = [3, 2, 1, 0, 1, 0, 0, 0, 0, 1, 2, 3, 0, 0, 0, 0];
        byte[] palette = [0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0, 0, 9, 9, 9, 0];
        int offset = 14 + 40 + palette.Length;
        var bmp = new byte[offset + pixels.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        bmp[10] = (byte)offset;
        bmp[14] = 40;
        bmp[18] = 5;
        bmp[22] = 2;
        bmp[26] = 1;
        bmp[28] = 8;
        bmp[46] = 4;
        Buffer.BlockCopy(palette, 0, bmp, 54, palette.Length);
        Buffer.BlockCopy(pixels, 0, bmp, offset, pixels.Length);

        PdfImage? image = PdfImage.TryLoad(bmp);

        Assert.NotNull(image);
        // 檔案內第二列（最後存）為影像最上列：索引 0,1,2,3,0（紅、綠、藍、灰、紅）。
        Assert.Equal("ff0000", Convert.ToHexString(image.Data, 0, 3).ToLowerInvariant());
        Assert.Equal("00ff00", Convert.ToHexString(image.Data, 3, 3).ToLowerInvariant());
        Assert.Equal("090909", Convert.ToHexString(image.Data, 9, 3).ToLowerInvariant());
    }

    [Fact]
    public void TruncatedOrUnsupportedBitmapIsRejectedWithoutThrowing()
    {
        byte[] gif = Convert.FromBase64String(InterlacedGifBase64);
        byte[] bmp = Convert.FromBase64String(Bmp24Base64);

        for (int length = 0; length < gif.Length; length += 3)
        {
            _ = PdfImage.TryLoad(gif[..length]);
        }

        for (int length = 0; length < bmp.Length; length += 3)
        {
            _ = PdfImage.TryLoad(bmp[..length]);
        }

        bmp[30] = 1; // BI_RLE8：不支援
        Assert.Null(PdfImage.TryLoad(bmp));
    }
}
