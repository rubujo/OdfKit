using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides task-oriented text document operations.
/// 提供任務導向的文字文件作業。
/// </summary>
public partial class TextDocument
{
    /// <summary>
    /// Sets the text enclosed by a named bookmark.
    /// 設定具名書籤所包含的文字。
    /// </summary>
    /// <param name="bookmarkName">The exact bookmark name. / 精確書籤名稱。</param>
    /// <param name="text">The replacement text. / 取代文字。</param>
    /// <returns>The ODT mutation report. / ODT 變更報告。</returns>
    public OdtMutationReport SetBookmarkText(string bookmarkName, string? text)
    {
        var report = new OdtMutationReport("SetBookmarkText");
        if (FindBookmark(bookmarkName) is null)
        {
            report.MissingTargets.Add(bookmarkName);
            return report;
        }

        Bookmarks[bookmarkName].Value = text ?? string.Empty;
        report.UpdatedCount = 1;
        return report;
    }

    /// <summary>
    /// Sets a user, form, or inline field value by semantic identifier.
    /// 依語意識別值設定使用者欄位、表單欄位或內嵌欄位值。
    /// </summary>
    /// <param name="fieldName">The semantic field identifier. / 欄位語意識別值。</param>
    /// <param name="value">The replacement value. / 取代值。</param>
    /// <returns>The ODT mutation report. / ODT 變更報告。</returns>
    public OdtMutationReport SetFieldValue(string fieldName, string? value)
    {
        var report = new OdtMutationReport("SetFieldValue");
        if (SetUserFieldValue(fieldName, value ?? string.Empty))
            report.UpdatedCount++;
        if (FormFields.Contains(fieldName) && FormFields.TrySetValue(fieldName, value))
            report.UpdatedCount++;

        OdfTextField[] inlineFields = GetTextFields()
            .Where(field => string.Equals(field.Identifier, fieldName, StringComparison.Ordinal))
            .ToArray();
        foreach (OdfTextField field in inlineFields)
        {
            field.DisplayText = value ?? string.Empty;
            report.UpdatedCount++;
        }

        if (report.UpdatedCount == 0)
            report.MissingTargets.Add(fieldName);
        else if (report.UpdatedCount > 1)
            report.AmbiguousTargets.Add(fieldName);
        return report;
    }

    /// <summary>
    /// Replaces the binary content of the first image with an exact frame name.
    /// 取代第一個具有精確外框名稱之圖片的二進位內容。
    /// </summary>
    /// <param name="imageName">The exact image frame name. / 精確圖片外框名稱。</param>
    /// <param name="imageBytes">The replacement image bytes. / 取代圖片位元組。</param>
    /// <returns>The ODT mutation report. / ODT 變更報告。</returns>
    public OdtMutationReport ReplaceImage(string imageName, byte[] imageBytes)
    {
        if (imageBytes is null)
            throw new ArgumentNullException(nameof(imageBytes));

        var report = new OdtMutationReport("ReplaceImage");
        OdfImage[] matches = Body.Images.Items
            .Where(image => string.Equals(image.Name, imageName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            report.MissingTargets.Add(imageName);
            return report;
        }

        string packagePath = new OdfMediaManager(Package).AddImage(imageBytes, imageName);
        matches[0].ImageNode.SetAttribute("href", OdfNamespaces.XLink, packagePath, "xlink");
        report.UpdatedCount = 1;
        report.CreatedPackagePaths.Add(packagePath);
        if (matches.Length > 1)
            report.AmbiguousTargets.Add(imageName);
        return report;
    }

    /// <summary>
    /// Appends another text document and reports the added top-level content count.
    /// 附加另一份文字文件，並回報新增的最上層內容數量。
    /// </summary>
    /// <param name="otherDocument">The source text document. / 來源文字文件。</param>
    /// <returns>The ODT mutation report. / ODT 變更報告。</returns>
    public new OdtMutationReport AppendDocument(OdfDocument otherDocument) => AppendDocument(otherDocument, null);

    /// <summary>
    /// Appends another text document using typed merge options.
    /// 使用具型別合併選項附加另一份文字文件。
    /// </summary>
    /// <param name="otherDocument">The source text document. / 來源文字文件。</param>
    /// <param name="options">The typed merge options. / 具型別合併選項。</param>
    /// <returns>The ODT mutation report. / ODT 變更報告。</returns>
    public new OdtMutationReport AppendDocument(OdfDocument otherDocument, OdfMergeOptions? options)
    {
        int beforeCount = BodyTextRoot.Children.Count;
        base.AppendDocument(otherDocument, options);
        return new OdtMutationReport("AppendDocument")
        {
            UpdatedCount = Math.Max(0, BodyTextRoot.Children.Count - beforeCount),
        };
    }
}
