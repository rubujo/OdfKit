using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.DOM;

#region Office Wrappers


/// <summary>
/// Provides the OfficeDocumentElement API.
/// 表示 ODF 中的 office:document 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeDocumentElement(string? prefix = null) : OdfElement("document", OdfNamespaces.Office, prefix);

/// <summary>
/// Provides the OfficeDocumentContentElement API.
/// 表示 ODF 中的 office:document-content 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeDocumentContentElement(string? prefix = null) : OdfElement("document-content", OdfNamespaces.Office, prefix);

/// <summary>
/// Provides the OfficeBodyElement API.
/// 表示 ODF 中的 office:body 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeBodyElement(string? prefix = null) : OdfElement("body", OdfNamespaces.Office, prefix);

/// <summary>
/// Provides the OfficeTextElement API.
/// 表示 ODF 中的 office:text 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeTextElement(string? prefix = null) : OdfElement("text", OdfNamespaces.Office, prefix);

/// <summary>
/// Provides the OfficeSpreadsheetElement API.
/// 表示 ODF 中的 office:spreadsheet 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeSpreadsheetElement(string? prefix = null) : OdfElement("spreadsheet", OdfNamespaces.Office, prefix);

/// <summary>
/// Provides the OfficePresentationElement API.
/// 表示 ODF 中的 office:presentation 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficePresentationElement(string? prefix = null) : OdfElement("presentation", OdfNamespaces.Office, prefix);

/// <summary>
/// Provides the OfficeDrawingElement API.
/// 表示 ODF 中的 office:drawing 元素。
/// </summary>
/// <param name="prefix">選用的命名空間前綴</param>
public partial class OfficeDrawingElement(string? prefix = null) : OdfElement("drawing", OdfNamespaces.Office, prefix);


#endregion
