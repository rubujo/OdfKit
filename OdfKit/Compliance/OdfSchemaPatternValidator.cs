using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 針對 XML 元素評估保留的 RELAX NG 模式樹中繼資料。
/// </summary>
public static partial class OdfSchemaPatternValidator
{
    /// <summary>
    /// 根據具名的結構描述模式驗證 XML 元素。
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="schema">結構描述集</param>
    /// <param name="patternName">模式名稱</param>
    /// <returns>模式驗證結果</returns>
    public static OdfSchemaPatternValidationResult ValidateElement(
        XElement element,
        OdfSchemaSet schema,
        string patternName)
    {
        if (element is null)
            throw new ArgumentNullException(nameof(element));
        if (schema is null)
            throw new ArgumentNullException(nameof(schema));
        if (string.IsNullOrWhiteSpace(patternName))
            throw new ArgumentException("Pattern name cannot be empty.", nameof(patternName));

        OdfSchemaPatternDefinition? pattern = schema.FindPattern(patternName);
        if (pattern is null)
        {
            return OdfSchemaPatternValidationResult.Fail(
                "ODF3100",
                $"Schema pattern '{patternName}' is not available.");
        }

        var context = new OdfSchemaPatternMatchContext(schema);
        foreach (OdfSchemaPatternNode root in pattern.Roots)
        {
            if (MatchesRootNode(root, element, context))
            {
                return OdfSchemaPatternValidationResult.Success();
            }
        }

        return OdfSchemaPatternValidationResult.Fail(
            "ODF3101",
            $"Element '{{{element.Name.NamespaceName}}}{element.Name.LocalName}' does not match schema pattern '{patternName}'.");
    }

}
