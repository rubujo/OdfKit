using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Image;

public partial class OdfImageDocument
{
    /// <summary>
    /// Gets the filter setting of the image frame with the specified name.
    /// 取得指定名稱影像框架的濾鏡設定。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <returns>The filter setting, or <see langword="null"/> if the frame does not exist or has no filter set. / 濾鏡設定；若框架不存在或未設定濾鏡則為 <see langword="null"/>。</returns>
    public OdfImageFilterInfo? GetImageFilter(string name)
    {
        OdfNode? frame = FindFrameByName(name);
        OdfNode? image = frame is null ? null : FindChild(frame, "image", OdfNamespaces.Draw);
        string? filterName = image?.GetAttribute("filter-name", OdfNamespaces.Draw);
        if (string.IsNullOrEmpty(filterName))
        {
            return null;
        }

        string? settings = image!.GetAttribute("filter-settings", OdfNamespaces.Draw);
        return new OdfImageFilterInfo(filterName!, ParseFilterSettings(settings));
    }

    /// <summary>
    /// Sets the filter of the image frame with the specified name.
    /// 設定指定名稱影像框架的濾鏡。
    /// </summary>
    /// <param name="name">The frame name. / 框架名稱。</param>
    /// <param name="filter">The filter setting; pass <see langword="null"/> to remove the existing filter. / 濾鏡設定；傳入 <see langword="null"/> 表示移除既有濾鏡。</param>
    /// <returns><see langword="true"/> if set successfully; <see langword="false"/> if the frame is not found. / 若成功設定則為 <see langword="true"/>；找不到框架時為 <see langword="false"/>。</returns>
    public bool SetImageFilter(string name, OdfImageFilterInfo? filter)
    {
        OdfNode? frame = FindFrameByName(name);
        OdfNode? image = frame is null ? null : FindChild(frame, "image", OdfNamespaces.Draw);
        if (image is null)
        {
            return false;
        }

        if (filter is null)
        {
            image.RemoveAttribute("filter-name", OdfNamespaces.Draw);
            image.RemoveAttribute("filter-settings", OdfNamespaces.Draw);
            return true;
        }

        image.SetAttribute("filter-name", OdfNamespaces.Draw, filter.FilterName, "draw");
        string settings = FormatFilterSettings(filter.Parameters);
        if (string.IsNullOrEmpty(settings))
        {
            image.RemoveAttribute("filter-settings", OdfNamespaces.Draw);
        }
        else
        {
            image.SetAttribute("filter-settings", OdfNamespaces.Draw, settings, "draw");
        }

        return true;
    }

    private static IReadOnlyDictionary<string, string> ParseFilterSettings(string? raw)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(raw))
        {
            return result;
        }

        foreach (string pair in raw!.Split(','))
        {
            int separator = pair.IndexOf(':');
            if (separator <= 0 || separator >= pair.Length - 1)
            {
                continue;
            }

            result[pair.Substring(0, separator)] = pair.Substring(separator + 1);
        }

        return result;
    }

    private static string FormatFilterSettings(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(parameters.Count);
        foreach (KeyValuePair<string, string> pair in parameters)
        {
            parts.Add($"{pair.Key}:{pair.Value}");
        }

        return string.Join(",", parts);
    }
}
