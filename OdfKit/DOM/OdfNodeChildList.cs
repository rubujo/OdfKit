using System.Collections;
using System.Runtime.CompilerServices;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 以雙向鏈結串列實作的子節點集合；插入與移除為 O(1)，並以延遲索引快取支援隨機存取。
/// </summary>
public sealed class OdfNodeChildList : IList<OdfNode>
{
    private readonly OdfNode _owner;
    private int _count;
    private OdfNode[]? _indexCache;
    private bool _indexCacheValid;

    internal OdfNodeChildList(OdfNode owner)
    {
        _owner = owner;
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            _owner.EnsureMaterialized();
            return _count;
        }
    }

    /// <inheritdoc />
    public bool IsReadOnly => false;

    internal int LoadedCount => _count;

    /// <inheritdoc />
    public OdfNode this[int index]
    {
        get
        {
            _owner.EnsureMaterialized();
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            EnsureIndexCache();
            return _indexCache![index];
        }
        set => throw new NotSupportedException(OdfLocalizer.GetMessage("Err_OdfNodeChildList_ChildNodeCollectionsSupport"));
    }

    /// <summary>
    /// 搜尋符合條件的第一個子節點（與 <see cref="List{T}.Find"/> 語意相同）。
    /// </summary>
    public OdfNode? Find(Predicate<OdfNode> match)
    {
        if (match is null)
        {
            throw new ArgumentNullException(nameof(match));
        }
        _owner.EnsureMaterialized();
        for (OdfNode? node = _owner.FirstChild; node is not null; node = node.NextSibling)
        {
            if (match(node))
            {
                return node;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Add(OdfNode item) => Append(item);

    /// <inheritdoc />
    public void Clear()
    {
        _owner.EnsureMaterialized();
        for (OdfNode? node = _owner.FirstChild; node is not null;)
        {
            OdfNode? next = node.NextSibling;
            Unlink(node);
            node = next;
        }
    }

    /// <inheritdoc />
    public bool Contains(OdfNode item)
    {
        _owner.EnsureMaterialized();
        return item.Parent == _owner;
    }

    /// <inheritdoc />
    public void CopyTo(OdfNode[] array, int arrayIndex)
    {
        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }
        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        _owner.EnsureMaterialized();
        if (array.Length - arrayIndex < _count)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfNodeChildList_TargetArrayOutSpace"));
        }

        int i = arrayIndex;
        for (OdfNode? node = _owner.FirstChild; node is not null; node = node.NextSibling)
        {
            array[i++] = node;
        }
    }

    /// <inheritdoc />
    public IEnumerator<OdfNode> GetEnumerator()
    {
        _owner.EnsureMaterialized();
        for (OdfNode? node = _owner.FirstChild; node is not null; node = node.NextSibling)
        {
            yield return node;
        }
    }

    /// <inheritdoc />
    public int IndexOf(OdfNode item)
    {
        _owner.EnsureMaterialized();
        if (item.Parent != _owner)
        {
            return -1;
        }

        int cached = item.TryGetSiblingIndexForParent(_owner);
        if (cached >= 0)
        {
            return cached;
        }

        int index = 0;
        for (OdfNode? node = _owner.FirstChild; node is not null; node = node.NextSibling, index++)
        {
            if (ReferenceEquals(node, item))
            {
                item.SiblingIndex = index;
                return index;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public void Insert(int index, OdfNode item)
    {
        _owner.EnsureMaterialized();
        if ((uint)index > (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == _count)
        {
            Append(item);
            return;
        }

        OdfNode refChild = this[index];
        InsertBefore(item, refChild);
    }

    /// <inheritdoc />
    public bool Remove(OdfNode item)
    {
        if (item.Parent != _owner)
        {
            return false;
        }

        Unlink(item);
        return true;
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        OdfNode child = this[index];
        Unlink(child);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void Append(OdfNode child)
    {
        if (child is null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        child.DetachFromParent();
        LinkLast(child);
    }

    internal void InsertBefore(OdfNode newChild, OdfNode refChild)
    {
        if (newChild is null)
        {
            throw new ArgumentNullException(nameof(newChild));
        }

        if (refChild is null)
        {
            throw new ArgumentNullException(nameof(refChild));
        }
        if (refChild.Parent != _owner)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNodeChildList_ReferenceNodeChildNode_2"));
        }

        newChild.DetachFromParent();
        LinkBefore(newChild, refChild);
    }

    internal void InsertAfter(OdfNode newChild, OdfNode refChild)
    {
        if (newChild is null)
        {
            throw new ArgumentNullException(nameof(newChild));
        }

        if (refChild is null)
        {
            throw new ArgumentNullException(nameof(refChild));
        }
        if (refChild.Parent != _owner)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNodeChildList_ReferenceNodeChildNode_2"));
        }

        newChild.DetachFromParent();
        if (refChild.NextSibling is null)
        {
            LinkLast(newChild);
            return;
        }

        LinkBefore(newChild, refChild.NextSibling);
    }

    internal void Unlink(OdfNode child)
    {
        if (child.PreviousSibling is not null)
        {
            child.PreviousSibling.NextSibling = child.NextSibling;
        }
        else
        {
            _owner.FirstChild = child.NextSibling;
        }

        if (child.NextSibling is not null)
        {
            child.NextSibling.PreviousSibling = child.PreviousSibling;
        }
        else
        {
            _owner.LastChild = child.PreviousSibling;
        }

        child.Parent = null;
        child.PreviousSibling = null;
        child.NextSibling = null;
        child.SiblingIndex = -1;
        _count--;
        InvalidateIndexCache();
        ReindexFromSibling(_owner.FirstChild, 0);
    }

    internal void ResetAfterPrune()
    {
        _count = 0;
        _indexCache = null;
        _indexCacheValid = false;
    }

    internal void ReleaseIndexCache()
    {
        _indexCache = null;
        _indexCacheValid = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LinkLast(OdfNode child)
    {
        child.Parent = _owner;
        child.NextSibling = null;
        child.PreviousSibling = _owner.LastChild;
        if (_owner.LastChild is not null)
        {
            _owner.LastChild.NextSibling = child;
        }
        else
        {
            _owner.FirstChild = child;
        }

        _owner.LastChild = child;
        child.SiblingIndex = _count;
        _count++;
        InvalidateIndexCache();
    }

    private void LinkBefore(OdfNode newChild, OdfNode refChild)
    {
        newChild.Parent = _owner;
        newChild.NextSibling = refChild;
        newChild.PreviousSibling = refChild.PreviousSibling;
        if (refChild.PreviousSibling is not null)
        {
            refChild.PreviousSibling.NextSibling = newChild;
        }
        else
        {
            _owner.FirstChild = newChild;
        }

        refChild.PreviousSibling = newChild;
        int insertIndex = refChild.SiblingIndex >= 0 ? refChild.SiblingIndex : IndexOf(refChild);
        newChild.SiblingIndex = insertIndex;
        refChild.SiblingIndex = insertIndex + 1;
        _count++;
        InvalidateIndexCache();
        ReindexFromSibling(refChild, insertIndex + 1);
    }

    private void ReindexFromSibling(OdfNode? start, int startIndex)
    {
        for (OdfNode? node = start; node is not null; node = node.NextSibling, startIndex++)
        {
            node.SiblingIndex = startIndex;
        }
    }

    private void EnsureIndexCache()
    {
        if (_indexCacheValid && _indexCache is not null && _indexCache.Length == _count)
        {
            return;
        }

        if (_count == 0)
        {
            _indexCache = [];
            _indexCacheValid = true;
            return;
        }

        var cache = new OdfNode[_count];
        int i = 0;
        for (OdfNode? node = _owner.FirstChild; node is not null; node = node.NextSibling)
        {
            node.SiblingIndex = i;
            cache[i++] = node;
        }

        _indexCache = cache;
        _indexCacheValid = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidateIndexCache() => _indexCacheValid = false;
}
