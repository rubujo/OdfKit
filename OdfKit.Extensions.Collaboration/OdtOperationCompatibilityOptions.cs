namespace OdfKit.Collaboration;

/// <summary>
/// Provides odt operation envelope mode.
/// 指定 ODT JSON operations 的外層封包格式。
/// </summary>
public enum OdtOperationEnvelopeMode
{
    /// <summary>
    /// 直接輸入或輸出 operations 陣列。
    /// </summary>
    BareArray,

    /// <summary>
    /// 使用 TDF ODF Toolkit 相容的 <c>{ "changes": [...] }</c> 封包。
    /// </summary>
    TdfChangesObject,
}

/// <summary>
/// Provides odt unsupported operation policy.
/// 指定遇到目前 compatibility subset 未支援的 operation 時要採取的策略。
/// </summary>
public enum OdtUnsupportedOperationPolicy
{
    /// <summary>
    /// 略過未知 operation，且不加入診斷訊息。
    /// </summary>
    Ignore,

    /// <summary>
    /// 略過未知 operation，並在 import report 中加入診斷訊息。
    /// </summary>
    RecordDiagnostic,

    /// <summary>
    /// 立即擲出 <see cref="NotSupportedException"/>。
    /// </summary>
    Throw,
}

/// <summary>
/// Provides odt operation compatibility options.
/// 控制 ODT JSON operations 與 TDF ODF Toolkit 相容子集合互通時的行為。
/// </summary>
public sealed class OdtOperationCompatibilityOptions
{
    /// <summary>
    /// Gets or sets envelope mode.
    /// 取得或設定匯出時使用的封包格式；匯入時會同時接受裸陣列與 TDF changes 封包。
    /// </summary>
    public OdtOperationEnvelopeMode EnvelopeMode { get; set; } = OdtOperationEnvelopeMode.BareArray;

    /// <summary>
    /// Gets or sets unsupported operation policy.
    /// 取得或設定遇到未支援 operation 時的處理策略。
    /// </summary>
    public OdtUnsupportedOperationPolicy UnsupportedOperationPolicy { get; set; } =
        OdtUnsupportedOperationPolicy.RecordDiagnostic;

    /// <summary>
    /// Gets or sets emit diagnostics.
    /// 取得或設定是否在 report 中輸出診斷訊息。
    /// </summary>
    public bool EmitDiagnostics { get; set; } = true;

    /// <summary>
    /// Creates create tdf compatibility.
    /// 建立以 TDF changes 封包輸出，並對未知 operation 產生診斷的預設相容設定。
    /// </summary>
    /// <returns>The result. / TDF 相容設定</returns>
    public static OdtOperationCompatibilityOptions CreateTdfCompatibility() => new()
    {
        EnvelopeMode = OdtOperationEnvelopeMode.TdfChangesObject,
        UnsupportedOperationPolicy = OdtUnsupportedOperationPolicy.RecordDiagnostic,
        EmitDiagnostics = true,
    };
}
