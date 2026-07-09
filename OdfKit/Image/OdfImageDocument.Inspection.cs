using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using OdfKit.Compliance;

namespace OdfKit.Image;

/// <summary>
/// Provides image inspection APIs for <see cref="OdfImageDocument"/>.
/// 提供 <see cref="OdfImageDocument"/> 的圖片檢查 API。
/// </summary>
public partial class OdfImageDocument
{
    /// <summary>
    /// Inspects images for practical portable-editing risks.
    /// 檢查圖片的實務可攜編輯風險。
    /// </summary>
    /// <returns>The image inspection report. / 圖片檢查報告。</returns>
    public OdfImageInspectionReport InspectImages() => InspectImages(null, null);

    /// <summary>
    /// Short overload of InspectImages that accepts options; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 options；其餘可選參數使用預設值並轉呼叫最長 InspectImages 多載。
    /// </summary>
    public OdfImageInspectionReport InspectImages(OdfImageInspectionOptions? options) => InspectImages(options, null);

    /// <summary>
    /// Inspects images for practical portable-editing risks with a compatibility profile.
    /// 依相容性設定檔檢查圖片的實務可攜編輯風險。
    /// </summary>
    /// <param name="options">The inspection options. / 檢查選項。</param>
    /// <param name="profile">The practical compatibility profile. / 實務相容性設定檔。</param>
    /// <returns>The image inspection report. / 圖片檢查報告。</returns>
    public OdfImageInspectionReport InspectImages(
        OdfImageInspectionOptions? options,
        OdfPracticalCompatibilityProfile? profile)
    {
        options ??= new OdfImageInspectionOptions();
        var report = new OdfImageInspectionReport();
        var seenHashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (OdfImageFrameInfo frame in GetImageFrames())
        {
            if (!IsPortableImageMediaType(frame.MediaType))
            {
                report.Issues.Add(CreateIssue("IMG0001", "Msg_ImageInspection_NonPortableMediaType", frame, OdfIssueSeverity.Warning, profile));
            }

            if (options.ReportMissingAltText &&
                string.IsNullOrWhiteSpace(frame.Title) &&
                string.IsNullOrWhiteSpace(frame.Description))
            {
                report.Issues.Add(CreateIssue("IMG0002", "Msg_ImageInspection_MissingAltText", frame, OdfIssueSeverity.Info, profile));
            }

            if (frame.Size.HasValue && frame.Size.Value > options.LargeImageThresholdBytes)
            {
                report.Issues.Add(CreateIssue("IMG0003", "Msg_ImageInspection_LargeImage", frame, OdfIssueSeverity.Info, profile));
            }

            if (frame.Crop is not null || frame.RotationDegrees.HasValue)
            {
                report.Issues.Add(CreateIssue("IMG0004", "Msg_ImageInspection_Transform", frame, OdfIssueSeverity.Warning, profile));
            }

            if (!string.IsNullOrWhiteSpace(frame.ImageHref) && Package.HasEntry(frame.ImageHref!))
            {
                using SHA256 sha256 = SHA256.Create();
                string hash = Convert.ToBase64String(sha256.ComputeHash(Package.ReadEntry(frame.ImageHref!)));
                if (seenHashes.TryGetValue(hash, out _))
                {
                    report.Issues.Add(CreateIssue("IMG0005", "Msg_ImageInspection_DuplicateBytes", frame, OdfIssueSeverity.Info, profile));
                }
                else
                {
                    seenHashes.Add(hash, frame.ImageHref!);
                }
            }
        }

        return report;
    }

    private static OdfImageInspectionIssue CreateIssue(
        string ruleId,
        string messageKey,
        OdfImageFrameInfo frame,
        OdfIssueSeverity severity,
        OdfPracticalCompatibilityProfile? profile) =>
        new(
            ruleId,
            frame.Name,
            frame.ImageHref,
            OdfLocalizer.GetMessage(messageKey),
            OdfLocalizer.GetSuggestedFix(ruleId),
            severity,
            profile,
            messageKey,
            "Rule_SuggestedFix_" + ruleId);

    private static bool IsPortableImageMediaType(string? mediaType) =>
        mediaType is not null &&
        (mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase));
}
