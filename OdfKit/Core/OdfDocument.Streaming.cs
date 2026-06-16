using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Web Streaming APIs


    /// <summary>
    /// 將文件儲存為 ODF 封裝位元組陣列。
    /// </summary>
    /// <returns>包含文件封裝內容的位元組陣列</returns>
    public byte[] SaveToBytes()
    {
        using var ms = new MemoryStream();
        SaveToStream(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項</param>
    public void SaveToStream(Stream destinationStream, OdfSaveOptions? options = null)
    {
        if (destinationStream == null)
            throw new ArgumentNullException(nameof(destinationStream));

        options ??= OdfSaveOptions.Default;
        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        Package.SaveToStream(destinationStream, options);

        if (destinationStream.CanSeek)
        {
            destinationStream.Position = 0;
        }
    }

    /// <summary>
    /// 非同步將文件儲存至指定的資料流。
    /// </summary>
    /// <param name="destinationStream">要寫入文件封裝內容的目標資料流。</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步儲存作業的工作。</returns>
    public Task SaveAsync(Stream destinationStream, OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => SaveToStream(destinationStream, options), cancellationToken);
    }


    #endregion
}
