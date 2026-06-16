using System;

namespace OdfKit.Compliance;

/// <summary>
/// 描述 ODF 結構描述中已知的一個元素定義。
/// </summary>
/// <param name="name">限定名稱</param>
/// <param name="role">元素角色</param>
/// <param name="supportedVersions">支援的 ODF 版本範圍</param>
/// <param name="documentKind">文件種類</param>
public sealed class OdfElementDefinition(
    OdfQualifiedName name,
    OdfSchemaElementRole role,
    OdfVersionRange supportedVersions,
    OdfDocumentKind documentKind = OdfDocumentKind.Unknown)
{
    /// <summary>
    /// 取得命名空間限定的元素名稱。
    /// </summary>
    public OdfQualifiedName Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// 取得此元素的結構描述角色。
    /// </summary>
    public OdfSchemaElementRole Role { get; } = role;

    /// <summary>
    /// 取得支援此元素的 ODF 版本範圍。
    /// </summary>
    public OdfVersionRange SupportedVersions { get; } = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));

    /// <summary>
    /// 取得此元素所代表的文件種類（適用時）。
    /// </summary>
    public OdfDocumentKind DocumentKind { get; } = documentKind;
}

