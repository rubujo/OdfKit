using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides APIs for odf document metadata.
/// 提供文件中繼資料的高階操作入口。
/// </summary>
public sealed class OdfDocumentMetadata
{
    private readonly OdfDocument _document;

    /// <summary>
    /// Provides odf document metadata.
    /// 初始化 <see cref="OdfDocumentMetadata"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The value to use. / 所屬文件</param>
    public OdfDocumentMetadata(OdfDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定標題。
    /// </summary>
    public string? Title
    {
        get => _document.Title;
        set => _document.Title = value;
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定作者。
    /// </summary>
    public string? Creator
    {
        get => _document.Creator;
        set => _document.Creator = value;
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定主旨。
    /// </summary>
    public string? Subject
    {
        get => _document.Subject;
        set => _document.Subject = value;
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定描述。
    /// </summary>
    public string? Description
    {
        get => _document.Description;
        set => _document.Description = value;
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文件語言（BCP-47 語言標籤，例如 "zh-TW"、"en-US"）。
    /// 對應 ODF 的 <c>dc:language</c> 元素。
    /// </summary>
    public string? Language
    {
        get => _document.Language;
        set => _document.Language = value;
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文件來源範本中繼資料。
    /// </summary>
    public OdfTemplateMetadata? TemplateMetadata
    {
        get => _document.TemplateMetadata;
        set => _document.TemplateMetadata = value;
    }
}
