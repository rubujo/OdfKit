using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Extensions.Rendering;

/// <summary>
/// 定義 LibreOffice 文件格式轉換的後端介面。
/// </summary>
public interface ILibreOfficeConversionBackend
{
    /// <summary>
    /// 以非同步方式將輸入的文件資料流轉換為目標格式並傳回結果資料流。
    /// </summary>
    /// <param name="input">包含來源文件內容的輸入資料流。</param>
    /// <param name="inputExtension">來源文件的副檔名（例如 <c>odt</c>、<c>ods</c> 等）。</param>
    /// <param name="convertTo">要轉換的目標格式副檔名（例如 <c>pdf</c>、<c>png</c> 等）。</param>
    /// <param name="ct">用於取消作業的取消語彙。</param>
    /// <returns>包含轉換後文件內容的結果資料流。</returns>
    Task<Stream> ConvertAsync(Stream input, string inputExtension, string convertTo, CancellationToken ct);
}
