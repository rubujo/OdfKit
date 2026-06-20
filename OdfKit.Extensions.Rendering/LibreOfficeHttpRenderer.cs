using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;

namespace OdfKit.Extensions.Rendering;

/// <summary>
/// 使用 Docker 轉檔 API 進行雲原生文件轉譯的渲染器，內建池化管理與併發閘道控制。
/// </summary>
public sealed class LibreOfficeHttpRenderer : IDisposable
{
    private readonly ILibreOfficeConversionBackend _backend;
    private readonly SemaphoreSlim _semaphore;
    private bool _isDisposed;

    /// <summary>
    /// 初始化 <see cref="LibreOfficeHttpRenderer"/> 類別的新執行個體。
    /// </summary>
    /// <param name="backend">轉換後端實作；若為 null 則預設使用 <see cref="UnoserverRestBackend"/>。</param>
    /// <param name="maxConcurrentCalls">最大允許並行轉檔的併發上限，預設為 4。</param>
    public LibreOfficeHttpRenderer(ILibreOfficeConversionBackend? backend = null, int maxConcurrentCalls = 4)
    {
        if (maxConcurrentCalls <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentCalls), "最大併發連線數必須大於 0。");

        _backend = backend ?? new UnoserverRestBackend();
        _semaphore = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls);
    }

    /// <summary>
    /// 將指定文件非同步轉檔為目標格式並寫入輸出資料流。
    /// </summary>
    /// <param name="document">來源文件。</param>
    /// <param name="outputStream">用以寫入轉檔結果的輸出資料流。</param>
    /// <param name="targetFormat">目標副檔名格式（例如 <c>pdf</c>）。</param>
    /// <param name="ct">用於取消作業的取消語彙。</param>
    public async Task ConvertAsync(OdfDocument document, Stream outputStream, string targetFormat, CancellationToken ct = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(LibreOfficeHttpRenderer));
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (outputStream is null)
            throw new ArgumentNullException(nameof(outputStream));
        if (string.IsNullOrEmpty(targetFormat))
            throw new ArgumentNullException(nameof(targetFormat));

        string ext = LibreOfficeRenderer.GetInputExtension(document);

        // 儲存文件為位元組陣列
        byte[] docBytes = document.SaveToBytes();
        using var inputMs = new MemoryStream(docBytes);

        // 併發閘道限流
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using Stream resultStream = await _backend.ConvertAsync(inputMs, ext, targetFormat, ct).ConfigureAwait(false);
            await resultStream.CopyToAsync(outputStream, 81920, ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 釋放此渲染器所使用的資源。
    /// </summary>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _semaphore.Dispose();
            _isDisposed = true;
        }
    }
}
