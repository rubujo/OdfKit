using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures object property to spreadsheet column mappings.
/// 設定物件屬性與試算表欄位的對應。
/// </summary>
public sealed class OdfObjectColumnMap
{
    /// <summary>
    /// Gets the column mappings.
    /// 取得欄位對應集合。
    /// </summary>
    public IList<OdfObjectColumnMapping> Columns { get; } = new List<OdfObjectColumnMapping>();

    /// <summary>
    /// Adds or updates a property mapping.
    /// 新增或更新屬性對應。
    /// </summary>
    /// <param name="propertyName">The property name. / 屬性名稱。</param>
    /// <param name="header">The optional header name. / 選用標題名稱。</param>
    /// <param name="order">The optional column order. / 選用欄位順序。</param>
    /// <param name="ignore">Whether the property is ignored. / 是否忽略此屬性。</param>
    /// <returns>The created mapping. / 已建立的對應。</returns>
    public OdfObjectColumnMapping Map(string propertyName, string? header = null, int? order = null, bool ignore = false)
    {
        OdfObjectColumnMapping? existing = Columns.FirstOrDefault(mapping => mapping.PropertyName == propertyName);
        if (existing is not null)
        {
            Columns.Remove(existing);
        }

        var mapping = new OdfObjectColumnMapping(propertyName)
        {
            Header = header,
            Order = order,
            Ignore = ignore
        };
        Columns.Add(mapping);
        return mapping;
    }

    internal OdfObjectColumnMapping? Find(string propertyName) =>
        Columns.FirstOrDefault(mapping => mapping.PropertyName == propertyName);
}
