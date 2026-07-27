using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents rich text content for an ODS cell, composed of multiple <see cref="OdfRichTextRun"/> instances.
/// 代表 ODS 儲存格的富文字內容，由多個 <see cref="OdfRichTextRun"/> 組成。
/// </summary>
public sealed class OdfRichText
{
    private readonly List<OdfRichTextRun> _runs = new();

    /// <summary>
    /// Gets all formatted runs.
    /// 取得所有格式片段。
    /// </summary>
    public IReadOnlyList<OdfRichTextRun> Runs => _runs;

    /// <summary>
    /// Clears all formatted runs.
    /// 清除所有格式片段。
    /// </summary>
    public void Clear() => _runs.Clear();

    /// <summary>
    /// Adds a plain text run with default formatting.
    /// 以預設格式新增純文字片段。
    /// </summary>
    /// <param name="text">The run text. / 片段文字。</param>
    /// <returns>The current rich text object for chaining. / 目前富文字物件，方便鏈式呼叫。</returns>
    public OdfRichText AddRun(string text) => AddRun(text, OdfRichTextRunOptions.Default);

    /// <summary>
    /// Adds a formatted run using an options object.
    /// 以 options 物件新增格式片段。
    /// </summary>
    /// <param name="text">The run text. / 片段文字。</param>
    /// <param name="options">The run formatting options. / 片段格式選項。</param>
    /// <returns>The current rich text object for chaining. / 目前富文字物件，方便鏈式呼叫。</returns>
    public OdfRichText AddRun(string text, OdfRichTextRunOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(text, nameof(text));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));

        _runs.Add(new OdfRichTextRun
        {
            Text = text,
            Bold = options.Bold,
            Italic = options.Italic,
            Underline = options.Underline,
            Color = options.Color,
            FontFamily = options.FontFamily,
        });

        return this;
    }

    /// <summary>
    /// Adds a line break run.
    /// 新增一個換行片段。
    /// </summary>
    /// <returns>The current rich text object for chaining. / 目前富文字物件，方便鏈式呼叫。</returns>
    public OdfRichText AddLineBreak()
    {
        _runs.Add(new OdfRichTextRun { Text = "\n" });
        return this;
    }
}
