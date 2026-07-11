using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

// 類產生式成品：例外／診斷訊息字典入口。
// 各語系內容見 OdfLocalizer.Exceptions.<culture>.cs；更新規則見 docs/maintainability.md。
/// <summary>
/// Builds the exception and diagnostic message dictionaries for all supported cultures.
/// 建立所有支援語系的例外與診斷訊息字典。
/// </summary>
public static partial class OdfLocalizer
{
    private static readonly Dictionary<string, Dictionary<string, string>> ExceptionDictionaries = CreateExceptionDictionaries();

    private static Dictionary<string, Dictionary<string, string>> CreateExceptionDictionaries()
    {
        Dictionary<string, Dictionary<string, string>> map = new(StringComparer.OrdinalIgnoreCase);
        AddExceptionDictionaryEn(map);
        AddExceptionDictionaryZhTw(map);
        AddExceptionDictionaryDe(map);
        AddExceptionDictionaryFr(map);
        AddExceptionDictionaryNl(map);
        AddExceptionDictionaryNb(map);
        AddExceptionDictionaryPt(map);
        AddExceptionDictionaryIt(map);
        AddExceptionDictionarySk(map);
        AddExceptionDictionaryDa(map);
        AddExceptionDictionaryMs(map);
        AddExceptionDictionaryKo(map);
        AddExceptionDictionaryJa(map);
        AddExceptionDictionaryEs(map);
        AddExceptionDictionaryCs(map);
        AddExceptionDictionaryPl(map);
        AddExceptionDictionaryPtBr(map);
        return map;
    }
}
