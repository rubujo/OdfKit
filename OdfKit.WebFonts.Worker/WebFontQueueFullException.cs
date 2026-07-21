using OdfKit.Compliance;

namespace OdfKit.WebFonts.Worker;

/// <summary>
/// Represents a rejected WebFont generation request when the bounded worker queue is full.
/// 表示 WebFont 有界 worker 佇列已滿而遭拒絕的產生要求。
/// </summary>
public sealed class WebFontQueueFullException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebFontQueueFullException"/> class.
    /// 初始化 <see cref="WebFontQueueFullException"/> 類別的新執行個體。
    /// </summary>
    public WebFontQueueFullException()
        : base(OdfLocalizer.GetMessage("Err_WebFont_QueueFull"))
    {
    }
}
