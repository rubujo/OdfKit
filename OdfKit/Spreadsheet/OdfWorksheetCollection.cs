using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

/// <summary>
/// Indexes and enumerates workbook worksheets.
/// 提供活頁簿工作表的索引與列舉入口。
/// </summary>
public sealed class OdfWorksheetCollection : IEnumerable<OdfTableSheet>
{
    private readonly SpreadsheetDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfWorksheetCollection"/> class.
    /// 初始化 <see cref="OdfWorksheetCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning spreadsheet document. / 所屬試算表文件。</param>
    public OdfWorksheetCollection(SpreadsheetDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets the current worksheet count.
    /// 取得目前工作表數量。
    /// </summary>
    public int Count => _document.GetSheets().Count;

    /// <summary>
    /// Gets a worksheet by index.
    /// 依索引取得工作表。
    /// </summary>
    /// <param name="index">The zero-based worksheet index. / 採 0 為基準的工作表索引。</param>
    /// <returns>The worksheet at the specified index. / 指定索引的工作表。</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range. / 當索引超出範圍時擲出。</exception>
    public OdfTableSheet this[int index]
    {
        get
        {
            IReadOnlyList<OdfTableSheet> sheets = _document.GetSheets();
            if (index < 0 || index >= sheets.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return sheets[index];
        }
    }

    /// <summary>
    /// Gets a worksheet by name.
    /// 依名稱取得工作表。
    /// </summary>
    /// <param name="name">The worksheet name. / 工作表名稱。</param>
    /// <returns>The worksheet with the specified name. / 指定名稱的工作表。</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified worksheet cannot be found. / 當找不到指定工作表時擲出。</exception>
    public OdfTableSheet this[string name]
    {
        get
        {
            OdfTableSheet? sheet = _document.GetSheet(name);
            if (sheet is null)
            {
                throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_OdfWorksheetCollection_SheetNamedCannotFound", name));
            }

            return sheet;
        }
    }

    /// <summary>
    /// Adds a worksheet to the collection.
    /// 新增工作表。
    /// </summary>
    /// <param name="name">The worksheet name. / 工作表名稱。</param>
    /// <returns>The added worksheet. / 新增完成的工作表。</returns>
    public OdfTableSheet Add(string name)
    {
        return _document.AddSheet(name);
    }

    /// <summary>
    /// Adopts a worksheet from another document or the same document to the end of the collection.
    /// 將另一份文件或同一份文件中的工作表採納到集合末尾。
    /// </summary>
    /// <param name="sheet">The source worksheet to adopt. / 要採納的來源工作表。</param>
    /// <param name="newName">The optional new worksheet name after adoption; when unspecified, the source name is preserved. / 採納後選用的新工作表名稱；未指定時保留來源名稱。</param>
    /// <returns>The adopted worksheet that belongs to this collection's owning document. / 採納完成且屬於此集合所屬文件的工作表。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sheet"/> is <see langword="null"/>. / 當 <paramref name="sheet"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfTableSheet Adopt(OdfTableSheet sheet, string? newName = null)
    {
        return _document.AdoptSheet(sheet, newName);
    }

    /// <summary>
    /// Finds a worksheet with the specified name.
    /// 尋找指定名稱的工作表。
    /// </summary>
    /// <param name="name">The worksheet name. / 工作表名稱。</param>
    /// <returns>The found worksheet, or <see langword="null"/> if no worksheet is found. / 找到的工作表；找不到時為 <see langword="null"/>。</returns>
    public OdfTableSheet? Find(string name)
    {
        return _document.GetSheet(name);
    }

    /// <summary>
    /// Gets the worksheet enumerator.
    /// 取得工作表列舉器。
    /// </summary>
    /// <returns>The worksheet enumerator. / 工作表列舉器。</returns>
    public IEnumerator<OdfTableSheet> GetEnumerator()
    {
        return _document.GetSheets().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
