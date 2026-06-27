using System;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示一個可並行產生的 ODS 工作表寫入工作。
/// </summary>
public sealed class OdsSheetWriteJob
{
    /// <summary>
    /// 初始化 <see cref="OdsSheetWriteJob"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="writeAsync">寫入工作表內容的非同步委派</param>
    /// <exception cref="ArgumentException">當 <paramref name="sheetName"/> 為 null 或空白時擲出</exception>
    /// <exception cref="ArgumentNullException">當 <paramref name="writeAsync"/> 為 null 時擲出</exception>
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
    /// 取得工作表名稱。
    /// </summary>
    public string SheetName { get; }

    /// <summary>
    /// 取得寫入工作表內容的非同步委派。
    /// </summary>
    public Func<OdsSheetWriter, CancellationToken, Task> WriteAsync { get; }
}
