using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// Provides the OdfFontResolver API.
/// 提供 ODF 文件的字型解析與內嵌功能。
/// </summary>
/// <remarks>
/// All members forward to <see cref="OdfFontContext.Default"/>; create an <see cref="OdfFontContext"/>
/// instance for isolated (for example per-tenant) font state.
/// 所有成員一律轉發至 <see cref="OdfFontContext.Default"/>；需要隔離的字型狀態（例如各租戶）
/// 請改建立 <see cref="OdfFontContext"/> 執行個體。
/// </remarks>
public static class OdfFontResolver
{
    /// <summary>
    /// Performs warn if unresolvable.
    /// 檢查指定字型名稱是否能成功解析出實際字型檔案；若找不到則發出一次性警告（同一名稱不重複記錄）。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <param name="context">用於警告訊息的情境描述（例如觸發此字型查詢的功能名稱）</param>
    /// <returns>若該字型可成功解析則為 <see langword="true"/></returns>
    public static bool WarnIfUnresolvable(string fontName, string context)
        => OdfFontContext.Default.WarnIfUnresolvable(fontName, context);

    /// <summary>
    /// Returns whether this instance is true type collection.
    /// 檢查指定字型檔案是否為 TrueType Collection（.ttc）格式。PDFsharp 等部分渲染後端不支援直接讀取
    /// </summary>
    /// <param name="filePath">字型檔案路徑</param>
    /// <returns>若檔案以 TTC 簽章（'ttcf'）開頭則為 <see langword="true"/></returns>
    public static bool IsTrueTypeCollection(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] signature = new byte[4];
            if (fs.Read(signature, 0, 4) != 4)
                return false;

            // 'ttcf' 的大端序位元組序列。
            return signature[0] == 0x74 && signature[1] == 0x74 && signature[2] == 0x63 && signature[3] == 0x66;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Registers fallback.
    /// 註冊字型替代對照規則（例如在無微軟字型之 Linux/Docker 上將 "MS YaHei" 對照至 "Noto Sans CJK TC"）。
    /// </summary>
    /// <param name="targetFont">要替代的目標字型名稱</param>
    /// <param name="replacementFont">用來替代的字型名稱</param>
    /// <exception cref="ArgumentNullException">當參數為空時拋出</exception>
    public static void RegisterFallback(string targetFont, string replacementFont)
        => OdfFontContext.Default.RegisterFallback(targetFont, replacementFont);

    /// <summary>
    /// Maps font.
    /// 取得指定字型的實質替代字型名稱。若無替代規則則傳回原名稱。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <returns>替代後或原字型名稱</returns>
    public static string MapFont(string fontName)
        => OdfFontContext.Default.MapFont(fontName);

    /// <summary>
    /// Gets font fallback candidates.
    /// 取得指定字型的解析候選序列，依序包含原始名稱、使用者註冊替代字型與內建跨平台替代字型。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <returns>依優先順序排列且已去除重複項目的字型候選序列</returns>
    public static IReadOnlyList<string> GetFontFallbackCandidates(string fontName)
        => OdfFontContext.Default.GetFontFallbackCandidates(fontName);

    /// <summary>
    /// Resolves font fallback.
    /// 依指定可用性探針解析第一個可使用的字型候選名稱。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <param name="isAvailable">用來判斷字型候選是否可使用的探針</param>
    /// <returns>第一個可使用的候選字型名稱，若沒有候選符合則為 null</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="isAvailable"/> 為 <see langword="null"/> 時擲出</exception>
    public static string? ResolveFontFallback(string fontName, Func<string, bool> isAvailable)
        => OdfFontContext.Default.ResolveFontFallback(fontName, isAvailable);

    /// <summary>
    /// Registers font.
    /// 顯式註冊字型對應。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <param name="filePath">字型檔案的路徑</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="fontName"/> 或 <paramref name="filePath"/> 為 null 時拋出</exception>
    /// <exception cref="FileNotFoundException">當找不到指定的字型檔案時拋出</exception>
    public static void RegisterFont(string fontName, string filePath)
        => OdfFontContext.Default.RegisterFont(fontName, filePath);

    /// <summary>
    /// Registers font directory.
    /// 註冊用於搜尋字型檔案的目錄。
    /// </summary>
    /// <param name="directoryPath">字型目錄的路徑</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="directoryPath"/> 為 null 時拋出</exception>
    /// <exception cref="DirectoryNotFoundException">當找不到指定的字型目錄時拋出</exception>
    public static void RegisterFontDirectory(string directoryPath)
        => OdfFontContext.Default.RegisterFontDirectory(directoryPath);

    /// <summary>
    /// Registers font subsetter.
    /// 註冊字型子集化擴充實作。
    /// </summary>
    /// <param name="subsetter">字型子集化實作</param>
    /// <returns>可用於還原先前註冊狀態的資源控制代碼</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="subsetter"/> 為 <see langword="null"/> 時擲出</exception>
    public static IDisposable RegisterFontSubsetter(IFontSubsetter subsetter)
        => OdfFontContext.Default.RegisterFontSubsetter(subsetter);

    /// <summary>
    /// Resolves font path.
    /// 依字型家族名稱解析字型的絕對路徑。
    /// </summary>
    /// <param name="fontName">字型名稱</param>
    /// <returns>字型檔案的絕對路徑，若無法解析則為 null</returns>
    public static string? ResolveFontPath(string fontName)
        => OdfFontContext.Default.ResolveFontPath(fontName);

    /// <summary>
    /// Performs embed fonts.
    /// 掃描並將文件中定義的所有字型內嵌至套件中。
    /// </summary>
    /// <param name="package">ODF 套件</param>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    public static void EmbedFonts(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
        => OdfFontContext.Default.EmbedFonts(package, contentRoot, stylesRoot);

    /// <summary>
    /// Performs embed font subsets.
    /// 若已註冊字型子集化實作，掃描文件中的 PUA 自造字並將對應子集字型嵌入封裝。
    /// </summary>
    /// <param name="package">ODF 套件</param>
    /// <param name="contentRoot">內容 XML 的根節點</param>
    /// <param name="stylesRoot">樣式 XML 的根節點</param>
    public static void EmbedFontSubsets(OdfPackage package, OdfNode contentRoot, OdfNode stylesRoot)
        => OdfFontContext.Default.EmbedFontSubsets(package, contentRoot, stylesRoot);
}
