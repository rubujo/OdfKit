using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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
        SignAsync(certificate).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 非同步使用指定的 X.509 憑證對文件進行數位簽章。
    /// </summary>
    /// <param name="certificate">用於簽章的憑證</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步簽章作業的工作</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 DOM 寫入、ZIP 寫入與 HTTP（TSA／CRL）期間協作檢查取消語彙。
    /// </remarks>
    public async Task SignAsync(X509Certificate2 certificate, CancellationToken cancellationToken = default)
    {
        StyleEngine.DeduplicateAndSaveStyles();
        OdfDocumentPersistenceEngine.WriteAllDomEntries(PersistenceCollaborators, OdfSaveOptions.Default);

        await OdfSigner.SignAsync(
            Package,
            certificate,
            new OdfSigningOptions { Level = XadesLevel.None },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 取得文件封裝內數位簽章專案的摘要狀態。
    /// </summary>
    /// <returns>描述簽章專案存在狀態、可讀性與簽章數量的摘要</returns>
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
        // 簽章摘要為最佳努力查詢：無法讀取時回傳 Unreadable，不向上拋出。
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
    /// <param name="options">簽章驗證選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <returns>詳細的數位簽章驗證結果</returns>
    public OdfSignatureValidationResult VerifySignatures(OdfSigningOptions? options = null)
    {
        return OdfSigner.VerifySignatures(Package, options);
    }

    /// <summary>
    /// 非同步驗證文件中的所有數位簽章，並傳回詳細驗證結果。
    /// </summary>
    /// <param name="options">簽章驗證選項；若為 <see langword="null"/>，則使用預設選項</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步驗證作業的工作，其結果包含詳細的數位簽章驗證結果</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在簽章解析與 HTTP（CRL）期間協作檢查取消語彙。
    /// </remarks>
    public Task<OdfSignatureValidationResult> VerifySignaturesAsync(
        OdfSigningOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return OdfSigner.VerifySignaturesAsync(Package, options, cancellationToken);
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
