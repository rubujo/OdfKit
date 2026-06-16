using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件中的註解。
/// </summary>
public class OdfComment
{
    /// <summary>
    /// 取得或設定註解的作者。
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// 取得或設定註解的日期與時間。
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 取得或設定註解的內文。
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// 取得唯一識別碼。此識別碼在 ODF 1.3 中用於參考上層註解。
    /// </summary>
    public string Name { get; }

    private readonly List<OdfComment> _replies = [];

    /// <summary>
    /// 取得此註解的回覆列表。
    /// </summary>
    public IReadOnlyList<OdfComment> Replies => _replies;

    /// <summary>
    /// 初始化 <see cref="OdfComment"/> 類別的新執行個體。
    /// </summary>
    /// <param name="author">註解的作者</param>
    /// <param name="text">註解的內文</param>
    public OdfComment(string author, string text)
        : this(author, text, DateTime.UtcNow, Guid.NewGuid().ToString("N"))
    {
    }

    /// <summary>
    /// 初始化 <see cref="OdfComment"/> 類別的新執行個體。
    /// </summary>
    /// <param name="author">註解的作者</param>
    /// <param name="text">註解的內文</param>
    /// <param name="date">註解的日期</param>
    /// <param name="name">註解的唯一名稱</param>
    public OdfComment(string author, string text, DateTime date, string name)
    {
        Author = author ?? throw new ArgumentNullException(nameof(author));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Date = date;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 新增回覆至此註解。
    /// </summary>
    /// <param name="author">回覆的作者</param>
    /// <param name="text">回覆的內文</param>
    public void AddReply(string author, string text)
    {
        var reply = new OdfComment(author, text, DateTime.UtcNow, Guid.NewGuid().ToString("N"));
        _replies.Add(reply);
    }

    /// <summary>
    /// 新增回覆至此註解。
    /// </summary>
    /// <param name="reply">要新增的回覆註解執行個體</param>
    public void AddReply(OdfComment reply)
    {
        if (reply is null)
            throw new ArgumentNullException(nameof(reply));
        _replies.Add(reply);
    }

    private struct CommentStackFrame(OdfComment comment, string? parentName, bool isExit)
    {
        public OdfComment Comment { get; } = comment;
        public string? ParentName { get; } = parentName;
        public bool IsExit { get; } = isExit;
    }

    /// <summary>
    /// 將此註解及其回覆遞迴呈現為標準的 ODF 1.3 XML 扁平同級辦公室註解節點。
    /// </summary>
    /// <returns>包含註解 XML 結構的 <see cref="OdfNode"/> 執行個體</returns>
    public OdfNode ToXmlNode()
    {
        if (_replies.Count == 0)
        {
            var annotationNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            annotationNode.SetAttribute("name", OdfNamespaces.Office, Name, "office");

            // 建立者項目
            var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
            creator.TextContent = Author;
            annotationNode.AppendChild(creator);

            // 日期項目
            var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
            dateNode.TextContent = Date.ToString("yyyy-MM-ddTHH:mm:ssZ");
            annotationNode.AppendChild(dateNode);

            // 代表文字的段落
            var paragraphs = Text.Split(["\r\n", "\n"], StringSplitOptions.None);
            foreach (var pText in paragraphs)
            {
                var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                pNode.TextContent = pText;
                annotationNode.AppendChild(pNode);
            }

            return annotationNode;
        }

        var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);
        var activePath = new HashSet<OdfComment>();
        var serializedNames = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<CommentStackFrame>();

        stack.Push(new CommentStackFrame(this, null, false));

        while (stack.Count > 0)
        {
            var frame = stack.Pop();
            if (frame.IsExit)
            {
                activePath.Remove(frame.Comment);
                continue;
            }

            if (activePath.Contains(frame.Comment))
            {
                throw new InvalidOperationException("Circular reference detected in OdfComment replies.");
            }

            if (!serializedNames.Add(frame.Comment.Name))
            {
                continue;
            }

            activePath.Add(frame.Comment);
            stack.Push(new CommentStackFrame(frame.Comment, frame.ParentName, true));

            var annotationNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            annotationNode.SetAttribute("name", OdfNamespaces.Office, frame.Comment.Name, "office");
            if (frame.ParentName is not null)
            {
                annotationNode.SetAttribute("annotation-parent", OdfNamespaces.Office, frame.ParentName, "office");
            }

            // 建立者項目
            var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
            creator.TextContent = frame.Comment.Author;
            annotationNode.AppendChild(creator);

            // 日期項目
            var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
            dateNode.TextContent = frame.Comment.Date.ToString("yyyy-MM-ddTHH:mm:ssZ");
            annotationNode.AppendChild(dateNode);

            // 代表文字的段落
            var paragraphs = frame.Comment.Text.Split(["\r\n", "\n"], StringSplitOptions.None);
            foreach (var pText in paragraphs)
            {
                var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                pNode.TextContent = pText;
                annotationNode.AppendChild(pNode);
            }

            container.AppendChild(annotationNode);

            for (int i = frame.Comment._replies.Count - 1; i >= 0; i--)
            {
                stack.Push(new CommentStackFrame(frame.Comment._replies[i], frame.Comment.Name, false));
            }
        }

        return container;
    }

    private static OdfComment FromXmlNodeSingle(OdfNode node, string uniqueName)
    {
        if (node.LocalName != "annotation" || node.NamespaceUri != OdfNamespaces.Office)
        {
            throw new ArgumentException("Provided node is not a valid ODF office:annotation.");
        }

        string author = "Unknown";
        DateTime date = DateTime.UtcNow;
        string text = string.Empty;

        foreach (var child in node.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Dc)
            {
                if (child.LocalName == "creator")
                    author = child.TextContent;
                else if (child.LocalName == "date" && DateTime.TryParse(child.TextContent, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    if (dt == DateTime.MinValue || dt == DateTime.MaxValue)
                    {
                        date = dt;
                    }
                    else
                    {
                        try
                        {
                            date = dt.ToUniversalTime();
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            date = dt;
                        }
                    }
                }
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "p")
            {
                if (string.IsNullOrEmpty(text))
                    text = child.TextContent;
                else
                    text += "\n" + child.TextContent;
            }
        }

        return new OdfComment(author, text, date, uniqueName);
    }

    /// <summary>
    /// 將標準的 ODF 1.3 XML 扁平同級項目還原解析為 <see cref="OdfComment"/> 物件樹的輔助方法。
    /// </summary>
    /// <param name="node">要解析的 <see cref="OdfNode"/> 節點</param>
    /// <returns>解析後的 <see cref="OdfComment"/> 根註解物件</returns>
    public static OdfComment FromXmlNode(OdfNode node)
    {
        if (node is null)
            throw new ArgumentNullException(nameof(node));

        // 識別所有候選 XML 節點
        IEnumerable<OdfNode> xmlNodes;
        if (node.LocalName == "annotation-list")
        {
            xmlNodes = node.Children;
        }
        else if (node.Parent is not null)
        {
            xmlNodes = node.Parent.Children;
        }
        else
        {
            xmlNodes = [node];
        }

        var commentsMap = new Dictionary<string, OdfComment>(StringComparer.Ordinal);
        var parentMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var mostRecentParent = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var parsedCommentsList = new List<OdfComment>();
        OdfComment? targetComment = null;
        OdfComment? rootComment = null;

        foreach (var child in xmlNodes)
        {
            if (child.LocalName == "annotation" && child.NamespaceUri == OdfNamespaces.Office)
            {
                string originalName = child.GetAttribute("name", OdfNamespaces.Office) ?? Guid.NewGuid().ToString("N");

                // 產生唯一名稱
                string uniqueName = originalName;
                int counter = 1;
                while (seenNames.Contains(uniqueName))
                {
                    uniqueName = $"{originalName}_{counter++}";
                }
                seenNames.Add(uniqueName);

                // 解析單一註解
                var comment = FromXmlNodeSingle(child, uniqueName);
                commentsMap[uniqueName] = comment;
                parsedCommentsList.Add(comment);

                // 如果這是傳遞給 FromXmlNode 的節點，記住其對應的解析結果
                if (child == node)
                {
                    targetComment = comment;
                }

                // 尋找上層註解
                string? parentOriginalName = child.GetAttribute("annotation-parent", OdfNamespaces.Office);
                if (!string.IsNullOrEmpty(parentOriginalName))
                {
                    if (mostRecentParent.TryGetValue(parentOriginalName!, out string? parentUniqueName))
                    {
                        parentMap[uniqueName] = parentUniqueName;
                    }
                    else
                    {
                        parentMap[uniqueName] = parentOriginalName!;
                    }
                }
                else if (rootComment is null)
                {
                    rootComment = comment;
                }

                // 更新最近的上層對照表
                mostRecentParent[originalName] = uniqueName;
            }
        }

        // 如果找不到 rootComment（例如所有註解都有上層註解），則遞補使用第一個解析的註解
        if (rootComment is null && commentsMap.Count > 0)
        {
            rootComment = parsedCommentsList[0];
        }

        // 依原始出現順序連結上層註解與回覆
        foreach (var comment in parsedCommentsList)
        {
            if (parentMap.TryGetValue(comment.Name, out string? parentUniqueName))
            {
                if (commentsMap.TryGetValue(parentUniqueName, out var parentComment))
                {
                    parentComment.AddReply(comment);
                }
            }
        }

        if (node.LocalName == "annotation-list")
        {
            return rootComment ?? throw new ArgumentException("No valid office:annotation elements found in container.");
        }
        else
        {
            return targetComment ?? rootComment ?? throw new ArgumentException("No valid office:annotation elements found in container.");
        }
    }
}
