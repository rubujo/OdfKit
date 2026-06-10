using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Core;

namespace OdfKit.Text
{
    public class OdfComment
    {
        public string Author { get; set; }
        public DateTime Date { get; set; }
        public string Text { get; set; }
        public string Name { get; } // Unique Identifier used in ODF 1.3 to reference parent
        
        private readonly List<OdfComment> _replies = new();
        public IReadOnlyList<OdfComment> Replies => _replies;

        public OdfComment(string author, string text) 
            : this(author, text, DateTime.UtcNow, Guid.NewGuid().ToString("N"))
        {
        }

        public OdfComment(string author, string text, DateTime date, string name)
        {
            Author = author ?? throw new ArgumentNullException(nameof(author));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Date = date;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public void AddReply(string author, string text)
        {
            var reply = new OdfComment(author, text, DateTime.UtcNow, Guid.NewGuid().ToString("N"));
            _replies.Add(reply);
        }

        public void AddReply(OdfComment reply)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));
            _replies.Add(reply);
        }

        private struct CommentStackFrame
        {
            public OdfComment Comment { get; }
            public string? ParentName { get; }
            public bool IsExit { get; }

            public CommentStackFrame(OdfComment comment, string? parentName, bool isExit)
            {
                Comment = comment;
                ParentName = parentName;
                IsExit = isExit;
            }
        }

        /// <summary>
        /// Renders this comment and its replies recursively into standard ODF 1.3 XML flat sibling <office:annotation> nodes.
        /// </summary>
        public OdfNode ToXmlNode()
        {
            if (_replies.Count == 0)
            {
                var annotationNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
                annotationNode.SetAttribute("name", OdfNamespaces.Office, Name, "office");

                // Creator element
                var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
                creator.TextContent = Author;
                annotationNode.AppendChild(creator);

                // Date element
                var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
                dateNode.TextContent = Date.ToString("yyyy-MM-ddTHH:mm:ssZ");
                annotationNode.AppendChild(dateNode);

                // Paragraph representing text
                var paragraphs = Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
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
                if (frame.ParentName != null)
                {
                    annotationNode.SetAttribute("annotation-parent", OdfNamespaces.Office, frame.ParentName, "office");
                }

                // Creator element
                var creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
                creator.TextContent = frame.Comment.Author;
                annotationNode.AppendChild(creator);

                // Date element
                var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
                dateNode.TextContent = frame.Comment.Date.ToString("yyyy-MM-ddTHH:mm:ssZ");
                annotationNode.AppendChild(dateNode);

                // Paragraph representing text
                var paragraphs = frame.Comment.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
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
                    if (child.LocalName == "creator") author = child.TextContent;
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
        /// Helper to parse standard ODF 1.3 XML flat sibling elements back to OdfComment object tree.
        /// </summary>
        public static OdfComment FromXmlNode(OdfNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            // Identify all candidate XML nodes
            IEnumerable<OdfNode> xmlNodes;
            if (node.LocalName == "annotation-list")
            {
                xmlNodes = node.Children;
            }
            else if (node.Parent != null)
            {
                xmlNodes = node.Parent.Children;
            }
            else
            {
                xmlNodes = new[] { node };
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
                    
                    // Generate a unique name
                    string uniqueName = originalName;
                    int counter = 1;
                    while (seenNames.Contains(uniqueName))
                    {
                        uniqueName = $"{originalName}_{counter++}";
                    }
                    seenNames.Add(uniqueName);

                    // Parse this single comment
                    var comment = FromXmlNodeSingle(child, uniqueName);
                    commentsMap[uniqueName] = comment;
                    parsedCommentsList.Add(comment);

                    // If this is the node passed to FromXmlNode, remember its parsed counterpart
                    if (child == node)
                    {
                        targetComment = comment;
                    }

                    // Look up parent
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
                    else if (rootComment == null)
                    {
                        rootComment = comment;
                    }

                    // Update most recent parent map
                    mostRecentParent[originalName] = uniqueName;
                }
            }

            // If we didn't find a rootComment (e.g. all comments have parents), fallback to the first parsed comment
            if (rootComment == null && commentsMap.Count > 0)
            {
                rootComment = parsedCommentsList[0];
            }

            // Link parents and replies in the original order of occurrence
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
}
