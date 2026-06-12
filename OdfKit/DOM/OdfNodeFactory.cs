using System;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 根據限定名稱來具現化特定 <see cref="OdfElement"/> 子類別的工廠類別。
/// </summary>
public static partial class OdfNodeFactory
{
    /// <summary>
    /// 建立特定類型的 ODF 元素；如果無對應的特定類型，則建立通用的 <see cref="OdfElement"/>。
    /// </summary>
    /// <param name="localName">元素局部名稱</param>
    /// <param name="namespaceUri">元素命名空間 URI</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    /// <returns>所建立的 ODF 元素節點</returns>
    public static OdfNode CreateElement(string localName, string namespaceUri, string? prefix = null)
    {
        // 嘗試先使用結構定義驅動所產生的對應關係來產生元素
        var generatedElement = CreateGeneratedElement(localName, namespaceUri, prefix);
        if (generatedElement is not null)
        {
            return generatedElement;
        }

        // 後備使用通用的 OdfElement
        return new OdfElement(localName, namespaceUri, prefix);
    }
}
