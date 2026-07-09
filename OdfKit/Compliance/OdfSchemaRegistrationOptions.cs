namespace OdfKit.Compliance;

/// <summary>
/// Configures temporary schema registration in <see cref="OdfSchemaRegistry"/>.
/// 設定 <see cref="OdfSchemaRegistry"/> 的暫存結構描述註冊選項。
/// </summary>
public sealed class OdfSchemaRegistrationOptions
{
    /// <summary>
    /// Gets the default registration options (merge, do not overwrite).
    /// 取得預設註冊選項（合併既有定義，不覆寫）。
    /// </summary>
    public static OdfSchemaRegistrationOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets whether to merge with an existing schema for the same version.
    /// 取得或設定是否與同版本既有結構描述合併。
    /// </summary>
    public bool MergeWithExisting { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to overwrite existing element or attribute definitions on merge.
    /// 取得或設定合併時是否覆寫既有元素或屬性定義。
    /// </summary>
    public bool OverwriteExisting { get; set; }
}
