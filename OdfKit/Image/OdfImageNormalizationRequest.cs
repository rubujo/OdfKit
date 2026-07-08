namespace OdfKit.Image;

/// <summary>
/// Describes a practical image compatibility normalization recommendation.
/// 描述實務影像相容性正規化建議。
/// </summary>
/// <param name="PreferredName">The normalized preferred file name. / 正規化後的偏好檔名。</param>
/// <param name="MediaType">The supplied media type. / 傳入的媒體類型。</param>
/// <param name="IsPortable">Whether the image format is portable across common ODF editors. / 影像格式是否可在常見 ODF 編輯器間可攜。</param>
/// <param name="RecommendedMediaType">The recommended portable media type, if conversion is suggested. / 建議轉換的可攜媒體類型。</param>
/// <param name="RecommendedName">The recommended portable file name, if conversion is suggested. / 建議轉換的可攜檔名。</param>
public sealed record OdfImageNormalizationRequest(
    string PreferredName,
    string? MediaType,
    bool IsPortable,
    string? RecommendedMediaType,
    string? RecommendedName);
