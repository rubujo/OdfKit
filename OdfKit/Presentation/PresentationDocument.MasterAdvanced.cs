using System;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    /// <summary>
    /// 依名稱取得可編輯的投影片母片。
    /// </summary>
    /// <param name="name">母片名稱。</param>
    /// <returns>對應的母片物件。</returns>
    /// <exception cref="ArgumentException">找不到指定名稱的母片時擲出。</exception>
    public OdfMasterPage GetMasterPage(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("母片名稱不可為空白。", nameof(name));

        foreach (OdfMasterPage masterPage in GetMasterPages())
        {
            if (string.Equals(masterPage.Name, name, StringComparison.Ordinal))
                return masterPage;
        }

        throw new ArgumentException($"找不到母片「{name}」。", nameof(name));
    }
}
