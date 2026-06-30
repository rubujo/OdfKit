namespace OdfKit.Collaboration;

/// <summary>
/// Defines the replay status of a single ODT JSON operation.
/// 定義單一 ODT JSON operation 的重播狀態。
/// </summary>
public enum OdtOperationReplayStatus
{
    /// <summary>
    /// The operation was replayed.
    /// operation 已重播。
    /// </summary>
    Replayed,

    /// <summary>
    /// The operation was ignored because it only carries metadata for this compatibility layer.
    /// operation 因僅承載此相容層的中介資料而被略過。
    /// </summary>
    Ignored,

    /// <summary>
    /// The operation is unsupported or outside the safety limits.
    /// operation 不受支援或超出安全限制。
    /// </summary>
    Unsupported,
}

/// <summary>
/// Describes the replay result of a single ODT JSON operation.
/// 描述單一 ODT JSON operation 的重播結果。
/// </summary>
public sealed class OdtOperationReportEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdtOperationReportEntry"/> class.
    /// 初始化 <see cref="OdtOperationReportEntry"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sourceIndex">The zero-based source index. / 以零為基底的來源索引。</param>
    /// <param name="operationName">The operation name. / operation 名稱。</param>
    /// <param name="status">The replay status. / 重播狀態。</param>
    /// <param name="messageKey">The localized message key. / 在地化訊息鍵值。</param>
    /// <param name="message">The localized message. / 在地化訊息。</param>
    /// <param name="reason">The diagnostic reason. / 診斷原因。</param>
    public OdtOperationReportEntry(
        int sourceIndex,
        string? operationName,
        OdtOperationReplayStatus status,
        string? messageKey,
        string? message,
        string? reason)
    {
        SourceIndex = sourceIndex;
        OperationName = operationName;
        Status = status;
        MessageKey = messageKey;
        Message = message;
        Reason = reason;
    }

    /// <summary>
    /// Gets the zero-based source index.
    /// 取得以零為基底的來源索引。
    /// </summary>
    public int SourceIndex { get; }

    /// <summary>
    /// Gets the operation name.
    /// 取得 operation 名稱。
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets the replay status.
    /// 取得重播狀態。
    /// </summary>
    public OdtOperationReplayStatus Status { get; }

    /// <summary>
    /// Gets the localized message key.
    /// 取得在地化訊息鍵值。
    /// </summary>
    public string? MessageKey { get; }

    /// <summary>
    /// Gets the localized message.
    /// 取得在地化訊息。
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the diagnostic reason.
    /// 取得診斷原因。
    /// </summary>
    public string? Reason { get; }
}
