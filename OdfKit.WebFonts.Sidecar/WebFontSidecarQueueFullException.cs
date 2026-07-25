using OdfKit.Compliance;

namespace OdfKit.WebFonts.Sidecar;

/// <summary>
/// Represents a WebFont generation request rejected because the sidecar queue is full.
/// 表示因 Sidecar 工作佇列已滿而遭拒絕的 WebFont 產生要求。
/// </summary>
public sealed class WebFontSidecarQueueFullException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance with the localized queue-full message.
    /// 使用已在地化的佇列已滿訊息初始化執行個體。
    /// </summary>
    public WebFontSidecarQueueFullException()
        : base(OdfLocalizer.GetMessage("Err_WebFont_QueueFull"))
    {
    }
}
