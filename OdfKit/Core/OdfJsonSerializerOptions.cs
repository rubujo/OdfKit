using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OdfKit.Core;

/// <summary>
/// 提供專案共用的 <see cref="JsonSerializerOptions"/> 預設組態。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Text.Json.JsonSerializer"/> 預設編碼器會將非 ASCII 字元（含中文）跳脫為 <c>\uXXXX</c>，
/// 不利於終端機與人工閱讀。本類別統一採用 <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>，
/// 保留 UTF-8 字面量，並僅跳脫 JSON 語法所需字元。
/// </para>
/// <para>
/// 程式庫 <see cref="OdfKit.Compliance.OdfValidationReport.ToJson"/> 使用手寫序列化以維持穩定欄位順序，
/// 其 <c>EscapeJson</c> 語意與本類別之編碼器策略一致。
/// </para>
/// </remarks>
public static class OdfJsonSerializerOptions
{
    /// <summary>
    /// 供 CLI 與人工閱讀使用的縮排 JSON 組態。
    /// </summary>
    public static JsonSerializerOptions HumanReadable { get; } = CreateBase(writeIndented: true);

    /// <summary>
    /// 供 corpus manifest 等檔案持久化使用的組態（camelCase 屬性名稱 + 縮排）。
    /// </summary>
    public static JsonSerializerOptions Manifest { get; } = CreateManifest();

    /// <summary>
    /// 供測試或機器消費使用的單行 JSON 組態。
    /// </summary>
    public static JsonSerializerOptions Compact { get; } = CreateBase(writeIndented: false);

    private static JsonSerializerOptions CreateBase(bool writeIndented)
    {
        return new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = writeIndented,
        };
    }

    private static JsonSerializerOptions CreateManifest()
    {
        return new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
    }
}
