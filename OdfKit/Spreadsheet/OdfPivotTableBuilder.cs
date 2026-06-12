#pragma warning disable 1591 // Suppress CS1591 (missing XML comments) for legacy hand-written APIs to maintain zero-warning compilation under TreatWarningsAsErrors while package XML documentation is generated.
using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Core;

namespace OdfKit.Spreadsheet
{
    public class OdfPivotTableBuilder
    {
        private readonly string _name;
        private readonly OdfCellRange _sourceRange;
        private readonly OdfCellAddress _targetStart;
        private readonly OdfTableSheet _sheet;
        private readonly List<(string name, string orientation, string? function)> _fields = new();

        public OdfPivotTableBuilder(string name, OdfCellRange sourceRange, OdfCellAddress targetStart, OdfTableSheet sheet)
        {
            _name = name;
            _sourceRange = sourceRange;
            _targetStart = targetStart;
            _sheet = sheet;
        }

        public OdfPivotTableBuilder AddRowField(string fieldName)
        {
            _fields.Add((fieldName, "row", null));
            return this;
        }

        public OdfPivotTableBuilder AddColumnField(string fieldName)
        {
            _fields.Add((fieldName, "column", null));
            return this;
        }

        public OdfPivotTableBuilder AddDataField(string fieldName, string function = "sum")
        {
            _fields.Add((fieldName, "data", function));
            return this;
        }

        public OdfPivotTableBuilder AddPageField(string fieldName)
        {
            _fields.Add((fieldName, "page", null));
            return this;
        }

        public OdfNode Build()
        {
            OdfNode? tablesContainer = null;
            foreach (var child in _sheet.TableNode.Children)
            {
                if (child.LocalName == "data-pilot-tables" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    tablesContainer = child;
                    break;
                }
            }
            if (tablesContainer == null)
            {
                tablesContainer = new OdfNode(OdfNodeType.Element, "data-pilot-tables", OdfNamespaces.Table, "table");
                _sheet.TableNode.AppendChild(tablesContainer);
            }

            var tableNode = new OdfNode(OdfNodeType.Element, "data-pilot-table", OdfNamespaces.Table, "table");
            tableNode.SetAttribute("name", OdfNamespaces.Table, _name, "table");
            tableNode.SetAttribute("target-range-address", OdfNamespaces.Table, _targetStart.ToOdfString(false), "table");
            tableNode.SetAttribute("buttons", OdfNamespaces.Table, _targetStart.ToOdfString(false), "table");

            var sourceRangeNode = new OdfNode(OdfNodeType.Element, "source-cell-range", OdfNamespaces.Table, "table");
            sourceRangeNode.SetAttribute("cell-range-address", OdfNamespaces.Table, _sourceRange.ToOdfString(false), "table");
            tableNode.AppendChild(sourceRangeNode);

            foreach (var field in _fields)
            {
                var fieldNode = new OdfNode(OdfNodeType.Element, "data-pilot-field", OdfNamespaces.Table, "table");
                fieldNode.SetAttribute("source-field-name", OdfNamespaces.Table, field.name, "table");
                fieldNode.SetAttribute("orientation", OdfNamespaces.Table, field.orientation, "table");
                if (field.orientation == "data" && !string.IsNullOrEmpty(field.function))
                {
                    fieldNode.SetAttribute("function", OdfNamespaces.Table, field.function!, "table");
                }
                tableNode.AppendChild(fieldNode);
            }

            tablesContainer.AppendChild(tableNode);
            return tableNode;
        }
    }
}
