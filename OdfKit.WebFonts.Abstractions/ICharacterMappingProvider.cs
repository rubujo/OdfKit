namespace OdfKit.WebFonts;

/// <summary>
/// Converts a declared legacy byte encoding into Unicode text without guessing.
/// 在不猜測編碼的情況下，將明確宣告的舊式位元組編碼轉換為 Unicode 文字。
/// </summary>
public interface ICharacterMappingProvider
{
    /// <summary>
    /// Gets the stable mapping profile identifier and version.
    /// 取得穩定的 mapping profile 識別碼與版本。
    /// </summary>
    string ProfileId { get; }

    /// <summary>
    /// Decodes bytes and rejects unmapped or malformed input.
    /// 解碼位元組，並拒絕未對應或格式錯誤的輸入。
    /// </summary>
    /// <param name="source">The encoded source bytes. / 已編碼的來源位元組。</param>
    /// <returns>Strictly decoded Unicode text. / 嚴格解碼後的 Unicode 文字。</returns>
    string Decode(byte[] source);
}
