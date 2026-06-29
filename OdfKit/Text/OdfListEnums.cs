using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Represents the type of a list level.
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
/// Defines a single level configuration of a multi-level list style.
/// 定義多層級清單樣式的單一層級設定。
/// </summary>
public sealed class OdfListLevelStyle
{
    /// <summary>
    /// Gets the level (1–10).
    /// 層級（1–10）
    /// </summary>
    public int Level { get; init; } = 1;
    /// <summary>
    /// Gets the level type (number or bullet).
    /// 層級類型（編號或專案符號）
    /// </summary>
    public OdfListLevelType Type { get; init; } = OdfListLevelType.Number;
    /// <summary>
    /// Gets the bullet character (valid only for the Bullet type).
    /// 專案符號字元（僅 Bullet 類型有效）
    /// </summary>
    public string? BulletChar { get; init; }
    /// <summary>
    /// Gets the number format ("1", "a", "A", "i", "I").
    /// 編號格式（"1"、"a"、"A"、"i"、"I"）
    /// </summary>
    public string NumFormat { get; init; } = "1";
    /// <summary>
    /// Gets the number prefix text.
    /// 編號前綴文字
    /// </summary>
    public string? NumPrefix { get; init; }
    /// <summary>
    /// Gets the number suffix text (defaults to ".").
    /// 編號後綴文字（預設為 "."）
    /// </summary>
    public string? NumSuffix { get; init; } = ".";
    /// <summary>
    /// Gets the left indent amount.
    /// 左側縮排量
    /// </summary>
    public OdfLength IndentLeft { get; init; }
    /// <summary>
    /// Gets the first line indent amount (a negative value indicates a hanging indent).
    /// 首行縮排量（負值表示懸掛縮排）
    /// </summary>
    public OdfLength FirstLineIndent { get; init; }
}
