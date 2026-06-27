using OdfKit.Compliance;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    private int _updateDepth;

    /// <summary>
    /// 取得目前文件是否位於批次更新範圍內。
    /// </summary>
    public bool IsUpdateActive => _updateDepth > 0;

    /// <summary>
    /// 開始批次更新範圍，暫緩樣式重對照與自動樣式去重，直到最外層範圍結束。
    /// </summary>
    /// <returns>可釋放的批次更新範圍；建議搭配 <c>using</c> 使用</returns>
    /// <remarks>
    /// 批次更新範圍支援巢狀呼叫。只有最外層 <see cref="EndUpdate"/> 或範圍釋放時，才會重新整理延後的樣式變更。
    /// </remarks>
    public IDisposable BeginUpdate()
    {
        _updateDepth++;
        StyleEngine.BeginUpdate();
        return new OdfDocumentUpdateScope(this);
    }

    /// <summary>
    /// 結束目前批次更新範圍，並在離開最外層範圍時重新整理延後的樣式變更。
    /// </summary>
    /// <exception cref="InvalidOperationException">當未先呼叫 <see cref="BeginUpdate"/> 時擲出</exception>
    public void EndUpdate()
    {
        if (_updateDepth == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDocument_EndUpdateWithoutBegin"));
        }

        _updateDepth--;
        StyleEngine.EndUpdate();
    }

    internal void FlushPendingUpdateChanges()
    {
        StyleEngine.FlushPendingStyles();
    }

    private sealed class OdfDocumentUpdateScope(OdfDocument document) : IDisposable
    {
        private OdfDocument? _document = document;

        public void Dispose()
        {
            OdfDocument? document = _document;
            if (document is null)
                return;

            _document = null;
            document.EndUpdate();
        }
    }
}
