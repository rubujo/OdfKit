using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Helper Methods

    /// <summary>
    /// 尋找或建立 office:meta 根節點。
    /// </summary>
    /// <returns>office:meta 節點。</returns>
    protected OdfNode FindOrCreateMetaRoot()
        => OdfDocumentMetadataEngine.FindOrCreateMetaRoot(MetaDom);

    /// <summary>
    /// 尋找指定名稱的設定項目。
    /// </summary>
    /// <param name="name">設定項目名稱。</param>
    /// <returns>設定項目節點；若不存在則為 <see langword="null"/>。</returns>
    protected OdfNode? FindSettingsConfigItem(string name)
        => OdfDocumentSettingsEngine.FindSettingsConfigItem(SettingsDom, name);

    /// <summary>
    /// 尋找或建立指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點。</param>
    /// <param name="name">設定集合名稱。</param>
    /// <returns>設定集合節點。</returns>
    protected OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
        => OdfDocumentSettingsEngine.FindOrCreateSettingsNode(root, name);

    /// <summary>
    /// 尋找指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點。</param>
    /// <param name="name">設定集合名稱。</param>
    /// <returns>設定集合節點；若不存在則為 <see langword="null"/>。</returns>
    protected OdfNode? FindSettingsNode(OdfNode root, string name)
        => OdfDocumentSettingsEngine.FindSettingsNode(root, name);

    /// <summary>
    /// 尋找或建立設定 map 節點。
    /// </summary>
    /// <param name="setNode">設定集合節點。</param>
    /// <param name="name">map 名稱。</param>
    /// <returns>設定 map 節點。</returns>
    protected OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
        => OdfDocumentSettingsEngine.FindOrCreateMapNode(setNode, name);

    /// <summary>
    /// 尋找或建立設定 map entry 節點。
    /// </summary>
    /// <param name="mapNode">設定 map 節點。</param>
    /// <returns>設定 map entry 節點。</returns>
    protected OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
        => OdfDocumentSettingsEngine.FindOrCreateMapEntryNode(mapNode);

    /// <summary>
    /// 尋找或建立設定項目節點。
    /// </summary>
    /// <param name="entryNode">設定 map entry 節點。</param>
    /// <param name="name">設定項目名稱。</param>
    /// <param name="type">設定項目類型。</param>
    /// <returns>設定項目節點。</returns>
    protected OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
        => OdfDocumentSettingsEngine.FindOrCreateConfigItemNode(entryNode, name, type);

    #endregion
}
