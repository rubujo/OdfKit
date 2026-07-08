namespace OdfKit.Database;

/// <summary>
/// Describes one practical database query parameter.
/// 描述一個實務資料庫查詢參數。
/// </summary>
/// <param name="Name">The parameter name. / 參數名稱。</param>
/// <param name="Type">The parameter type hint. / 參數型別提示。</param>
/// <param name="Description">The optional parameter description. / 選用參數描述。</param>
public sealed record OdfDatabaseQueryParameter(
    string Name,
    string Type,
    string? Description = null);
