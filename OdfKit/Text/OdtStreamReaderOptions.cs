namespace OdfKit.Text;

/// <summary>
/// Configures resource limits and stream ownership for <see cref="OdtStreamReader"/>.
/// 設定 <see cref="OdtStreamReader"/> 的資源限制與資料流所有權。
/// </summary>
public sealed class OdtStreamReaderOptions
{
    /// <summary>
    /// Gets or sets the maximum XML character count. A value of zero disables this limit.
    /// 取得或設定 XML 字元數上限；設為 0 代表停用此限制。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative. / 當值為負數時擲出。</exception>
    public long MaxXmlCharactersInDocument
    {
        get => _maxXmlCharactersInDocument;
        set => _maxXmlCharactersInDocument = OdfKit.Core.OdfOptionGuard.EnsureNonNegative(value, nameof(MaxXmlCharactersInDocument));
    }

    private long _maxXmlCharactersInDocument = 64L * 1024L * 1024L;

    /// <summary>
    /// Gets or sets the maximum number of text nodes returned by the reader.
    /// 取得或設定讀取器可回傳的文字節點數上限。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1. / 當值小於 1 時擲出。</exception>
    public int MaxNodes
    {
        get => _maxNodes;
        set => _maxNodes = OdfKit.Core.OdfOptionGuard.EnsurePositive(value, nameof(MaxNodes));
    }

    private int _maxNodes = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum extracted text length for one node.
    /// 取得或設定單一節點可擷取的文字長度上限。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 1. / 當值小於 1 時擲出。</exception>
    public int MaxNodeTextCharacters
    {
        get => _maxNodeTextCharacters;
        set => _maxNodeTextCharacters = OdfKit.Core.OdfOptionGuard.EnsurePositive(value, nameof(MaxNodeTextCharacters));
    }

    private int _maxNodeTextCharacters = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether the input stream remains open after disposal.
    /// 取得或設定處置讀取器後是否保持輸入資料流開啟。
    /// </summary>
    public bool LeaveOpen { get; set; }
}
