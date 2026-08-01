using System.Data.Common;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.Data.SqlServer;

/// <summary>
/// Reads SQL Server Unicode values or explicitly encoded legacy bytes without guessing an encoding.
/// 讀取 SQL Server Unicode 值或明確編碼的舊式位元組，且不猜測編碼。
/// </summary>
public static class SqlServerWebFontTextReader
{
    private const int DefaultMaxLegacyBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Reads an nchar, nvarchar, ntext, varchar, or text value already decoded by the data provider.
    /// 讀取已由資料 provider 解碼的 nchar、nvarchar、ntext、varchar 或 text 值。
    /// </summary>
    /// <param name="reader">The positioned data reader. / 已定位的資料 reader。</param>
    /// <param name="ordinal">The zero-based column ordinal. / 以零為基準的欄位序號。</param>
    /// <returns>A validated Unicode sequence. / 驗證後的 Unicode 序列。</returns>
    /// <remarks>
    /// This method cannot recover data already lost during a non-Unicode SQL code-page conversion.
    /// 此方法無法復原已在非 Unicode SQL code page 轉換期間遺失的資料。
    /// </remarks>
    public static WebFontTextSequence ReadProviderDecodedText(DbDataReader reader, int ordinal)
    {
        ValidateReader(reader, ordinal);
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return WebFontTextSequence.Create(reader.GetString(ordinal));
    }

    /// <summary>
    /// Reads a varbinary value and decodes it through an explicit mapping provider.
    /// 讀取 varbinary 值，並透過明確的 mapping provider 解碼。
    /// </summary>
    /// <param name="reader">The positioned data reader. / 已定位的資料 reader。</param>
    /// <param name="ordinal">The zero-based column ordinal. / 以零為基準的欄位序號。</param>
    /// <param name="mappingProvider">The declared legacy mapping provider. / 明確宣告的舊式 mapping provider。</param>
    /// <returns>A validated Unicode sequence. / 驗證後的 Unicode 序列。</returns>
    public static WebFontTextSequence ReadLegacyBytes(
        DbDataReader reader,
        int ordinal,
        ICharacterMappingProvider mappingProvider)
        => ReadLegacyBytes(reader, ordinal, mappingProvider, DefaultMaxLegacyBytes);

    /// <summary>
    /// Reads a bounded varbinary value and decodes it through an explicit mapping provider.
    /// 讀取有界的 varbinary 值，並透過明確的 mapping provider 解碼。
    /// </summary>
    /// <param name="reader">The positioned data reader. / 已定位的資料 reader。</param>
    /// <param name="ordinal">The zero-based column ordinal. / 以零為基準的欄位序號。</param>
    /// <param name="mappingProvider">The declared legacy mapping provider. / 明確宣告的舊式 mapping provider。</param>
    /// <param name="maxBytes">The hard byte limit. / 位元組硬性上限。</param>
    /// <returns>A validated Unicode sequence. / 驗證後的 Unicode 序列。</returns>
    public static WebFontTextSequence ReadLegacyBytes(
        DbDataReader reader,
        int ordinal,
        ICharacterMappingProvider mappingProvider,
        int maxBytes)
    {
        ValidateReader(reader, ordinal);
        if (mappingProvider is null || maxBytes <= 0 || reader.IsDBNull(ordinal))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        long length = reader.GetBytes(ordinal, 0, null, 0, 0);
        if (length <= 0 || length > maxBytes || length > int.MaxValue)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        var bytes = new byte[(int)length];
        int totalRead = 0;
        while (totalRead < bytes.Length)
        {
            long read = reader.GetBytes(
                ordinal,
                totalRead,
                bytes,
                totalRead,
                bytes.Length - totalRead);
            if (read <= 0 || read > bytes.Length - totalRead)
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            totalRead = checked(totalRead + (int)read);
        }

        return WebFontTextSequence.Create(mappingProvider.Decode(bytes));
    }

    private static void ValidateReader(DbDataReader reader, int ordinal)
    {
        if (reader is null || reader.IsClosed || ordinal < 0 || ordinal >= reader.FieldCount)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }
    }
}
