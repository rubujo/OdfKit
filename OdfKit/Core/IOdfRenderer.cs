using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace OdfKit.Core;

/// <summary>
/// Defines the IOdfRenderer contract.
/// 定義 ODF 文件渲染與 PDF 匯出的抽象介面。
/// </summary>
public interface IOdfRenderer
{
    /// <summary>
    /// 將指定的 OdfDocument 轉換並寫入 PDF 輸出資料流，支援可選的數位簽章。
    /// </summary>
    /// <param name="document">要進行轉換的 ODF 文件</param>
    /// <param name="pdfStream">要寫入 PDF 的目標資料流</param>
    /// <param name="certificate">用於簽章 PDF 的憑證；若為 null 則不簽章</param>
    void ExportToPdf(OdfDocument document, Stream pdfStream, X509Certificate2? certificate = null);
}

/// <summary>
/// Provides global registration and discovery for <see cref="IOdfRenderer"/> implementations.
/// 提供 <see cref="IOdfRenderer"/> 實作的全域註冊與探索機制。
/// </summary>
public static class OdfRendererRegistry
{
    private static IOdfRenderer? _renderer;
    private static bool _attemptedAutoRegister;

    /// <summary>
    /// Registers the renderer used by document export helpers.
    /// 註冊文件匯出輔助方法使用的渲染器。
    /// </summary>
    /// <param name="renderer">The renderer instance to register. / 要註冊的渲染器執行個體。</param>
    public static void Register(IOdfRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>
    /// Gets the registered renderer, or tries to discover the PDF extension renderer on first access.
    /// 取得已註冊的渲染器；若尚未註冊，第一次存取時會嘗試探索 PDF 擴充套件渲染器。
    /// </summary>
    public static IOdfRenderer? Instance
    {
        get
        {
            if (_renderer == null && !_attemptedAutoRegister && IsAutoRegistrationSupported)
            {
                _attemptedAutoRegister = true;
                TryAutoRegister();
            }
            return _renderer;
        }
    }

#if NET8_0_OR_GREATER
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    private static bool IsAutoRegistrationSupported => RuntimeFeature.IsDynamicCodeSupported;

    [RequiresUnreferencedCode("依名稱載入選用 PDF renderer；NativeAOT 應呼叫 Register 明確註冊。")]
#else
    private static bool IsAutoRegistrationSupported => true;
#endif
    private static void TryAutoRegister()
    {
        try
        {
            var assembly = Assembly.Load("OdfKit.Extensions.Pdf");
            var type = assembly.GetType("OdfKit.Export.OdfPdfRenderer");
            if (type != null)
            {
                var instance = Activator.CreateInstance(type) as IOdfRenderer;
                if (instance != null)
                {
                    _renderer = instance;
                }
            }
        }
        catch
        {
            // 若載入失敗（例如未參照或未部署該擴充套件），則靜默忽略，等待後續顯式註冊或丟出例外。
        }
    }
}
