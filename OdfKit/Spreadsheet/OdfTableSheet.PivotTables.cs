using System;
using System.Globalization;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    /// <summary>
    /// Creates a strongly typed pivot table and configures its row, column, data, and filter fields with a delegate.
    /// 建立強型別樞紐分析表，並以設定委派配置列、欄、資料與篩選欄位。
    /// </summary>
    /// <param name="sourceRange">The source data range. / 來源資料範圍。</param>
    /// <param name="targetCell">The pivot table output start cell. / 樞紐分析表輸出起點。</param>
    /// <param name="configure">The delegate used to configure pivot table fields. / 用來設定樞紐分析表欄位的委派。</param>
    /// <returns>The XML node representing the built pivot table. / 代表建置後樞紐分析表的 XML 節點。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>. / 當 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfNode CreatePivotTable(
        OdfCellRange sourceRange,
        OdfCellAddress targetCell,
        Action<OdfPivotTableBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var builder = new OdfPivotTableBuilder(
            CreatePivotTableName(),
            EnsureSheetName(sourceRange),
            EnsureSheetName(targetCell),
            this);
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a strongly typed pivot table and configures its row, column, data, and filter fields with a delegate.
    /// 建立強型別樞紐分析表，並以設定委派配置列、欄、資料與篩選欄位。
    /// </summary>
    /// <param name="name">The pivot table name. / 樞紐分析表名稱。</param>
    /// <param name="sourceRange">The source data range. / 來源資料範圍。</param>
    /// <param name="targetCell">The pivot table output start cell. / 樞紐分析表輸出起點。</param>
    /// <param name="configure">The delegate used to configure pivot table fields. / 用來設定樞紐分析表欄位的委派。</param>
    /// <returns>The XML node representing the built pivot table. / 代表建置後樞紐分析表的 XML 節點。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="configure"/> is <see langword="null"/>. / 當 <paramref name="name"/> 或 <paramref name="configure"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfNode CreatePivotTable(
        string name,
        OdfCellRange sourceRange,
        OdfCellAddress targetCell,
        Action<OdfPivotTableBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        if (name is null)
            throw new ArgumentNullException(nameof(name));

        var builder = new OdfPivotTableBuilder(
            name,
            EnsureSheetName(sourceRange),
            EnsureSheetName(targetCell),
            this);
        configure(builder);
        return builder.Build();
    }

    private OdfCellAddress EnsureSheetName(OdfCellAddress address) =>
        address.SheetName is not null
            ? address
            : new OdfCellAddress(
                address.Row,
                address.Column,
                Name,
                address.IsRowAbsolute,
                address.IsColumnAbsolute,
                address.IsSheetAbsolute);

    private string CreatePivotTableName()
    {
        int index = _doc.GetPivotTables().Count + 1;
        string name;
        do
        {
            name = $"PivotTable{index.ToString(CultureInfo.InvariantCulture)}";
            index++;
        }
        while (PivotTableNameExists(name));

        return name;
    }

    private bool PivotTableNameExists(string name)
    {
        foreach (OdfPivotTableInfo info in _doc.GetPivotTables())
        {
            if (string.Equals(info.Name, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
