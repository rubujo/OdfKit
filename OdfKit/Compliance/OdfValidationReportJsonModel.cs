using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 表示驗證報告的 JSON 匯出模型。
/// </summary>
public sealed class OdfValidationReportJsonModel
{
    /// <summary>
    /// 初始化 <see cref="OdfValidationReportJsonModel"/> 類別的新執行個體。
    /// </summary>
    /// <param name="isValid">文件是否通過驗證</param>
    /// <param name="detectedVersion">偵測到的 ODF 版本</param>
    /// <param name="documentKind">偵測到的文件種類</param>
    /// <param name="infoCount">資訊性問題數量</param>
    /// <param name="warningCount">警告問題數量</param>
    /// <param name="errorCount">錯誤問題數量</param>
    /// <param name="fatalCount">致命問題數量</param>
    /// <param name="blockingIssueCount">會讓驗證失敗的問題數量</param>
    /// <param name="issues">驗證問題匯出模型集合</param>
    public OdfValidationReportJsonModel(
        bool isValid,
        string detectedVersion,
        string documentKind,
        int infoCount,
        int warningCount,
        int errorCount,
        int fatalCount,
        int blockingIssueCount,
        IReadOnlyList<OdfValidationIssueJsonModel> issues)
    {
        IsValid = isValid;
        DetectedVersion = detectedVersion ?? string.Empty;
        DocumentKind = documentKind ?? string.Empty;
        InfoCount = infoCount;
        WarningCount = warningCount;
        ErrorCount = errorCount;
        FatalCount = fatalCount;
        BlockingIssueCount = blockingIssueCount;
        Issues = issues ?? Array.Empty<OdfValidationIssueJsonModel>();
    }

    /// <summary>
    /// 取得文件是否通過驗證。
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 取得偵測到的 ODF 版本。
    /// </summary>
    public string DetectedVersion { get; }

    /// <summary>
    /// 取得偵測到的文件種類。
    /// </summary>
    public string DocumentKind { get; }

    /// <summary>
    /// 取得資訊性問題數量。
    /// </summary>
    public int InfoCount { get; }

    /// <summary>
    /// 取得警告問題數量。
    /// </summary>
    public int WarningCount { get; }

    /// <summary>
    /// 取得錯誤問題數量。
    /// </summary>
    public int ErrorCount { get; }

    /// <summary>
    /// 取得致命問題數量。
    /// </summary>
    public int FatalCount { get; }

    /// <summary>
    /// 取得會讓驗證失敗的問題數量。
    /// </summary>
    public int BlockingIssueCount { get; }

    /// <summary>
    /// 取得驗證問題匯出模型集合。
    /// </summary>
    public IReadOnlyList<OdfValidationIssueJsonModel> Issues { get; }
}
