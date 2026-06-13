#pragma warning restore CS1591

using System;

namespace OdfKit.Core;

/// <summary>
/// 指定在附加或合併文件時，如何解決樣式名稱的衝突。
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// 衝突的來源文件樣式會被重新命名（例如 MyStyle_s1 ），並複製以保持來源文件格式的獨立性。
    /// </summary>
    KeepSourceFormatting,

    /// <summary>
    /// 捨棄衝突的樣式。複製的來源節點將直接參考目的地的樣式，以符合目的地的佈景主題。
    /// </summary>
    UseDestinationStyles
}

/// <summary>
/// 合併或附加文件時的組態選項。
/// </summary>
public class OdfMergeOptions
{
    /// <summary>
    /// 取得或設定樣式衝突的解決策略。
    /// </summary>
    public ConflictResolution StyleConflictResolution { get; set; } = ConflictResolution.KeepSourceFormatting;

    /// <summary>
    /// 取得或設定一個值，指出是否應複製並遷移參考的媒體或圖片。
    /// </summary>
    public bool CopyMedia { get; set; } = true;

    /// <summary>
    /// 取得或設定一個值，指出是否應匯入自訂樣式。
    /// </summary>
    public bool ImportStyles { get; set; } = true;

    /// <summary>
    /// 取得預設的合併選項組態。
    /// </summary>
    public static OdfMergeOptions Default => new();
}

