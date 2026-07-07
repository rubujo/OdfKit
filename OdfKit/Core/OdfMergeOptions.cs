#pragma warning restore CS1591

using System;

namespace OdfKit.Core;

/// <summary>
/// Defines style conflict behavior for document append and merge operations.
/// 定義附加或合併文件時的樣式衝突處理方式。
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Renames conflicting source styles so copied content keeps its original formatting.
    /// 重新命名發生衝突的來源樣式，讓複製內容保留原始格式。
    /// </summary>
    KeepSourceFormatting,

    /// <summary>
    /// Reuses destination styles when names conflict.
    /// 樣式名稱衝突時重用目的地樣式。
    /// </summary>
    UseDestinationStyles
}

/// <summary>
/// Controls style and media handling for document append and merge operations.
/// 控制附加或合併文件時的樣式與媒體處理方式。
/// </summary>
public class OdfMergeOptions
{
    /// <summary>
    /// Gets or sets the strategy used when source and destination style names conflict.
    /// 取得或設定來源與目的地樣式名稱衝突時使用的策略。
    /// </summary>
    public ConflictResolution StyleConflictResolution { get; set; } = ConflictResolution.KeepSourceFormatting;

    /// <summary>
    /// Gets or sets a value indicating whether referenced media entries are copied into the destination.
    /// 取得或設定是否將參照的媒體專案複製到目的地文件。
    /// </summary>
    public bool CopyMedia { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether source custom styles are imported.
    /// 取得或設定是否匯入來源文件的自訂樣式。
    /// </summary>
    public bool ImportStyles { get; set; } = true;

    /// <summary>
    /// Gets a new instance with default merge settings.
    /// 取得使用預設合併設定的新執行個體。
    /// </summary>
    public static OdfMergeOptions Default => new();
}

