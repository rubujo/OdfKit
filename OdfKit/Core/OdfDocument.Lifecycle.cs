using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Package Lifecycle & Persistence


    /// <summary>
    /// 儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    public virtual void Save(OdfSaveOptions? options = null)
    {
        options ??= OdfSaveOptions.Default;

        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        Package.Save(options);
    }

    /// <summary>
    /// 將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑。</param>
    /// <param name="options">儲存設定選項。</param>
    public void Save(string path, OdfSaveOptions? options = null)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        options ??= OdfSaveOptions.Default;
        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Package.SaveToStream(stream, options);
    }

    /// <summary>
    /// 非同步儲存文件至 ODF 封裝容器中。
    /// </summary>
    /// <param name="options">儲存設定選項</param>
    /// <param name="cancellationToken">取消語彙</param>
    /// <returns>代表非同步作業的 Task 執行個體</returns>
    public virtual async Task SaveAsync(OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= OdfSaveOptions.Default;

        StyleEngine.DeduplicateAndSaveStyles();
        UpdateDocumentStatistics();
        ApplySaveVersionOptions(options);

        WriteDomToEntry("content.xml", ContentDom, options);
        WriteDomToEntry("styles.xml", StylesDom, options);
        WriteDomToEntry("meta.xml", MetaDom, options);
        WriteDomToEntry("settings.xml", SettingsDom, options);

        await Package.SaveAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 非同步將文件保存到指定檔案路徑。
    /// </summary>
    /// <param name="path">要寫入的檔案路徑。</param>
    /// <param name="options">儲存設定選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步儲存作業的工作。</returns>
    public Task SaveAsync(string path, OdfSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Save(path, options), cancellationToken);
    }

    private void WriteDomToEntry(string name, OdfNode node, OdfSaveOptions options)
    {
        using var ms = new MemoryStream();
        OdfXmlWriter.Write(node, ms, options);
        string path = string.IsNullOrEmpty(SubPath) ? name : SubPath + name;
        Package.WriteEntry(path, ms.ToArray(), "text/xml");
    }


    #endregion
}
