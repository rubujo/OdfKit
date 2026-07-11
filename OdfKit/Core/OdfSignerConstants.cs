using System;

namespace OdfKit.Core;

/// <summary>
/// ODF 數位簽章管線共用的常數。
/// </summary>
internal static class OdfSignerConstants
{
    /// <summary>
    /// 封裝內文件簽章描述檔路徑。
    /// </summary>
    internal const string SignaturePath = "META-INF/documentsignatures.xml";

    /// <summary>
    /// 判斷指定的封裝項目是否需要納入簽章涵蓋範圍（簽章時應簽署、驗證時應要求涵蓋）。
    /// </summary>
    /// <param name="normalizedEntryName">已正規化（使用 '/' 分隔且無前導斜線）的封裝項目名稱</param>
    /// <returns>若需要涵蓋，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    internal static bool IsCoverableEntry(string normalizedEntryName)
    {
        if (string.IsNullOrEmpty(normalizedEntryName) || normalizedEntryName.EndsWith("/", StringComparison.Ordinal))
            return false;

        return !string.Equals(normalizedEntryName, SignaturePath, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedEntryName, "META-INF/macrosignatures.xml", StringComparison.OrdinalIgnoreCase);
    }
}
