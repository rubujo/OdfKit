using System;

namespace OdfKit.DOM;

/// <summary>
/// Provides the OdfTypedDomAttributeDatatypeCoverage API.
/// 表示單一 schema 屬性值類型與 typed DOM 屬性 helper 的對照結果。
/// </summary>
public sealed class OdfTypedDomAttributeDatatypeCoverage
{
    /// <summary>
    /// Executes the OdfTypedDomAttributeDatatypeCoverage operation.
    /// 初始化 schema 屬性值類型覆蓋專案。
    /// </summary>
    /// <param name="schemaValueType">schema 宣告的屬性值類型</param>
    /// <param name="schemaAttributeCount">使用此類型的 schema 屬性數</param>
    /// <param name="wrapperPropertyType">對應的 wrapper 屬性類型</param>
    /// <param name="wrapperPropertyCount">使用此 wrapper 屬性類型的公開屬性數</param>
    /// <param name="hasTypedHelper">是否已有非字串 typed helper 覆蓋</param>
    /// <param name="status">覆蓋狀態</param>
    public OdfTypedDomAttributeDatatypeCoverage(
        string schemaValueType,
        int schemaAttributeCount,
        string wrapperPropertyType,
        int wrapperPropertyCount,
        bool hasTypedHelper,
        string status)
    {
        SchemaValueType = schemaValueType ?? throw new ArgumentNullException(nameof(schemaValueType));
        SchemaAttributeCount = schemaAttributeCount;
        WrapperPropertyType = wrapperPropertyType ?? throw new ArgumentNullException(nameof(wrapperPropertyType));
        WrapperPropertyCount = wrapperPropertyCount;
        HasTypedHelper = hasTypedHelper;
        Status = status ?? throw new ArgumentNullException(nameof(status));
    }

    /// <summary>
    /// Gets the SchemaValueType value.
    /// 取得 schema 宣告的屬性值類型。
    /// </summary>
    public string SchemaValueType { get; }

    /// <summary>
    /// Gets the SchemaAttributeCount value.
    /// 取得使用此類型的 schema 屬性數。
    /// </summary>
    public int SchemaAttributeCount { get; }

    /// <summary>
    /// Gets the WrapperPropertyType value.
    /// 取得對應的 wrapper 屬性類型。
    /// </summary>
    public string WrapperPropertyType { get; }

    /// <summary>
    /// Gets the WrapperPropertyCount value.
    /// 取得使用此 wrapper 屬性類型的公開屬性數。
    /// </summary>
    public int WrapperPropertyCount { get; }

    /// <summary>
    /// Gets a value indicating the HasTypedHelper state.
    /// 取得是否已有非字串 typed helper 覆蓋。
    /// </summary>
    public bool HasTypedHelper { get; }

    /// <summary>
    /// Gets the Status value.
    /// 取得覆蓋狀態。
    /// </summary>
    public string Status { get; }
}
