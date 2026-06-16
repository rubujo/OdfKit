using System;

namespace OdfKit.Compliance;

/// <summary>
/// 描述 ODF 結構描述中已知的一個屬性定義。
/// </summary>
/// <param name="name">限定名稱</param>
/// <param name="valueType">值類型</param>
/// <param name="supportedVersions">支援的 ODF 版本範圍</param>
/// <param name="isRequiredOnDocumentRoot">是否在文件根元素上為必要</param>
public sealed class OdfAttributeDefinition(
    OdfQualifiedName name,
    string valueType,
    OdfVersionRange supportedVersions,
    bool isRequiredOnDocumentRoot = false)
{
    /// <summary>
    /// 取得命名空間限定的屬性名稱。
    /// </summary>
    public OdfQualifiedName Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// 取得結構描述值類型名稱。
    /// </summary>
    public string ValueType { get; } = valueType ?? throw new ArgumentNullException(nameof(valueType));

    /// <summary>
    /// 取得支援此屬性的 ODF 版本範圍。
    /// </summary>
    public OdfVersionRange SupportedVersions { get; } = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));

    /// <summary>
    /// 取得一個值，表示此屬性在 ODF 文件根元素上是否為必要。
    /// </summary>
    public bool IsRequiredOnDocumentRoot { get; } = isRequiredOnDocumentRoot;
}

