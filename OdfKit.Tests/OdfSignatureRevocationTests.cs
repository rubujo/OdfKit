using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對 OdfSignatureVerifier.Revocation.cs（CRL 撤銷檢查邏輯）、OdfSignatureTsaClient.cs
/// （TSA／CRL HTTP 用戶端）與 OdfSignatureCrlUtilities.cs（CRL 剖析與驗簽）三個檔案的專屬測試。
/// 撤銷邏輯相關測試以反射直接呼叫 internal 且 private 的
/// OdfSignatureVerifier.VerifyRevocationStatusAsync，略過完整 XAdES 簽章／驗證管線
/// （該管線中不在本次委託範圍內的檔案含有無條件的 catch (Exception ex)，會吞掉部分例外並轉換為
/// 一般驗證失敗結果），以精準鎖定這三個檔案本身的行為語意；TSA／CRL HTTP 用戶端則因其成員為
/// internal static，直接呼叫即可，不需反射。
/// </summary>
// 依 DocsAndCorpusContractTests.LargeTestFilesDeclareCategoryTrait 的門檻（>= 700 行且含
// [Fact]/[Theory]）宣告分類。選擇 Boundary 而非 Smoke：本檔測試 CRL／TSA 有效期、簽章鏈與撤銷
// 檢查等安全邊界條件（缺少 nextUpdate、序號 0、偽造時間戳等），性質與同樣標記 Boundary 的
// AdvancedSecurityTests.cs（本檔涵蓋範圍的姊妹檔）一致，而非 Windows smoke 矩陣鎖定的
// 快速好路徑子集。.github/workflows/ci.yml 的 Windows smoke 矩陣以
// Category=Smoke&FullyQualifiedName~<特定類別> 篩選，目前沒有任何條目引用
// OdfSignatureRevocationTests，因此標記 Boundary 不會、也不需要把本檔拉進該矩陣；
// 本檔仍由不加篩選的完整回歸套件（同工作流程的 full regression job，兩個 TFM）每次涵蓋。
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class OdfSignatureRevocationTests
{
    private static readonly MethodInfo s_verifyRevocationMethod =
        typeof(OdfSigningOptions).Assembly.GetType("OdfKit.Core.OdfSignatureVerifier")
            ?.GetMethod("VerifyRevocationStatusAsync", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "找不到 OdfSignatureVerifier.VerifyRevocationStatusAsync，其簽章可能已變更，需同步更新本測試檔的反射呼叫。");

    private static Task<bool> InvokeVerifyRevocationStatusAsync(
        List<X509Certificate2> chainCerts,
        List<byte[]> embeddedCrls,
        OdfSigningOptions options,
        OdfSingleSignatureValidationResult singleResult,
        CancellationToken cancellationToken)
    {
        return (Task<bool>)s_verifyRevocationMethod.Invoke(
            null,
            new object?[] { chainCerts, embeddedCrls, options, singleResult, cancellationToken })!;
    }

    #region 分發點下載失敗與彙整

    [Fact]
    public async Task CrlHostAllowListRejectsUnlistedHostBeforeSendingRequest()
    {
        int requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        });
        using var httpClient = new HttpClient(handler);
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "allowed.example.test" };

        await Assert.ThrowsAsync<HttpRequestException>(() => OdfSignatureTsaClient.DownloadCrlAsync(
            "https://blocked.example.test/list.crl",
            httpClient,
            allowedHosts,
            TestContext.Current.CancellationToken));

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task CrlHostAllowListAllowsExactCaseInsensitiveHost()
    {
        var handler = new MockHttpMessageHandler((request, ct) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
        }));
        using var httpClient = new HttpClient(handler);
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CRL.EXAMPLE.TEST" };

        byte[] content = await OdfSignatureTsaClient.DownloadCrlAsync(
            "https://crl.example.test/list.crl",
            httpClient,
            allowedHosts,
            TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 1, 2, 3 }, content);
    }

    [Fact]
    public async Task AllDistributionPointsFailReturnsAggregatedFailureWithBothUrls()
    {
        byte[] cdp = BuildCdpExtension(
            "http://crl1.example.test/a.crl",
            "http://crl2.example.test/b.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootA", "RevLeafA", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)));
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
        Assert.Contains("crl1.example.test", singleResult.ErrorMessage ?? "", StringComparison.Ordinal);
        Assert.Contains("crl2.example.test", singleResult.ErrorMessage ?? "", StringComparison.Ordinal);
        Assert.Contains(singleResult.Warnings, w => w.Contains("crl1.example.test", StringComparison.Ordinal));
        Assert.Contains(singleResult.Warnings, w => w.Contains("crl2.example.test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnlineCrlInvalidSignatureTreatedAsDistributionPointFailure()
    {
        byte[] cdp = BuildCdpExtension("http://crl.example.test/bad-signature.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootG", "RevLeafG", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] badSigCrl = CreateMockCrlBytes(rootCert, new List<string>(), useInvalidSignature: true);

        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(badSigCrl)
            }));
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
        Assert.Contains(singleResult.Warnings, w => w.Contains("bad-signature.crl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IssuerCertificateNotInChainWithCheckRevocationTrueReturnsRevocationCheckFailed()
    {
        var (root, leaf) = GenerateCertificateChain("RevRootE", "RevLeafE");
        using var rootCert = root;
        using var leafCert = leaf;

        var options = new OdfSigningOptions { CheckRevocation = true };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        // 故意不含 root，issuer 憑證在鏈中找不到。
        var chainCerts = new List<X509Certificate2> { leafCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
    }

    [Fact]
    public async Task NoCrlDistributionPointsAndNoEmbeddedCrlReturnsRevocationCheckFailed()
    {
        var (root, leaf) = GenerateCertificateChain("RevRootF", "RevLeafF");
        using var rootCert = root;
        using var leafCert = leaf;

        var options = new OdfSigningOptions { CheckRevocation = true };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
    }

    #endregion

    #region 撤銷判定與並行下載語意

    [Fact]
    public async Task OnlineCrlReportsRevokedSingleUrlReturnsCertificateRevoked()
    {
        byte[] cdp = BuildCdpExtension("http://crl.example.test/revoked.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootI", "RevLeafI", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] revokedCrl = CreateMockCrlBytes(rootCert, new List<string> { leafCert.SerialNumber });

        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(revokedCrl)
            }));
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("CERTIFICATE_REVOKED", singleResult.ErrorCode);
    }

    /// <summary>
    /// 鎖定「不可取第一個成功結果」的安全語意：較快的分發點回報正常（無撤銷），
    /// 較慢的分發點回報撤銷，最終仍必須判定為撤銷，不可因第一個分發點成功即提早判定通過。
    /// </summary>
    [Fact]
    public async Task FastCleanUrlThenSlowRevokedUrlStillDetectsRevocation()
    {
        byte[] cdp = BuildCdpExtension(
            "http://crl.example.test/fast-clean.crl",
            "http://crl.example.test/slow-revoked.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootB", "RevLeafB", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] cleanCrl = CreateMockCrlBytes(rootCert, new List<string>());
        byte[] revokedCrl = CreateMockCrlBytes(rootCert, new List<string> { leafCert.SerialNumber });

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("fast-clean", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(cleanCrl)
                };
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(revokedCrl)
            };
        });
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("CERTIFICATE_REVOKED", singleResult.ErrorCode);
    }

    /// <summary>
    /// 鎖定並行下載的提早取消語意：第一個分發點回報撤銷後即中斷迴圈，
    /// 尚未完成的其餘分發點下載必須被取消，而非繼續等待或洩漏。
    /// </summary>
    [Fact]
    public async Task RevokedAtFirstUrlCancelsRemainingInFlightDownload()
    {
        byte[] cdp = BuildCdpExtension(
            "http://crl.example.test/fast-revoked.crl",
            "http://crl.example.test/never-resolves.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootC", "RevLeafC", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] revokedCrl = CreateMockCrlBytes(rootCert, new List<string> { leafCert.SerialNumber });
        var secondDownloadCancelledTcs = new TaskCompletionSource();

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("fast-revoked", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(revokedCrl)
                };
            }

            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                secondDownloadCancelledTcs.TrySetResult();
                throw;
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Equal("CERTIFICATE_REVOKED", singleResult.ErrorCode);

        Task completedTask = await Task.WhenAny(
            secondDownloadCancelledTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Same(secondDownloadCancelledTcs.Task, completedTask);
    }

    #endregion

    #region 內嵌 CRL 簽章驗證

    [Fact]
    public async Task EmbeddedCrlInvalidSignatureReturnsCrlSignatureInvalidImmediately()
    {
        // 無 CDP 延伸，逼迫只能走內嵌 CRL 分支。
        var (root, leaf) = GenerateCertificateChain("RevRootH", "RevLeafH");
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] badSigCrl = CreateMockCrlBytes(rootCert, new List<string>(), useInvalidSignature: true);

        var options = new OdfSigningOptions { CheckRevocation = true };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };
        var embeddedCrls = new List<byte[]> { badSigCrl };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, embeddedCrls, options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("CRL_SIGNATURE_INVALID", singleResult.ErrorCode);
    }

    #endregion

    #region CRL 有效期檢查（修正 1：簽章合法但已過期／尚未生效／缺少 nextUpdate 的 CRL 不可被採信）

    /// <summary>
    /// 修正 1 的核心攻擊情境：攻擊者取得憑證撤銷之前、由 CA 正式簽發、簽章完全合法的舊 CRL
    /// （issuer 與簽章皆通過驗證，且不含該憑證序號），嵌入文件企圖繞過撤銷檢查。
    /// 在加入 thisUpdate／nextUpdate 有效期檢查之前，這會讓 checkedAnyCrl 被設為 true，
    /// 使第 207 行附近的保險絲被跳過，最終被誤判為「已成功檢查且未撤銷」而通過驗證——
    /// 即使 options.CheckRevocation 為 true 也擋不住。修正後必須視同撤銷檢查失敗。
    /// </summary>
    [Fact]
    public async Task EmbeddedCrlExpiredSignatureValidIssuerMatchesTreatedAsRevocationCheckFailedNotUnrevoked()
    {
        var (root, leaf) = GenerateCertificateChain("RevRootExpired", "RevLeafExpired");
        using var rootCert = root;
        using var leafCert = leaf;

        // UTCTime 兩位數年份規則（RFC 5280 §4.1.2.5.1，BouncyCastle 亦採此規則）：YY >= 50 對應 19YY。
        // 990101／990201 因此代表 1999 年，確保在任何執行時間點都早已過期，不含該憑證序號，
        // 藉此排除「因序號比對而判定撤銷」的可能，單純鎖定「有效期檢查」本身。
        byte[] expiredCrl = CreateMockCrlBytes(
            rootCert,
            new List<string>(),
            thisUpdateDer: "990101000000Z",
            nextUpdateDer: "990201000000Z");

        var options = new OdfSigningOptions { CheckRevocation = true };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };
        var embeddedCrls = new List<byte[]> { expiredCrl };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, embeddedCrls, options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
    }

    [Fact]
    public async Task EmbeddedCrlNotYetValidTreatedAsRevocationCheckFailed()
    {
        var (root, leaf) = GenerateCertificateChain("RevRootNotYet", "RevLeafNotYet");
        using var rootCert = root;
        using var leafCert = leaf;

        // YY < 50 對應 20YY：48／49 代表 2048／2049，確保 thisUpdate 落在任何執行時間點的未來。
        byte[] notYetValidCrl = CreateMockCrlBytes(
            rootCert,
            new List<string>(),
            thisUpdateDer: "480101000000Z",
            nextUpdateDer: "491231235959Z");

        var options = new OdfSigningOptions { CheckRevocation = true };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };
        var embeddedCrls = new List<byte[]> { notYetValidCrl };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, embeddedCrls, options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
    }

    /// <summary>
    /// nextUpdate 為選用欄位，但對不可信來源的內嵌 CRL 從嚴解讀：省略 nextUpdate 一律視為
    /// 「無法確認有效期」而拒絕採信，不得放行為「永久有效」。
    /// </summary>
    [Fact]
    public async Task EmbeddedCrlMissingNextUpdateTreatedAsRevocationCheckFailed()
    {
        var (root, leaf) = GenerateCertificateChain("RevRootNoNextUpdate", "RevLeafNoNextUpdate");
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] missingNextUpdateCrl = CreateMockCrlBytes(
            rootCert,
            new List<string>(),
            nextUpdateDer: null);

        var options = new OdfSigningOptions { CheckRevocation = true };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };
        var embeddedCrls = new List<byte[]> { missingNextUpdateCrl };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, embeddedCrls, options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
    }

    /// <summary>
    /// 線上下載 CRL 路徑必須與內嵌 CRL 路徑受到相同的有效期檢查涵蓋。
    /// </summary>
    [Fact]
    public async Task OnlineCrlExpiredTreatedAsDistributionPointFailure()
    {
        byte[] cdp = BuildCdpExtension("http://crl.example.test/expired.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootOnlineExpired", "RevLeafOnlineExpired", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] expiredCrl = CreateMockCrlBytes(
            rootCert,
            new List<string>(),
            thisUpdateDer: "990101000000Z",
            nextUpdateDer: "990201000000Z");

        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expiredCrl)
            }));
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        bool result = await InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(singleResult.IsRevocationValid);
        Assert.Equal("REVOCATION_CHECK_FAILED", singleResult.ErrorCode);
        Assert.Contains(singleResult.Warnings, w => w.Contains("expired.crl", StringComparison.Ordinal));
    }

    #endregion

    #region GetRevokedSerialNumbers 解析失敗不可靜默吞掉（修正 2）

    /// <summary>
    /// 修正前：GetRevokedSerialNumbers 內部有空的 catch { }，任何解析例外都會被吞掉並靜默回傳
    /// （可能是部分或完全空的）撤銷序號集合，導致呼叫端誤判為「未撤銷」。
    /// 修正後：解析失敗必須向外拋出，交由呼叫端轉為撤銷檢查失敗，而非被靜默吞掉。
    /// </summary>
    [Fact]
    public void GetRevokedSerialNumbersMalformedCrlContentThrowsInsteadOfSilentlyReturningEmpty()
    {
        byte[] garbage = { 0x01, 0x02, 0x03 };
        Assert.ThrowsAny<Exception>(() => OdfSignatureCrlUtilities.GetRevokedSerialNumbers(garbage));
    }

    #endregion

    #region 外部 CancellationToken 取消傳遞

    /// <summary>
    /// 外部 CancellationToken 於並行下載期間取消時，必須正確傳遞為 OperationCanceledException。
    /// 因單一分發點下載被包裝為傳回值（不拋出），實際的取消是在下一個分發點迴圈開頭的
    /// ThrowIfCancellationRequested 觸發，因此使用兩個分發點以確定性地鎖定此語意，
    /// 而非依賴計時的取消時間點。
    /// </summary>
    [Fact]
    public async Task ExternalCancellationDuringConcurrentDownloadPropagatesOperationCanceledException()
    {
        byte[] cdp = BuildCdpExtension(
            "http://crl.example.test/slow-a.crl",
            "http://crl.example.test/fast-b.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootD", "RevLeafD", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        byte[] cleanCrl = CreateMockCrlBytes(rootCert, new List<string>());

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("slow-a", StringComparison.Ordinal))
            {
                // 只會被外部取消提早中止，測試不會真的等滿此逾時。
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(cleanCrl)
            };
        });
        using var httpClient = new HttpClient(handler);

        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        using var cts = new CancellationTokenSource();
        Task<bool> verifyTask = InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifyTask);
    }

    /// <summary>
    /// 驗證只有單一 CRL 分發點時，下載期間的外部取消仍會直接向呼叫端傳遞。
    /// </summary>
    [Fact]
    public async Task ExternalCancellationDuringSingleDownloadPropagatesOperationCanceledException()
    {
        byte[] cdp = BuildCdpExtension("http://crl.example.test/slow-only.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootSingle", "RevLeafSingle", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        var handler = new MockHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);
        var options = new OdfSigningOptions { CheckRevocation = true, HttpClient = httpClient };
        var singleResult = new OdfSingleSignatureValidationResult { IsRevocationValid = true };
        var chainCerts = new List<X509Certificate2> { leafCert, rootCert };

        using var cts = new CancellationTokenSource();
        Task<bool> verifyTask = InvokeVerifyRevocationStatusAsync(
            chainCerts, new List<byte[]>(), options, singleResult, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verifyTask);
    }

    #endregion

    #region OdfSignatureCrlUtilities 直接單元測試

    [Fact]
    public void GetCrlUrlsMultipleUrisInSingleDistributionPointReturnsAllInOrder()
    {
        byte[] cdp = BuildCdpExtension(
            "http://crl.example.test/one.crl",
            "http://crl.example.test/two.crl");
        var (root, leaf) = GenerateCertificateChain("RevRootJ", "RevLeafJ", cdp);
        using var rootCert = root;
        using var leafCert = leaf;

        List<string> urls = OdfSignatureCrlUtilities.GetCrlUrls(leafCert);

        Assert.Equal(2, urls.Count);
        Assert.Equal("http://crl.example.test/one.crl", urls[0]);
        Assert.Equal("http://crl.example.test/two.crl", urls[1]);
    }

    [Fact]
    public void IsCrlTimeValidWithinThisUpdateAndNextUpdateReturnsTrue()
    {
        var (root, _) = GenerateCertificateChain("RevRootTimeValidUnit", "RevLeafTimeValidUnit");
        using var rootCert = root;

        byte[] crl = CreateMockCrlBytes(rootCert, new List<string>());

        bool isValid = OdfSignatureCrlUtilities.IsCrlTimeValid(crl, DateTime.UtcNow, out string? invalidReason);

        Assert.True(isValid);
        Assert.Null(invalidReason);
    }

    [Fact]
    public void IsCrlTimeValidNextUpdateBeforeNowReturnsFalseWithReason()
    {
        var (root, _) = GenerateCertificateChain("RevRootExpiredUnit", "RevLeafExpiredUnit");
        using var rootCert = root;

        byte[] crl = CreateMockCrlBytes(
            rootCert,
            new List<string>(),
            thisUpdateDer: "990101000000Z",
            nextUpdateDer: "990201000000Z");

        bool isValid = OdfSignatureCrlUtilities.IsCrlTimeValid(crl, DateTime.UtcNow, out string? invalidReason);

        Assert.False(isValid);
        Assert.False(string.IsNullOrEmpty(invalidReason));
    }

    [Fact]
    public void IsCrlTimeValidThisUpdateAfterNowReturnsFalseWithReason()
    {
        var (root, _) = GenerateCertificateChain("RevRootNotYetUnit", "RevLeafNotYetUnit");
        using var rootCert = root;

        byte[] crl = CreateMockCrlBytes(
            rootCert,
            new List<string>(),
            thisUpdateDer: "480101000000Z",
            nextUpdateDer: "491231235959Z");

        bool isValid = OdfSignatureCrlUtilities.IsCrlTimeValid(crl, DateTime.UtcNow, out string? invalidReason);

        Assert.False(isValid);
        Assert.False(string.IsNullOrEmpty(invalidReason));
    }

    [Fact]
    public void IsCrlTimeValidMissingNextUpdateReturnsFalseWithReason()
    {
        var (root, _) = GenerateCertificateChain("RevRootNoNextUpdateUnit", "RevLeafNoNextUpdateUnit");
        using var rootCert = root;

        byte[] crl = CreateMockCrlBytes(rootCert, new List<string>(), nextUpdateDer: null);

        bool isValid = OdfSignatureCrlUtilities.IsCrlTimeValid(crl, DateTime.UtcNow, out string? invalidReason);

        Assert.False(isValid);
        Assert.False(string.IsNullOrEmpty(invalidReason));
    }

    #endregion

    #region OdfSignatureTsaClient 直接單元測試

    [Fact]
    public async Task DownloadCrlAsyncNonSuccessStatusCodeThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)));
        using var httpClient = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            OdfSignatureTsaClient.DownloadCrlAsync(
                "http://crl.example.test/a.crl", httpClient, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadCrlAsyncPreCancelledTokenThrowsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            }));
        using var httpClient = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OdfSignatureTsaClient.DownloadCrlAsync("http://crl.example.test/a.crl", httpClient, cts.Token));
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://127.0.0.1/a.crl")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/a.crl")]
    public async Task ValidatePublicHttpUriAsyncRejectsUnsafeDistributionPoints(string uri)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            OdfSignatureTsaClient.ValidatePublicHttpUriAsync(uri, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QueryTsaAsyncNonSuccessStatusCodeThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)));
        using var httpClient = new HttpClient(handler);

        byte[] hash = new byte[32];
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            OdfSignatureTsaClient.QueryTsaAsync(
                "http://tsa.example.test/tsa", hash, httpClient, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QueryTsaAsyncPreCancelledTokenThrowsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        using var httpClient = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        byte[] hash = new byte[32];
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OdfSignatureTsaClient.QueryTsaAsync("http://tsa.example.test/tsa", hash, httpClient, cts.Token));
    }

    /// <summary>
    /// 以 handler 直接拋出 TaskCanceledException 模擬 HttpClient 逾時（避免真的等待 30 秒預設逾時），
    /// 驗證 QueryTsaAsync 不會吞掉逾時例外，會如實向外傳遞。
    /// </summary>
    [Fact]
    public async Task QueryTsaAsyncHandlerThrowsSimulatedTimeoutPropagatesAsOperationCanceledException()
    {
        var handler = new MockHttpMessageHandler((request, ct) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated TSA network timeout.")));
        using var httpClient = new HttpClient(handler);

        byte[] hash = new byte[32];
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OdfSignatureTsaClient.QueryTsaAsync(
                "http://tsa.example.test/tsa", hash, httpClient, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExtractTimestampTokenMalformedDerThrowsCryptographicException()
    {
        byte[] garbage = { 0x01, 0x02, 0x03 };
        Assert.Throws<CryptographicException>(() => OdfSignatureTsaClient.ExtractTimestampToken(garbage));
    }

    [Fact]
    public void ExtractTimestampTokenRejectedStatusThrowsCryptographicException()
    {
        byte[] response = BuildTsaStatusOnlyResponse(status: 2); // 2 = rejection
        Assert.Throws<CryptographicException>(() => OdfSignatureTsaClient.ExtractTimestampToken(response));
    }

    [Fact]
    public void ExtractTimestampTokenMissingTimestampTokenThrowsCryptographicException()
    {
        byte[] response = BuildTsaStatusOnlyResponse(status: 0); // 0 = granted，但缺少 timeStampToken 欄位
        Assert.Throws<CryptographicException>(() => OdfSignatureTsaClient.ExtractTimestampToken(response));
    }

    #endregion

    #region 測試用憑證、CRL 與 DER 建構輔助方法

#if NET9_0_OR_GREATER
    private static X509Certificate2 LoadCertificateFromPfx(byte[] pfxData)
    {
        return X509CertificateLoader.LoadPkcs12Collection(
            pfxData,
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet)[0];
    }
#else
    private static X509Certificate2 LoadCertificateFromPfx(byte[] pfxData)
    {
        return new X509Certificate2(
            pfxData,
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
    }
#endif

    private static (X509Certificate2 Root, X509Certificate2 Leaf) GenerateCertificateChain(
        string rootName, string leafName, byte[]? cdpBytes = null)
    {
        using var rootRsa = RSA.Create(2048);
        var rootRequest = new CertificateRequest($"CN={rootName}", rootRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

        X509Certificate2 rootCert = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));

        using var leafRsa = RSA.Create(2048);
        var leafRequest = new CertificateRequest($"CN={leafName}", leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, false));

        if (cdpBytes != null)
        {
            leafRequest.CertificateExtensions.Add(new X509Extension("2.5.29.31", cdpBytes, false));
        }

        byte[] serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        serial[0] &= 0x7F; // 確保序號為正整數。

        X509Certificate2 leafCert = leafRequest.Create(rootCert, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(5), serial);
        X509Certificate2 leafWithKey = leafCert.CopyWithPrivateKey(leafRsa);

        byte[] rootPfx = rootCert.Export(X509ContentType.Pfx);
        X509Certificate2 rootImported = LoadCertificateFromPfx(rootPfx);

        byte[] leafPfx = leafWithKey.Export(X509ContentType.Pfx);
        X509Certificate2 leafImported = LoadCertificateFromPfx(leafPfx);

        return (rootImported, leafImported);
    }

    /// <summary>
    /// 建立含一或多個 URI 分發點的 CRLDistributionPoints (2.5.29.31) 延伸欄位原始 DER 位元組，
    /// 所有 URL 包裝於同一個 DistributionPoint 的 GeneralNames 內。
    /// </summary>
    private static byte[] BuildCdpExtension(params string[] urls)
    {
        byte[] generalNameEntries = Concat(urls.Select(url => WrapTlv(0x86, Encoding.ASCII.GetBytes(url))));
        byte[] generalNames = WrapTlv(0xa0, generalNameEntries); // fullName [0] IMPLICIT GeneralNames
        byte[] distributionPointName = WrapTlv(0xa0, generalNames); // distributionPoint [0] DistributionPointName
        byte[] distributionPoint = WrapTlv(0x30, distributionPointName); // DistributionPoint SEQUENCE
        return WrapTlv(0x30, distributionPoint); // CRLDistributionPoints ::= SEQUENCE OF DistributionPoint
    }

    private static byte[] BuildTsaStatusOnlyResponse(int status)
    {
        // PKIStatusInfo ::= SEQUENCE { status INTEGER, ... }
        byte[] statusInfo = WrapTlv(0x30, WrapTlv(0x02, new[] { (byte)status }));
        // TimeStampResp ::= SEQUENCE { status PKIStatusInfo, timeStampToken TimeStampToken OPTIONAL }
        // 此處故意省略 timeStampToken，模擬缺漏或格式錯誤的回應。
        return WrapTlv(0x30, statusInfo);
    }

    /// <summary>
    /// 建立測試用 CRL 位元組。<paramref name="thisUpdateDer"/>／<paramref name="nextUpdateDer"/> 採 UTCTime
    /// 內容格式（YYMMDDHHMMSSZ）；<paramref name="nextUpdateDer"/> 傳入 <see langword="null"/> 代表完全省略
    /// nextUpdate 欄位（用於驗證「缺少 nextUpdate 一律視為無法採信」的從嚴語意）。預設值刻意選在測試執行時
    /// 的合理有效期間內（thisUpdate 為過去、nextUpdate 為遠未來），確保未特別指定日期的既有測試案例
    /// 不受 CRL 有效期檢查影響。
    /// </summary>
    private static byte[] CreateMockCrlBytes(
        X509Certificate2 issuerCert,
        List<string> revokedSerials,
        bool useInvalidSignature = false,
        string thisUpdateDer = "260611000000Z",
        string? nextUpdateDer = "270611000000Z")
    {
        byte[] sigAlg = { 0x30, 0x0d, 0x06, 0x09, 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x0b, 0x05, 0x00 };
        byte[] issuerName = issuerCert.IssuerName.RawData;
        byte[] thisUpdate = EncodeUtcTime(thisUpdateDer);
        byte[] nextUpdate = nextUpdateDer != null ? EncodeUtcTime(nextUpdateDer) : Array.Empty<byte>();

        var revokedItemsList = new List<byte[]>();
        foreach (string serialHex in revokedSerials)
        {
            byte[] serialBytes = ParseHex(serialHex);
            byte[] integerBytes = WrapTlv(0x02, serialBytes);
            byte[] dateBytes = EncodeUtcTime("260611000000Z");

            byte[] itemInner = Concat(new[] { integerBytes, dateBytes });
            revokedItemsList.Add(WrapTlv(0x30, itemInner));
        }

        byte[] revokedSeq = revokedItemsList.Count > 0
            ? WrapTlv(0x30, Concat(revokedItemsList))
            : Array.Empty<byte>();

        byte[] tbsInner = Concat(new[] { sigAlg, issuerName, thisUpdate, nextUpdate, revokedSeq });
        byte[] tbsCertList = WrapTlv(0x30, tbsInner);

        byte[] sigValueBytes;
        if (useInvalidSignature)
        {
            sigValueBytes = new byte[] { 0x03, 0x03, 0x00, 0x01, 0x02 };
        }
        else
        {
            using RSA? rsa = issuerCert.GetRSAPrivateKey();
            if (rsa == null)
            {
                throw new InvalidOperationException("Issuer certificate does not have RSA private key.");
            }

            byte[] signature = rsa.SignData(tbsCertList, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            byte[] bitStringValue = new byte[signature.Length + 1];
            bitStringValue[0] = 0x00;
            Buffer.BlockCopy(signature, 0, bitStringValue, 1, signature.Length);

            sigValueBytes = WrapTlv(0x03, bitStringValue);
        }

        byte[] outerInner = Concat(new[] { tbsCertList, sigAlg, sigValueBytes });
        return WrapTlv(0x30, outerInner);
    }

    /// <summary>
    /// 將 UTCTime 內容字串（YYMMDDHHMMSSZ）編碼為 DER UTCTime（tag 0x17）。
    /// </summary>
    private static byte[] EncodeUtcTime(string yyMmDdHhMmSsZ) => WrapTlv(0x17, Encoding.ASCII.GetBytes(yyMmDdHhMmSsZ));

    private static byte[] WrapTlv(byte tag, byte[] inner)
    {
        byte[] len = EncodeDerLength(inner.Length);
        byte[] result = new byte[1 + len.Length + inner.Length];
        result[0] = tag;
        Buffer.BlockCopy(len, 0, result, 1, len.Length);
        Buffer.BlockCopy(inner, 0, result, 1 + len.Length, inner.Length);
        return result;
    }

    private static byte[] Concat(IEnumerable<byte[]> chunks)
    {
        using var ms = new MemoryStream();
        foreach (byte[] chunk in chunks)
        {
            ms.Write(chunk, 0, chunk.Length);
        }

        return ms.ToArray();
    }

    private static byte[] EncodeDerLength(int len)
    {
        if (len < 128)
        {
            return new[] { (byte)len };
        }

        if (len <= 255)
        {
            return new byte[] { 0x81, (byte)len };
        }

        return new byte[] { 0x82, (byte)(len >> 8), (byte)(len & 0xFF) };
    }

    private static byte[] ParseHex(string hex)
    {
        hex = hex.Replace("-", "");
        if (hex.Length % 2 != 0)
        {
            hex = "0" + hex;
        }

        byte[] raw = new byte[hex.Length / 2];
        for (int i = 0; i < raw.Length; i++)
        {
            raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return raw;
    }

    #endregion
}
