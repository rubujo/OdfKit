using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region High-Level Digital Signatures


    /// <summary>
    /// 使用指定的 X.509 憑證對文件進行數位簽章。
    /// </summary>
    /// <param name="certificate">用於簽章的憑證</param>
    public void Sign(X509Certificate2 certificate)
    {
        StyleEngine.DeduplicateAndSaveStyles();
        WriteDomToEntry("content.xml", ContentDom, OdfSaveOptions.Default);
        WriteDomToEntry("styles.xml", StylesDom, OdfSaveOptions.Default);
        WriteDomToEntry("meta.xml", MetaDom, OdfSaveOptions.Default);
        WriteDomToEntry("settings.xml", SettingsDom, OdfSaveOptions.Default);

        OdfSigner.Sign(Package, certificate);
    }

    /// <summary>
    /// 取得文件封裝內數位簽章項目的摘要狀態。
    /// </summary>
    /// <returns>描述簽章項目存在狀態、可讀性與簽章數量的摘要。</returns>
    public OdfDocumentSignatureSummary GetSignatureSummary()
    {
        if (!Package.HasEntry(DocumentSignaturesPath))
        {
            return OdfDocumentSignatureSummary.Unsigned(DocumentSignaturesPath);
        }

        try
        {
            using Stream stream = Package.GetEntryStream(DocumentSignaturesPath);
            int signatureCount = CountSignatureElements(stream, Package.LoadOptions.MaxXmlCharactersInDocument);
            return OdfDocumentSignatureSummary.Readable(DocumentSignaturesPath, signatureCount);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is XmlException)
        {
            return OdfDocumentSignatureSummary.Unreadable(DocumentSignaturesPath, ex.Message);
        }
    }

    /// <summary>
    /// 驗證文件中的所有數位簽章。
    /// </summary>
    /// <param name="certificates">輸出參數，傳回驗證通過的憑證集合</param>
    /// <returns>若所有簽章皆驗證成功則傳回 true；否則傳回 false</returns>
    public bool VerifySignatures(out X509Certificate2Collection certificates)
    {
        return OdfSigner.VerifySignatures(Package, out certificates);
    }

    /// <summary>
    /// 驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <param name="options">簽章驗證選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <returns>詳細的數位簽章驗證結果。</returns>
    public OdfSignatureValidationResult VerifySignatures(OdfSigningOptions? options = null)
    {
        return OdfSigner.VerifySignatures(Package, options);
    }

    /// <summary>
    /// 非同步驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <param name="options">簽章驗證選項；若為 <see langword="null"/>，則使用預設選項。</param>
    /// <returns>代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果。</returns>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync(OdfSigningOptions? options = null)
    {
        return OdfSignatureVerifier.VerifySignaturesAsync(Package, options);
    }

    private static int CountSignatureElements(Stream stream, long maxCharsInDocument = 0)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharsInDocument > 0 ? maxCharsInDocument : 0
        };

        int count = 0;
        using XmlReader reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element &&
                reader.LocalName == "Signature" &&
                reader.NamespaceURI == OdfNamespaces.Ds)
            {
                count++;
            }
        }

        return count;
    }


    #endregion
}
