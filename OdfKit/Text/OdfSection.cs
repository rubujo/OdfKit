using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Represents odf section.
/// 表示文字文件中的多欄版面配置區段。
/// </summary>
public class OdfSection
{
    internal OdfSection(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此區段相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定此區段的書寫模式。
    /// </summary>
    public string? WritingMode
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "writing-mode", OdfNamespaces.Style, "section");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "section", "section-properties", "writing-mode", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定此區段是否受保護（對應 <c>text:protected</c>）。
    /// </summary>
    /// <remarks>
    /// 用於將範本中特定區段標記為唯讀，防止使用者在套用範本後誤改其內容；實際強制力取決於
    /// 使用者端應用程式（例如 LibreOffice）是否遵循此標記，OdfKit 本身不會阻擋對受保護區段的程式化修改。
    /// </remarks>
    public bool IsProtected
    {
        get => Node.GetAttribute("protected", OdfNamespaces.Text) == "true";
        set => Node.SetAttribute("protected", OdfNamespaces.Text, value ? "true" : "false", "text");
    }

    /// <summary>
    /// Provides protect.
    /// 以指定密碼保護此區段。
    /// </summary>
    /// <param name="password">The value to use. / 密碼明文</param>
    public void Protect(string password)
    {
        OdfKit.Core.OdfProtectionHelper.ProtectNode(Node, password, "text", OdfNamespaces.Text);
    }

    /// <summary>
    /// Provides unprotect.
    /// 解除此區段的密碼保護。
    /// </summary>
    public void Unprotect()
    {
        OdfKit.Core.OdfProtectionHelper.UnprotectNode(Node, OdfNamespaces.Text);
    }

    /// <summary>
    /// Attempts to process try unprotect.
    /// 嘗試以指定密碼解除此區段的保護。
    /// </summary>
    /// <param name="password">The value to use. / 密碼明文</param>
    /// <returns>The result. / 若解除成功則為 true，否則為 false</returns>
    public bool TryUnprotect(string password)
    {
        if (!IsProtected)
            return true;
        if (OdfKit.Core.OdfProtectionHelper.VerifyPassword(Node, password, OdfNamespaces.Text))
        {
            Unprotect();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Provides verify password.
    /// 驗證指定密碼是否能成功解鎖此區段。
    /// </summary>
    /// <param name="password">The value to use. / 密碼明文</param>
    /// <returns>The result. / 若密碼正確或區段未受保護則為 true，否則為 false</returns>
    public bool VerifyPassword(string password)
    {
        if (!IsProtected)
            return true;
        return OdfKit.Core.OdfProtectionHelper.VerifyPassword(Node, password, OdfNamespaces.Text);
    }

    private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
}
