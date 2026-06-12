#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using OdfKit.DOM;
using OdfKit.Core;

namespace OdfKit.Spreadsheet
{
    public class OdfDatabaseRange
    {
        public OdfNode Node { get; }
        private readonly SpreadsheetDocument _doc;

        public OdfDatabaseRange(OdfNode node, SpreadsheetDocument doc)
        {
            Node = node;
            _doc = doc;
        }

        public string Name
        {
            get => Node.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
            set => Node.SetAttribute("name", OdfNamespaces.Table, value, "table");
        }

        public string TargetRangeAddress
        {
            get => Node.GetAttribute("target-range-address", OdfNamespaces.Table) ?? string.Empty;
            set => Node.SetAttribute("target-range-address", OdfNamespaces.Table, value, "table");
        }

        public void SetSort(params (int fieldNumber, bool ascending)[] rules)
        {
            OdfNode? existingSort = null;
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "sort" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    existingSort = child;
                    break;
                }
            }
            if (existingSort != null)
            {
                Node.RemoveChild(existingSort);
            }

            if (rules == null || rules.Length == 0) return;

            var sortNode = new OdfNode(OdfNodeType.Element, "sort", OdfNamespaces.Table, "table");
            foreach (var rule in rules)
            {
                var sortBy = new OdfNode(OdfNodeType.Element, "sort-by", OdfNamespaces.Table, "table");
                sortBy.SetAttribute("field-number", OdfNamespaces.Table, rule.fieldNumber.ToString(), "table");
                sortBy.SetAttribute("order", OdfNamespaces.Table, rule.ascending ? "ascending" : "descending", "table");
                sortNode.AppendChild(sortBy);
            }
            Node.AppendChild(sortNode);
        }

        public void SetFilter(params (int fieldNumber, string op, string value)[] conditions)
        {
            OdfNode? existingFilter = null;
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "filter" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    existingFilter = child;
                    break;
                }
            }
            if (existingFilter != null)
            {
                Node.RemoveChild(existingFilter);
            }

            if (conditions == null || conditions.Length == 0) return;

            var filterNode = new OdfNode(OdfNodeType.Element, "filter", OdfNamespaces.Table, "table");
            foreach (var cond in conditions)
            {
                var filterCond = new OdfNode(OdfNodeType.Element, "filter-condition", OdfNamespaces.Table, "table");
                filterCond.SetAttribute("field-number", OdfNamespaces.Table, cond.fieldNumber.ToString(), "table");
                filterCond.SetAttribute("operator", OdfNamespaces.Table, cond.op, "table");
                filterCond.SetAttribute("value", OdfNamespaces.Table, cond.value, "table");
                filterNode.AppendChild(filterCond);
            }
            Node.AppendChild(filterNode);
        }
    }
}
