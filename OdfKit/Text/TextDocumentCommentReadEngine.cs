using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件註解讀取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentCommentReadEngine
{
    internal static IReadOnlyList<OdfCommentInfo> GetCommentInfos(OdfNode bodyTextRoot)
    {
        List<OdfCommentInfo> comments = [];
        foreach (OdfComment comment in TextDocumentCommentsEngine.GetComments(bodyTextRoot))
        {
            comments.Add(new OdfCommentInfo(
                comment.Name,
                comment.Author,
                comment.Text,
                comment.Date,
                comment.Replies.Count));
        }

        return comments.AsReadOnly();
    }
}
