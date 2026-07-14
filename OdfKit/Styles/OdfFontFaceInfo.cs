namespace OdfKit.Styles;

/// <summary>
/// Describes an ODF font-face declaration.
/// 描述 ODF font-face 宣告的資訊。
/// </summary>
/// <param name="Name">The style:name value of the font-face declaration. / font-face 宣告的 style:name 值。</param>
/// <param name="Family">The concrete font family name. / 實際字型家族名稱。</param>
/// <param name="GenericFamily">The optional generic font family (for example "system-serif"). / 選用的泛用字型家族（例如 "system-serif"）。</param>
/// <param name="Pitch">The optional font pitch (for example "variable" or "fixed"). / 選用的字距類型（例如 "variable" 或 "fixed"）。</param>
public sealed record OdfFontFaceInfo(
    string Name,
    string Family,
    string? GenericFamily,
    string? Pitch);
