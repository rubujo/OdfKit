using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 自追蹤修訂規格節點讀取作者與時間中繼資料。
/// </summary>
internal static class OdfTrackedChangeMetadataReader
{
    /// <summary>
    /// 讀取修訂作者與時間，支援 <c>office:change-info</c> 與 LibreOffice 慣用之內嵌屬性。
    /// </summary>
    /// <param name="specNode">insertion／deletion／format-change 節點。</param>
    /// <returns>作者與 UTC 時間。</returns>
    internal static (string Author, DateTime ChangedAt) Read(OdfNode? specNode)
    {
        if (specNode is null)
        {
            return (string.Empty, DateTime.MinValue);
        }

        string author = specNode.GetAttribute("change-author", OdfNamespaces.Text) ?? string.Empty;
        string? dateText = specNode.GetAttribute("change-date-and-time", OdfNamespaces.Text);

        OdfNode? changeInfo = TextDocumentDomHelper.FindChildElement(specNode, "change-info", OdfNamespaces.Office);
        if (changeInfo is not null)
        {
            if (string.IsNullOrEmpty(author))
            {
                OdfNode? creatorNode = TextDocumentDomHelper.FindChildElement(changeInfo, "creator", OdfNamespaces.Dc);
                if (creatorNode is not null)
                {
                    author = creatorNode.TextContent ?? string.Empty;
                }
            }

            if (string.IsNullOrEmpty(dateText))
            {
                OdfNode? dateNode = TextDocumentDomHelper.FindChildElement(changeInfo, "date", OdfNamespaces.Dc);
                dateText = dateNode?.TextContent;
            }
        }

        return (author, ParseChangeDate(dateText));
    }

    /// <summary>
    /// 將修訂時間格式化為 ODF 日期字串。
    /// </summary>
    /// <param name="date">修訂時間。</param>
    /// <returns>ODF 日期字串。</returns>
    internal static string FormatChangeDate(DateTime date)
    {
        if (date == DateTime.MinValue)
        {
            return "0001-01-01T00:00:00Z";
        }

        if (date == DateTime.MaxValue)
        {
            return "9999-12-31T23:59:59.9999999Z";
        }

        return date.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 解析 ODF 修訂日期字串。
    /// </summary>
    /// <param name="textContent">日期文字。</param>
    /// <returns>UTC 時間；無法解析時回傳 <see cref="DateTime.MinValue"/>。</returns>
    internal static DateTime ParseChangeDate(string? textContent)
    {
        if (string.IsNullOrEmpty(textContent))
        {
            return DateTime.MinValue;
        }

        string value = textContent!;
        if (value == "0001-01-01T00:00:00Z" || value.StartsWith("0001-01-01", StringComparison.Ordinal))
        {
            return DateTime.MinValue;
        }

        if (value == "9999-12-31T23:59:59.9999999Z" || value.StartsWith("9999-12-31", StringComparison.Ordinal))
        {
            return DateTime.MaxValue;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsedDate)
            ? parsedDate
            : DateTime.MinValue;
    }

    /// <summary>
    /// 將作者與時間寫入修訂規格節點（內嵌屬性 + <c>office:change-info</c>）。
    /// </summary>
    /// <param name="typeNode">insertion／deletion／format-change 節點。</param>
    /// <param name="creator">作者。</param>
    /// <param name="date">修訂時間。</param>
    internal static void Write(OdfNode typeNode, string creator, DateTime date)
    {
        string dateText = FormatChangeDate(date);
        typeNode.SetAttribute("change-author", OdfNamespaces.Text, creator, "text");
        typeNode.SetAttribute("change-date-and-time", OdfNamespaces.Text, dateText, "text");

        var changeInfo = new OdfNode(OdfNodeType.Element, "change-info", OdfNamespaces.Office, "office");
        typeNode.AppendChild(changeInfo);

        var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
        creatorNode.TextContent = creator;
        changeInfo.AppendChild(creatorNode);

        var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        dateNode.TextContent = dateText;
        changeInfo.AppendChild(dateNode);
    }
}
