using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
using OdfKit.Core;
namespace OdfKit.Extensions.Rendering;

/// <summary>
/// Runs LibreOffice conversions through a local process backend.
/// 實作以本機安裝的 LibreOffice 處理程序（soffice）進行文件轉檔的後端。
/// </summary>
public sealed class LocalProcessBackend : ILibreOfficeConversionBackend
{
    private readonly LibreOfficeRenderer _renderer;

    /// <summary>
    /// Runs LibreOffice conversions through a local process backend.
    /// 初始化 <see cref="LocalProcessBackend"/> 類別的新執行個體。
    /// </summary>
    /// <param name="renderer">The numeric value. / 可選用的自訂 LibreOfficeRenderer 實例</param>
    public LocalProcessBackend(LibreOfficeRenderer? renderer = null)
    {
        _renderer = renderer ?? new LibreOfficeRenderer();
    }

    /// <summary>
    /// Converts async.
    /// 轉換 Async。
    /// </summary>
    /// <inheritdoc />
    public async Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(input, nameof(input));
        if (string.IsNullOrEmpty(inputExtension))
            throw new ArgumentNullException(nameof(inputExtension));
        if (string.IsNullOrEmpty(convertTo))
            throw new ArgumentNullException(nameof(convertTo));

        // 建立臨時工作沙盒
        string tempSandbox = Path.Combine(Path.GetTempPath(), "OdfKit_LocalBackend_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSandbox);

        try
        {
            string inputFilePath = Path.Combine(tempSandbox, $"document.{inputExtension}");
            using (var fs = new FileStream(inputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(fs, 81920, ct).ConfigureAwait(false);
            }

            string outputFilePath = Path.Combine(tempSandbox, $"converted.{convertTo}");

            await _renderer.ConvertFileAsync(inputFilePath, outputFilePath, convertTo, ct).ConfigureAwait(false);

            if (!File.Exists(outputFilePath))
            {
                throw new FileNotFoundException(OdfLocalizer.GetMessage("Err_LocalProcessBackend_NativeLibreofficeConversionSuccessfully"));
            }

            // 回傳關閉即刪除的獨立暫存檔資料流，避免大型轉檔結果完整常駐記憶體。
            Stream result = OdfTempStreamFactory.Create(
                estimatedSize: 0,
                temporaryDirectory: null,
                async: true,
                thresholdBytes: 0);
            try
            {
                using (var fs = new FileStream(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await fs.CopyToAsync(result, 81920, ct).ConfigureAwait(false);
                }

                result.Position = 0;
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
        finally
        {
            // 清理臨時沙盒
            try
            {
                if (Directory.Exists(tempSandbox))
                {
                    Directory.Delete(tempSandbox, true);
                }
            }
            catch
            {
                // 忽略清理失敗，由作業系統或稍後機制回收
            }
        }
    }
}
