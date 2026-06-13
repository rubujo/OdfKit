using System;
using System.IO;

namespace OdfKit.Tests;

/// <summary>
/// 提供尋找 MockSoffice 測試執行檔的公用輔助類別。
/// </summary>
public static class MockSofficeFinder
{
    /// <summary>
    /// 取得 MockSoffice 的執行檔路徑。
    /// </summary>
    /// <returns>MockSoffice 的完整檔案路徑；若找不到則回傳空字串。</returns>
    public static string GetMockSofficePath()
    {
        string baseDir = AppContext.BaseDirectory;
        
        // 優先在測試建置輸出目錄的 MockSoffice 子目錄下尋找
        string[] possiblePaths =
        [
            Path.Combine(baseDir, "MockSoffice", "MockSoffice.exe"),
            Path.Combine(baseDir, "MockSoffice", "MockSoffice"),
            Path.Combine(baseDir, "..", "..", "..", "MockSoffice", "bin", "MockSoffice.exe"),
            Path.Combine(baseDir, "..", "..", "..", "MockSoffice", "bin", "MockSoffice"),
            Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "MockSoffice.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "MockSoffice"),
            Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "Debug", "net8.0", "MockSoffice.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "OdfKit.Tests", "MockSoffice", "bin", "Debug", "net8.0", "MockSoffice")
        ];

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }
}
