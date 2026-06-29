using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Provides odf list level type.
/// 清單層級的類型。
/// </summary>
public enum OdfListLevelType
{
    /// <summary>
    /// 編號清單
    /// </summary>
    Number,
    /// <summary>
    /// 專案符號清單
    /// </summary>
    Bullet,
}

/// <summary>
/// Provides odf list level style.
/// 定義多層級清單樣式的單一層級設定。
/// </summary>
public sealed class OdfListLevelStyle
{
    /// <summary>
    /// Provides level.
    /// 層級（1–10）
    /// </summary>
    public int Level { get; init; } = 1;
    /// <summary>
    /// Provides type.
    /// 層級類型（編號或專案符號）
    /// </summary>
    public OdfListLevelType Type { get; init; } = OdfListLevelType.Number;
    /// <summary>
    /// Provides bullet char.
    /// 專案符號字元（僅 Bullet 類型有效）
    /// </summary>
    public string? BulletChar { get; init; }
    /// <summary>
    /// Provides num format.
    /// 編號格式（"1"、"a"、"A"、"i"、"I"）
    /// </summary>
    public string NumFormat { get; init; } = "1";
    /// <summary>
    /// Provides num prefix.
    /// 編號前綴文字
    /// </summary>
    public string? NumPrefix { get; init; }
    /// <summary>
    /// Provides num suffix.
    /// 編號後綴文字（預設為 "."）
    /// </summary>
    public string? NumSuffix { get; init; } = ".";
    /// <summary>
    /// Provides indent left.
    /// 左側縮排量
    /// </summary>
    public OdfLength IndentLeft { get; init; }
    /// <summary>
    /// Provides first line indent.
    /// 首行縮排量（負值表示懸掛縮排）
    /// </summary>
    public OdfLength FirstLineIndent { get; init; }
}
