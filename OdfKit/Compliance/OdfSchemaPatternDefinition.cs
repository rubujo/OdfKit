using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 描述從結構描述中繼資料中保留的一個命名 RELAX NG 模式。
/// </summary>
public sealed class OdfSchemaPatternDefinition
{
    private readonly IReadOnlyList<OdfSchemaPatternNode> _roots;

    /// <summary>
    /// 初始化命名模式定義的新執行個體。
    /// </summary>
    /// <param name="name">定義名稱</param>
    /// <param name="roots">根節點集合</param>
    public OdfSchemaPatternDefinition(string name, IEnumerable<OdfSchemaPatternNode> roots)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfSchemaPatternDefinition_PatternCannotBeEmpty"), nameof(name));
        Name = name;
        _roots = new List<OdfSchemaPatternNode>(roots ?? throw new ArgumentNullException(nameof(roots))).AsReadOnly();
    }

    /// <summary>
    /// 取得 RELAX NG 定義名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 取得此命名模式的根節點。
    /// </summary>
    public IReadOnlyList<OdfSchemaPatternNode> Roots => _roots;
}

