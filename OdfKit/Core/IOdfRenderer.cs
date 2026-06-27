using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace OdfKit.Core;

/// <summary>
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
/// 提供 IOdfRenderer 實作之全域註冊與取得機制。
/// </summary>
public static class OdfRendererRegistry
{
    private static IOdfRenderer? _renderer;
    private static bool _attemptedAutoRegister;

    /// <summary>
    /// 註冊預設的 ODF 渲染器。
    /// </summary>
    /// <param name="renderer">要註冊的渲染器實例</param>
    public static void Register(IOdfRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>
    /// 取得目前已註冊的渲染器實例，若未註冊則嘗試動態自動載入已引用的 PDF 擴充套件。
    /// </summary>
    public static IOdfRenderer? Instance
    {
        get
        {
            if (_renderer == null && !_attemptedAutoRegister)
            {
                _attemptedAutoRegister = true;
                TryAutoRegister();
            }
            return _renderer;
        }
    }

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
