using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Database;

public partial class OdfDatabaseDocument
{
    /// <summary>
    /// Gets the list of currently declared report components.
    /// 取得目前宣告的報表元件清單。
    /// </summary>
    public IReadOnlyList<OdfDatabaseReportInfo> Reports => GetReports();

    /// <summary>
    /// Gets the list of currently declared report components.
    /// 取得目前宣告的報表元件清單。
    /// </summary>
    /// <returns>The list of report components. / 報表元件清單。</returns>
    public IReadOnlyList<OdfDatabaseReportInfo> GetReports()
    {
        OdfNode? reportsNode = FindChildElement(GetDatabaseNode(), "reports", DatabaseNamespace);
        if (reportsNode is null)
        {
            return [];
        }

        List<OdfDatabaseReportInfo> reports = [];
        CollectReportComponents(reportsNode, reports);
        return reports.AsReadOnly();
    }

    /// <summary>
    /// Finds a report component by name.
    /// 依名稱尋找報表元件。
    /// </summary>
    /// <param name="name">The report name. / 報表名稱。</param>
    /// <returns>The matching report component, or <see langword="null"/> if not found. / 符合名稱的報表元件；找不到時為 <see langword="null"/>。</returns>
    public OdfDatabaseReportInfo? FindReport(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseDocument_ReportCannotBeEmpty_3"), nameof(name));
        }

        foreach (OdfDatabaseReportInfo report in GetReports())
        {
            if (string.Equals(report.Name, name, StringComparison.Ordinal))
            {
                return report;
            }
        }

        return null;
    }

    private static void CollectReportComponents(OdfNode parent, List<OdfDatabaseReportInfo> reports)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != DatabaseNamespace)
            {
                continue;
            }

            if (child.LocalName == "component")
            {
                reports.Add(new OdfDatabaseReportInfo(
                    child.GetAttribute("name", DatabaseNamespace) ?? string.Empty,
                    child.GetAttribute("href", OdfNamespaces.XLink),
                    child.GetAttribute("title", DatabaseNamespace),
                    child.GetAttribute("description", DatabaseNamespace),
                    ParseNullableBoolean(child.GetAttribute("as-template", DatabaseNamespace))));
            }
            else if (child.LocalName == "component-collection")
            {
                CollectReportComponents(child, reports);
            }
        }
    }
}
