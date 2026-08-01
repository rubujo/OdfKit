using System;
using System.IO;

namespace OdfKit.Core;

/// <summary>
/// 提供同目錄暫存檔與原子發布的內部協作者。
/// </summary>
internal static class OdfAtomicFile
{
    internal static string CreateTemporaryPath(string destinationPath)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullPath)!;
        string fileName = Path.GetFileName(fullPath);
        return Path.Combine(directory, $".{fileName}.odfkit-save-{Guid.NewGuid():N}.tmp");
    }

    internal static void Publish(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, destinationPath);
        }
    }

    internal static void TryDelete(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"[OdfAtomicFile] 無法清理暫存檔案 '{temporaryPath}': {ex.Message}");
        }
    }
}
