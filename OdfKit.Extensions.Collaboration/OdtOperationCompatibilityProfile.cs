using System;
using System.Collections.Generic;

namespace OdfKit.Collaboration;

/// <summary>
/// Exposes the audited ODF Toolkit operation compatibility surface.
/// 公開經稽核的 ODF Toolkit operation 相容範圍。
/// </summary>
public static class OdtOperationCompatibilityProfile
{
    private static readonly string[] ImportNames =
    [
        "addParagraph", "addText", "addTab", "addLineBreak", "delete", "move",
        "splitParagraph", "mergeParagraph", "addTable", "addRows", "addCells",
        "addColumn", "addColumns", "deleteColumns", "documentLayout", "addListStyle",
        "addStyle", "addFontDecl", "changeStyle", "deleteStyle", "addField",
        "updateField", "addNote", "addHeaderFooter", "deleteHeaderFooter",
        "deleteHeaderFooterContent", "addDrawing", "format",
    ];

    private static readonly string[] ExportNames =
    [
        "addParagraph", "addText", "addTab", "addLineBreak",
    ];

    /// <summary>
    /// Gets operation names accepted by the clean-room importer.
    /// 取得 clean-room 匯入器接受的 operation 名稱。
    /// </summary>
    public static IReadOnlyList<string> ImportOperations => ImportNames;

    /// <summary>
    /// Gets operation names emitted by the canonical document exporter.
    /// 取得標準文件匯出器會輸出的 operation 名稱。
    /// </summary>
    public static IReadOnlyList<string> ExportOperations => ExportNames;

    /// <summary>
    /// Determines whether the importer recognizes an operation name.
    /// 判斷匯入器是否辨識 operation 名稱。
    /// </summary>
    /// <param name="operationName">The operation name. / operation 名稱。</param>
    /// <returns>Whether the operation is recognized. / operation 是否受辨識。</returns>
    public static bool SupportsImport(string operationName) =>
        Array.IndexOf(ImportNames, operationName) >= 0;

    /// <summary>
    /// Determines whether the canonical exporter emits an operation name.
    /// 判斷標準匯出器是否會輸出 operation 名稱。
    /// </summary>
    /// <param name="operationName">The operation name. / operation 名稱。</param>
    /// <returns>Whether the operation may be emitted. / operation 是否可能輸出。</returns>
    public static bool SupportsExport(string operationName) =>
        Array.IndexOf(ExportNames, operationName) >= 0;
}
