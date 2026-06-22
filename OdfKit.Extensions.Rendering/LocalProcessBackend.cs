using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
namespace OdfKit.Extensions.Rendering;

/// <summary>
/// 實作基於本地安裝 LibreOffice 進程（soffice）的文件轉檔後端。
/// </summary>
public sealed class LocalProcessBackend : ILibreOfficeConversionBackend
{
    private readonly LibreOfficeRenderer _renderer;

    /// <summary>
    /// 初始化 <see cref="LocalProcessBackend"/> 類別的新執行個體。
    /// </summary>
    /// <param name="renderer">可選用的自訂 LibreOfficeRenderer 實例。</param>
    public LocalProcessBackend(LibreOfficeRenderer? renderer = null)
    {
        _renderer = renderer ?? new LibreOfficeRenderer();
    }

    /// <inheritdoc />
    public async Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
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

            // 讀取為獨立的 MemoryStream
            var ms = new MemoryStream();
            using (var fs = new FileStream(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await fs.CopyToAsync(ms, 81920, ct).ConfigureAwait(false);
            }
            ms.Position = 0;
            return ms;
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
