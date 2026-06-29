using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace OdfKit.Compliance;

/// <summary>
/// Provides multi-language localization translation and culture fallback lookup for the entire OdfKit library.
/// 提供 OdfKit 整個函式庫的多語系本地化翻譯與語系 Fallback 查找機制。
/// </summary>
public static partial class OdfLocalizer
{
    private static readonly Dictionary<string, Func<Dictionary<string, string>>> FactoryRegistrations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> DictionaryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncRoot = new();

    /// <summary>
    /// Gets or sets the global default culture. When set, it overrides the thread's default culture.
    /// 取得或設定全域預設的文化特性。若設定，將覆蓋執行緒預設語系。
    /// </summary>
    public static CultureInfo? DefaultCulture { get; set; }


    /// <summary>
    /// Gets the suggested fix guidance for the specified rule identifier and culture.
    /// 取得指定規則識別碼與文化特性的建議修復指引。
    /// </summary>
    /// <param name="ruleId">The unique identifier of the compliance rule. / 合規性規則的唯一識別碼。</param>
    /// <param name="culture">The specified culture; if null, the environment culture is auto-detected. / 指定的文化特性；若為 null 則自動偵測環境語系。</param>
    /// <returns>The suggested fix guidance string for the corresponding culture. / 對應語系的建議修復指引字串。</returns>
    public static string GetSuggestedFix(string ruleId, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(ruleId))
        {
            return string.Empty;
        }

        var dict = ResolveDictionary(culture ?? DefaultCulture ?? CultureInfo.CurrentUICulture);

        // 1. 優先嘗試查詢專屬的合規修復 Key (Rule_SuggestedFix_ODF0001)
        string ruleKey = "Rule_SuggestedFix_" + ruleId;
        if (dict is not null && dict.TryGetValue(ruleKey, out var fix))
        {
            return fix;
        }

        // 2. 後備相容：查詢既有的 Rule ID 鍵值
        if (dict is not null && dict.TryGetValue(ruleId, out fix))
        {
            return fix;
        }

        // 若無匹配，回退到英文 (en) 預設翻譯
        var enDict = ResolveDictionary(CultureInfo.InvariantCulture);
        if (enDict is not null && enDict.TryGetValue(ruleKey, out var enFix))
        {
            return enFix;
        }
        if (enDict is not null && enDict.TryGetValue(ruleId, out enFix))
        {
            return enFix;
        }

        // 3. 最終防線：才進入 BuildDefaultSuggestedFix 返回硬編碼英文
        return BuildDefaultSuggestedFix(ruleId);
    }

    /// <summary>
    /// Gets the localized error/warning message for the specified key (using the environment culture).
    /// 取得指定鍵值的本地化錯誤/警告訊息（使用環境語系）。
    /// </summary>
    /// <param name="messageKey">The message key. / 訊息鍵值。</param>
    /// <returns>The localized message for the corresponding culture. / 對應語系的本地化訊息。</returns>
    public static string GetMessage(string messageKey)
    {
        return GetMessage(messageKey, (CultureInfo?)null);
    }

    /// <summary>
    /// Gets the localized error/warning message for the specified key and culture.
    /// 取得指定鍵值與文化特性的本地化錯誤/警告訊息。
    /// </summary>
    /// <param name="messageKey">The message key. / 訊息鍵值。</param>
    /// <param name="culture">The specified culture; if null, the environment culture is auto-detected. / 指定的文化特性；若為 null 則自動偵測環境語系。</param>
    /// <returns>The localized message for the corresponding culture. / 對應語系的本地化訊息。</returns>
    public static string GetMessage(string messageKey, CultureInfo? culture)
    {
        if (string.IsNullOrEmpty(messageKey))
        {
            return string.Empty;
        }

        var dict = ResolveDictionary(culture ?? DefaultCulture ?? CultureInfo.CurrentUICulture);
        if (dict is not null && dict.TryGetValue(messageKey, out var msg))
        {
            return msg;
        }

        var enDict = ResolveDictionary(CultureInfo.InvariantCulture);
        if (enDict is not null && enDict.TryGetValue(messageKey, out var enMsg))
        {
            return enMsg;
        }

        return messageKey;
    }

    /// <summary>
    /// Gets the localized error/warning message for the specified key, formatted with the specified arguments (using the environment culture).
    /// 取得指定鍵值的本地化錯誤/警告訊息，並使用指定參數進行格式化（使用環境語系）。
    /// </summary>
    /// <param name="messageKey">The message key. / 訊息鍵值。</param>
    /// <param name="args">The format arguments. / 格式化參數。</param>
    /// <returns>The formatted localized message. / 格式化後的本地化訊息。</returns>
    public static string GetMessage(string messageKey, params object?[] args)
    {
        string format = GetMessage(messageKey, (CultureInfo?)null);
        try
        {
            return string.Format(DefaultCulture ?? CultureInfo.CurrentUICulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>
    /// Gets the localized error/warning message for the specified key and culture, formatted with the specified arguments.
    /// 取得指定鍵值與文化特性的本地化錯誤/警告訊息，並使用指定參數進行格式化。
    /// </summary>
    /// <param name="messageKey">The message key. / 訊息鍵值。</param>
    /// <param name="culture">The specified culture; if null, the environment culture is auto-detected. / 指定的文化特性；若為 null 則自動偵測環境語系。</param>
    /// <param name="args">The format arguments. / 格式化參數。</param>
    /// <returns>The formatted localized message. / 格式化後的本地化訊息。</returns>
    public static string GetMessage(string messageKey, CultureInfo? culture, params object?[] args)
    {
        string format = GetMessage(messageKey, culture);
        try
        {
            return string.Format(culture ?? DefaultCulture ?? CultureInfo.CurrentUICulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>
    /// 解析指定文化特性的本地化字典。
    /// </summary>
    /// <param name="culture">指定的文化特性</param>
    /// <returns>對應文化特性的本地化字典；若皆無匹配，則回退至預設英文字典；若仍無則傳回 null</returns>
    private static Dictionary<string, string>? ResolveDictionary(CultureInfo culture)
    {
        var current = culture;
        while (current is not null)
        {
            string name = current.Name;

            // 對於 InvariantCulture，我們強制將其視為 "en"，因為預設英文翻譯註冊在 "en" 鍵上
            if (string.IsNullOrEmpty(name))
            {
                name = "en";
            }

            if (DictionaryCache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            lock (SyncRoot)
            {
                if (DictionaryCache.TryGetValue(name, out cached))
                {
                    return cached;
                }

                if (FactoryRegistrations.TryGetValue(name, out var factory))
                {
                    var dict = factory();
                    MergeExceptionDictionary(name, dict);
                    DictionaryCache[name] = dict;
                    return dict;
                }
                else if (ExceptionDictionaries.TryGetValue(name, out var excDict))
                {
                    var dict = new Dictionary<string, string>(excDict, StringComparer.Ordinal);
                    DictionaryCache[name] = dict;
                    return dict;
                }
            }

            if (current == CultureInfo.InvariantCulture || string.IsNullOrEmpty(current.Name))
            {
                break;
            }

            current = current.Parent;
        }

        // 若完全沒有，回退到 en
        if (DictionaryCache.TryGetValue("en", out var cachedEn))
        {
            return cachedEn;
        }
        lock (SyncRoot)
        {
            if (DictionaryCache.TryGetValue("en", out cachedEn))
            {
                return cachedEn;
            }
            if (FactoryRegistrations.TryGetValue("en", out var factoryEn))
            {
                var dict = factoryEn();
                MergeExceptionDictionary("en", dict);
                DictionaryCache["en"] = dict;
                return dict;
            }
            else if (ExceptionDictionaries.TryGetValue("en", out var excDictEn))
            {
                var dict = new Dictionary<string, string>(excDictEn, StringComparer.Ordinal);
                DictionaryCache["en"] = dict;
                return dict;
            }
        }

        return null;
    }

    /// <summary>
    /// 合併例外訊息字典至目標字典中。
    /// </summary>
    /// <param name="name">語系名稱</param>
    /// <param name="target">要合併的目標字典</param>
    private static void MergeExceptionDictionary(string name, Dictionary<string, string> target)
    {
        if (ExceptionDictionaries.TryGetValue(name, out var excDict))
        {
            foreach (var kvp in excDict)
            {
                target[kvp.Key] = kvp.Value;
            }
        }

        MergeSupplementalComplianceDictionary(name, target);
    }

    /// <summary>
    /// 取得指定規則識別碼的預設硬編碼英文修復建議（作為最終 Fallback 備份防線）。
    /// </summary>
    /// <param name="ruleId">合規性規則的唯一識別碼</param>
    /// <returns>硬編碼的英文修復建議字串</returns>
    private static string BuildDefaultSuggestedFix(string ruleId)
    {
        return ruleId switch
        {
            "ODF0001" => "Add a valid mimetype entry.",
            "ODF0003" => "Place the mimetype entry as the first item in the ZIP package.",
            "ODF0004" => "Store the mimetype entry without compression.",
            "ODF0100" => "Add META-INF/manifest.xml and describe the package content.",
            "ODF0200" or "ODF0201" => "Remove unsafe or non-conforming ZIP entry paths.",
            "ODF0300" or "ODF3000" or "ODF3100" or "ODF3101" or "ODF3102" => "Correct the XML structure according to ODF schemas.",
            "ODF0400" or "ODF1002" => "Add or correct the office:version attribute in {0}.",
            "ODF0500" or "ODF0501" or "ODF2006" or "ODF3002" => "Correct the office:body content type to match the MIME type and extension.",
            "ODF1000" or "ODF1001" => "Verify that the document version matches the selected compliance profile.",
            "DisallowInvalidOdfNamespaceExtensions" => "Remove or correct elements/attributes in ODF namespaces not defined in the schema.",
            "RequireForeignExtensionIsolation" => "Place extensions in non-ODF namespaces and ensure they are removable.",
            "DisallowMacroByDefault" => "Remove macros, scripts, and event listeners, or use a policy allowing macros.",
            "RequireSafeExternalResourcePolicy" => "Use embedded resources or ensure external references comply with deployment policy.",
            _ => "Correct the document content based on validation messages."
        };
    }
}
