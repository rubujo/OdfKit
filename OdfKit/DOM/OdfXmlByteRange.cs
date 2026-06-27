namespace OdfKit.DOM;

/// <summary>
/// 表示節點在來源 UTF-8 XML 緩衝區中的位元組範圍。
/// </summary>
/// <param name="ElementOffset">完整元素起始位元組位置</param>
/// <param name="ElementLength">完整元素位元組長度，包含開始與結束標籤</param>
/// <param name="InnerOffset">元素內容起始位元組位置，不包含開始標籤</param>
/// <param name="InnerLength">元素內容位元組長度，不包含結束標籤</param>
public readonly record struct OdfXmlByteRange(
    int ElementOffset,
    int ElementLength,
    int InnerOffset,
    int InnerLength);
