using System;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    /// <summary>
    /// 依名稱取得可編輯的投影片母片。
    /// </summary>
    /// <param name="name">母片名稱</param>
    /// <returns>對應的母片物件</returns>
    /// <exception cref="ArgumentException">找不到指定名稱的母片時擲出</exception>
    public OdfMasterPage GetMasterPage(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_NameCannotBeEmpty"), nameof(name));

        foreach (OdfMasterPage masterPage in GetMasterPages())
        {
            if (string.Equals(masterPage.Name, name, StringComparison.Ordinal))
                return masterPage;
        }

        throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_MasterNotFound", name), nameof(name));
    }
}
