using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 自 DOM 樹移除指定修訂 ID 的變更標記與受影響內容。
/// </summary>
internal sealed class OdfTrackedChangePurger(string targetId)
{
    private readonly string _targetId = targetId;
    private bool _foundStart;
    private bool _foundEnd;

    /// <summary>
    /// 遞迴清除目標修訂 ID 的變更標記與區間內容。
    /// </summary>
    /// <param name="node">起始節點</param>
    public void Purge(OdfNode node)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            OdfNode child = node.Children[i];

            bool isEnd = child.LocalName == "change-end" &&
                child.NamespaceUri == OdfNamespaces.Text &&
                child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId;
            bool isStart = child.LocalName == "change-start" &&
                child.NamespaceUri == OdfNamespaces.Text &&
                child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId;

            if (isEnd)
            {
                _foundEnd = true;
                node.RemoveChild(child);
                continue;
            }

            if (isStart)
            {
                _foundStart = true;
                node.RemoveChild(child);
                continue;
            }

            bool wasEndFoundBefore = _foundEnd;
            bool wasStartFoundBefore = _foundStart;

            Purge(child);

            bool containedEnd = !wasEndFoundBefore && _foundEnd;
            bool containedStart = !wasStartFoundBefore && _foundStart;

            if (_foundEnd && !_foundStart && !containedEnd)
            {
                node.RemoveChild(child);
            }
        }
    }
}
