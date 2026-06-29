using System;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents an ODS sheet write job that can be generated in parallel.
/// 表示一個可並行產生的 ODS 工作表寫入工作。
/// </summary>
public sealed class OdsSheetWriteJob
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdsSheetWriteJob"/> class.
    /// 初始化 <see cref="OdsSheetWriteJob"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheetName">The sheet name. / 工作表名稱。</param>
    /// <param name="writeAsync">The asynchronous delegate that writes sheet content. / 寫入工作表內容的非同步委派。</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sheetName"/> is <see langword="null"/> or whitespace. / 當 <paramref name="sheetName"/> 為 <see langword="null"/> 或空白時擲出。</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writeAsync"/> is <see langword="null"/>. / 當 <paramref name="writeAsync"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdsSheetWriteJob(string sheetName, Func<OdsSheetWriter, CancellationToken, Task> writeAsync)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdsStreamWriter_SheetNameRequired"), nameof(sheetName));
        }

        SheetName = sheetName;
        WriteAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
    }

    /// <summary>
    /// Gets the sheet name.
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; }

    /// <summary>
    /// Gets the asynchronous delegate that writes sheet content.
    /// 取得寫入工作表內容的非同步委派。
    /// </summary>
    public Func<OdsSheetWriter, CancellationToken, Task> WriteAsync { get; }
}
