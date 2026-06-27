using System;
using System.Collections.Generic;

namespace OdfKit.Styles;

/// <summary>
/// 定義 ODF 字型子集化擴充點。
/// </summary>
public interface IFontSubsetter
{
    /// <summary>
    /// 依指定請求建立字型子集。
    /// </summary>
    /// <param name="request">字型子集化請求</param>
    /// <returns>產生的字型子集；若回傳 <see langword="null"/>，則保留原本的字型宣告</returns>
    OdfFontSubset? CreateSubset(OdfFontSubsetRequest request);
}

/// <summary>
/// 表示單一字型子集化請求。
/// </summary>
public sealed class OdfFontSubsetRequest
{
    /// <summary>
    /// 初始化 <see cref="OdfFontSubsetRequest"/> 類別的新執行個體。
    /// </summary>
    /// <param name="fontName">ODF 字型宣告名稱</param>
    /// <param name="fontPath">解析出的原始字型檔案路徑；若無法解析則為 <see langword="null"/></param>
    /// <param name="codePoints">文件中偵測到的 PUA 自造字碼位集合</param>
    public OdfFontSubsetRequest(string fontName, string? fontPath, IReadOnlyCollection<int> codePoints)
    {
        FontName = fontName ?? throw new ArgumentNullException(nameof(fontName));
        FontPath = fontPath;
        CodePoints = codePoints ?? throw new ArgumentNullException(nameof(codePoints));
    }

    /// <summary>
    /// 取得 ODF 字型宣告名稱。
    /// </summary>
    public string FontName { get; }

    /// <summary>
    /// 取得解析出的原始字型檔案路徑；若無法解析則為 <see langword="null"/>。
    /// </summary>
    public string? FontPath { get; }

    /// <summary>
    /// 取得文件中偵測到的 PUA 自造字碼位集合。
    /// </summary>
    public IReadOnlyCollection<int> CodePoints { get; }
}

/// <summary>
/// 表示字型子集化完成後要嵌入 ODF 封裝的結果。
/// </summary>
public sealed class OdfFontSubset
{
    /// <summary>
    /// 初始化 <see cref="OdfFontSubset"/> 類別的新執行個體。
    /// </summary>
    /// <param name="bytes">字型子集二進位資料</param>
    /// <param name="extension">字型副檔名，例如 <c>.ttf</c> 或 <c>.otf</c></param>
    /// <param name="mediaType">字型 MIME 類型</param>
    public OdfFontSubset(byte[] bytes, string extension, string mediaType)
    {
        Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        Extension = NormalizeExtension(extension);
        MediaType = string.IsNullOrWhiteSpace(mediaType)
            ? "application/x-font-truetype"
            : mediaType;
    }

    /// <summary>
    /// 取得字型子集二進位資料。
    /// </summary>
    public byte[] Bytes { get; }

    /// <summary>
    /// 取得字型副檔名。
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// 取得字型 MIME 類型。
    /// </summary>
    public string MediaType { get; }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".ttf";
        }

        return extension[0] == '.' ? extension : "." + extension;
    }
}
