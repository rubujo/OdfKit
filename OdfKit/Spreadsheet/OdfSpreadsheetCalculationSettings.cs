using System.Globalization;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODS 活頁簿層級的計算設定高階外觀。
/// </summary>
public sealed class OdfSpreadsheetCalculationSettings
{
    private readonly OdfNode _node;

    internal OdfSpreadsheetCalculationSettings(OdfNode node)
    {
        _node = node;
    }

    /// <summary>
    /// 取得或設定是否自動尋找標籤。
    /// </summary>
    public bool? AutomaticFindLabels
    {
        get => GetBoolean("automatic-find-labels");
        set => SetBoolean("automatic-find-labels", value);
    }

    /// <summary>
    /// 取得或設定搜尋與比較是否區分大小寫。
    /// </summary>
    public bool? CaseSensitive
    {
        get => GetBoolean("case-sensitive");
        set => SetBoolean("case-sensitive", value);
    }

    /// <summary>
    /// 取得或設定 null 日期所使用的基準年份。
    /// </summary>
    public int? NullYear
    {
        get => GetInt32("null-year");
        set => SetInt32("null-year", value);
    }

    /// <summary>
    /// 取得或設定是否依顯示精度計算。
    /// </summary>
    public bool? PrecisionAsShown
    {
        get => GetBoolean("precision-as-shown");
        set => SetBoolean("precision-as-shown", value);
    }

    /// <summary>
    /// 取得或設定搜尋條件是否必須符合整個儲存格。
    /// </summary>
    public bool? SearchCriteriaMustApplyToWholeCell
    {
        get => GetBoolean("search-criteria-must-apply-to-whole-cell");
        set => SetBoolean("search-criteria-must-apply-to-whole-cell", value);
    }

    /// <summary>
    /// 取得或設定公式搜尋是否使用正規表示式。
    /// </summary>
    public bool? UseRegularExpressions
    {
        get => GetBoolean("use-regular-expressions");
        set => SetBoolean("use-regular-expressions", value);
    }

    /// <summary>
    /// 取得或設定公式搜尋是否使用萬用字元。
    /// </summary>
    public bool? UseWildcards
    {
        get => GetBoolean("use-wildcards");
        set => SetBoolean("use-wildcards", value);
    }

    /// <summary>
    /// 取得是否已存在反覆運算設定。
    /// </summary>
    public bool HasIteration => FindIterationNode() is not null;

    /// <summary>
    /// 取得或建立反覆運算設定。
    /// </summary>
    public OdfSpreadsheetIterationSettings Iteration =>
        new(FindOrCreateChild(_node, "iteration", OdfNamespaces.Table, "table"));

    /// <summary>
    /// 清除反覆運算設定。
    /// </summary>
    public void ClearIteration()
    {
        OdfNode? iteration = FindIterationNode();
        if (iteration is not null)
        {
            _node.RemoveChild(iteration);
        }
    }

    internal OdfNode Node => _node;

    private bool? GetBoolean(string localName)
    {
        string? value = _node.GetAttribute(localName, OdfNamespaces.Table);
        return bool.TryParse(value, out bool parsed) ? parsed : null;
    }

    private void SetBoolean(string localName, bool? value)
    {
        if (value.HasValue)
        {
            _node.SetAttribute(localName, OdfNamespaces.Table, value.Value ? "true" : "false", "table");
        }
        else
        {
            _node.RemoveAttribute(localName, OdfNamespaces.Table);
        }
    }

    private int? GetInt32(string localName)
    {
        string? value = _node.GetAttribute(localName, OdfNamespaces.Table);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
    }

    private void SetInt32(string localName, int? value)
    {
        if (value.HasValue)
        {
            _node.SetAttribute(localName, OdfNamespaces.Table, value.Value.ToString(CultureInfo.InvariantCulture), "table");
        }
        else
        {
            _node.RemoveAttribute(localName, OdfNamespaces.Table);
        }
    }

    private OdfNode? FindIterationNode() =>
        _node.Children.FirstOrDefault(child =>
            child.NodeType is OdfNodeType.Element &&
            child.LocalName == "iteration" &&
            child.NamespaceUri == OdfNamespaces.Table);

    private static OdfNode FindOrCreateChild(OdfNode parent, string localName, string namespaceUri, string prefix)
    {
        OdfNode? existing = parent.Children.FirstOrDefault(child =>
            child.NodeType is OdfNodeType.Element &&
            child.LocalName == localName &&
            child.NamespaceUri == namespaceUri);

        if (existing is not null)
        {
            return existing;
        }

        var created = new OdfNode(OdfNodeType.Element, localName, namespaceUri, prefix);
        parent.AppendChild(created);
        return created;
    }
}

/// <summary>
/// 表示 ODS 活頁簿層級的公式反覆運算設定。
/// </summary>
public sealed class OdfSpreadsheetIterationSettings
{
    private readonly OdfNode _node;

    internal OdfSpreadsheetIterationSettings(OdfNode node)
    {
        _node = node;
    }

    /// <summary>
    /// 取得或設定反覆運算是否啟用。
    /// </summary>
    public bool? Enabled
    {
        get => GetStatus();
        set => SetStatus(value);
    }

    /// <summary>
    /// 取得或設定最大誤差。
    /// </summary>
    public decimal? MaximumDifference
    {
        get => GetDecimal("maximum-difference");
        set => SetDecimal("maximum-difference", value);
    }

    /// <summary>
    /// 取得或設定最大反覆運算步數。
    /// </summary>
    public int? Steps
    {
        get => GetInt32("steps");
        set => SetInt32("steps", value);
    }

    internal OdfNode Node => _node;

    private bool? GetStatus()
    {
        string? value = _node.GetAttribute("status", OdfNamespaces.Table);
        return value switch
        {
            "enable" => true,
            "disable" => false,
            _ => null,
        };
    }

    private void SetStatus(bool? value)
    {
        if (value.HasValue)
        {
            _node.SetAttribute("status", OdfNamespaces.Table, value.Value ? "enable" : "disable", "table");
        }
        else
        {
            _node.RemoveAttribute("status", OdfNamespaces.Table);
        }
    }

    private decimal? GetDecimal(string localName)
    {
        string? value = _node.GetAttribute(localName, OdfNamespaces.Table);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;
    }

    private void SetDecimal(string localName, decimal? value)
    {
        if (value.HasValue)
        {
            _node.SetAttribute(localName, OdfNamespaces.Table, value.Value.ToString(CultureInfo.InvariantCulture), "table");
        }
        else
        {
            _node.RemoveAttribute(localName, OdfNamespaces.Table);
        }
    }

    private int? GetInt32(string localName)
    {
        string? value = _node.GetAttribute(localName, OdfNamespaces.Table);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;
    }

    private void SetInt32(string localName, int? value)
    {
        if (value.HasValue)
        {
            _node.SetAttribute(localName, OdfNamespaces.Table, value.Value.ToString(CultureInfo.InvariantCulture), "table");
        }
        else
        {
            _node.RemoveAttribute(localName, OdfNamespaces.Table);
        }
    }
}
