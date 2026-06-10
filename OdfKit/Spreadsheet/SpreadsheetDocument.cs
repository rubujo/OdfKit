using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet
{
    public class SpreadsheetDocument : OdfDocument
    {
        public OdfNode SheetsRoot { get; private set; } = null!;

        public SpreadsheetDocument(OdfPackage package) : base(package)
        {
            if (string.IsNullOrEmpty(package.MimeType))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
            }
            InitializeSheetsRoot();
        }

        private void InitializeSheetsRoot()
        {
            var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
            SheetsRoot = FindOrCreateChild(body, "spreadsheet", OdfNamespaces.Office, "office");
        }

        protected override string GetDefaultContentXml()
        {
            return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:body><office:spreadsheet></office:spreadsheet></office:body></office:document-content>";
        }

        protected override string GetDefaultStylesXml()
        {
            return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"1.3\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles></office:master-styles></office:document-styles>";
        }

        public OdfTableSheet AddSheet(string name)
        {
            var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
            table.SetAttribute("name", OdfNamespaces.Table, name, "table");
            SheetsRoot.AppendChild(table);
            return new OdfTableSheet(table, this);
        }

        public OdfTableSheet? GetSheet(string name)
        {
            foreach (var child in SheetsRoot.Children)
            {
                if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table &&
                    child.GetAttribute("name", OdfNamespaces.Table) == name)
                {
                    return new OdfTableSheet(child, this);
                }
            }
            return null;
        }

        public IReadOnlyList<OdfTableSheet> GetSheets()
        {
            var list = new List<OdfTableSheet>();
            foreach (var child in SheetsRoot.Children)
            {
                if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    list.Add(new OdfTableSheet(child, this));
                }
            }
            return list;
        }

        public bool WorkbookStructureProtected
        {
            get
            {
                var val = FindSettingsConfigItem("StructureProtected");
                return val != null && val.TextContent == "true";
            }
        }

        public void ProtectWorkbook(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            
            byte[] input = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

            byte[] hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                hash = sha.ComputeHash(input);
            }

            var docSettings = FindOrCreateSettingsNode(SettingsDom, "document-settings");
            
            OdfNode? mapNode = null;
            foreach (var child in docSettings.Children)
            {
                if (child.LocalName == "config-item-map-named" && child.GetAttribute("name", OdfNamespaces.Config) == "WorkbookSettings")
                {
                    mapNode = child;
                    break;
                }
            }
            if (mapNode == null)
            {
                mapNode = new OdfNode(OdfNodeType.Element, "config-item-map-named", OdfNamespaces.Config, "config");
                mapNode.SetAttribute("name", OdfNamespaces.Config, "WorkbookSettings", "config");
                docSettings.AppendChild(mapNode);
            }
            
            OdfNode? entry = null;
            if (mapNode.Children.Count > 0)
            {
                entry = mapNode.Children[0];
            }
            else
            {
                entry = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
                mapNode.AppendChild(entry);
            }

            var itemProt = FindOrCreateConfigItemNode(entry, "StructureProtected", "boolean");
            itemProt.TextContent = "true";

            var itemKey = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKey", "string");
            itemKey.TextContent = Convert.ToBase64String(hash);

            var itemAlgo = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKeyDigestAlgorithm", "string");
            itemAlgo.TextContent = "http://www.w3.org/2001/04/xmlenc#sha256";

            var itemSalt = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKeyDigestSalt", "string");
            itemSalt.TextContent = Convert.ToBase64String(salt);
        }

        protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        {
            var srcSpreadsheet = sourceDoc as SpreadsheetDocument ?? throw new ArgumentException("Source document must be a SpreadsheetDocument.");
            
            foreach (var child in srcSpreadsheet.SheetsRoot.Children)
            {
                if (child.NodeType == OdfNodeType.Element)
                {
                    var imported = OdfNode.ImportNode(child, srcSpreadsheet.Package, Package);
                    RemapStylesInNodes(imported, renameMap);
                    SheetsRoot.AppendChild(imported);
                }
            }
        }
    }

    public class OdfTableSheet
    {
        public OdfNode TableNode { get; }
        private readonly SpreadsheetDocument _doc;

        public OdfTableSheet(OdfNode tableNode, SpreadsheetDocument doc)
        {
            TableNode = tableNode;
            _doc = doc;
        }

        public string Name
        {
            get => TableNode.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
            set => TableNode.SetAttribute("name", OdfNamespaces.Table, value, "table");
        }

        public bool Visible
        {
            get => TableNode.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
            set => TableNode.SetAttribute("visibility", OdfNamespaces.Table, value ? "visible" : "collapse", "table");
        }

        public bool IsProtected
        {
            get => TableNode.GetAttribute("protected", OdfNamespaces.Table) == "true";
        }

        public void Protect(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            
            byte[] input = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

            byte[] hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                hash = sha.ComputeHash(input);
            }

            TableNode.SetAttribute("protected", OdfNamespaces.Table, "true", "table");
            TableNode.SetAttribute("protection-key", OdfNamespaces.Table, Convert.ToBase64String(hash), "table");
            TableNode.SetAttribute("protection-key-digest-algorithm", OdfNamespaces.Table, "http://www.w3.org/2000/09/xmldsig#sha256", "table");
            TableNode.SetAttribute("protection-key-digest-salt", OdfNamespaces.Table, Convert.ToBase64String(salt), "table");
        }

        public OdfCell GetCell(int row, int col)
        {
            var cellNode = GetOrCreateCellNode(row, col);
            return new OdfCell(cellNode, row, col, _doc);
        }

        public OdfCell GetCell(string address)
        {
            if (!OdfCellAddress.TryParse(address, out var addr))
            {
                throw new FormatException($"Invalid cell address: '{address}'");
            }
            return GetCell(addr.Row, addr.Column);
        }

        public void MergeCells(OdfCellRange range, OdfBorder? outerBorder = null)
        {
            int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
            int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
            int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
            int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

            var mainCell = GetCell(startRow, startCol);
            mainCell.Node.SetAttribute("number-columns-spanned", OdfNamespaces.Table, (endCol - startCol + 1).ToString(), "table");
            mainCell.Node.SetAttribute("number-rows-spanned", OdfNamespaces.Table, (endRow - startRow + 1).ToString(), "table");

            for (int r = startRow; r <= endRow; r++)
            {
                for (int c = startCol; c <= endCol; c++)
                {
                    if (r == startRow && c == startCol) continue;
                    
                    var coveredNode = new OdfNode(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
                    ReplaceCellNode(r, c, coveredNode);
                    
                    if (outerBorder.HasValue)
                    {
                        var cellBorderTop = (r == startRow) ? outerBorder.Value : OdfBorder.None;
                        var cellBorderBottom = (r == endRow) ? outerBorder.Value : OdfBorder.None;
                        var cellBorderLeft = (c == startCol) ? outerBorder.Value : OdfBorder.None;
                        var cellBorderRight = (c == endCol) ? outerBorder.Value : OdfBorder.None;
                        
                        var coveredCell = new OdfCell(coveredNode, r, c, _doc);
                        coveredCell.SetBorders(cellBorderTop, cellBorderBottom, cellBorderLeft, cellBorderRight);
                    }
                }
            }
        }

        public void AutoFitColumnWidth(int col)
        {
            var values = new List<string>();
            var rows = GetRowsList();
            foreach (var rowNode in rows)
            {
                var cells = GetCellsInRow(rowNode);
                if (col < cells.Count)
                {
                    values.Add(cells[col].TextContent);
                }
            }

            var optimalWidth = CalculateOptimalColumnWidth(values);
            SetColumnWidth(col, optimalWidth);
        }

        public void SetColumnWidth(int col, OdfLength width)
        {
            var colNode = GetOrCreateColumnNode(col);
            string styleName = _doc.StyleEngine.GetOrCreateLocalStyle(colNode, "table-column").GetAttribute("name", OdfNamespaces.Style) ?? "co1";
            _doc.StyleEngine.SetLocalStyleProperty(colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
        }

        private OdfLength CalculateOptimalColumnWidth(IEnumerable<string> cellValues, double fontSizePt = 10)
        {
            double maxWeight = 0;
            foreach (var value in cellValues)
            {
                double weight = 0;
                foreach (char c in value)
                {
                    weight += (c <= 127) ? 1.0 : 1.85;
                }
                if (weight > maxWeight) maxWeight = weight;
            }

            double totalChars = maxWeight + 1.5;
            double widthInCm = totalChars * (fontSizePt / 10.0) * 0.22;
            return OdfLength.FromCentimeters(Math.Max(widthInCm, 1.0));
        }

        public void AddConditionalFormat(OdfCellRange range, string conditionValue, string styleName)
        {
            const string calcextNs = "urn:org:documentfoundation:names:experimental:calc:xmlns:calcext:1.0";
            
            OdfNode? formatsNode = null;
            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "conditional-formats" && child.NamespaceUri == calcextNs)
                {
                    formatsNode = child;
                    break;
                }
            }
            if (formatsNode == null)
            {
                formatsNode = new OdfNode(OdfNodeType.Element, "conditional-formats", calcextNs, "calcext");
                TableNode.AppendChild(formatsNode);
            }

            var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
            string sheetPrefix = $"{Name}.";
            string rangeAddr = $"{sheetPrefix}{range.StartAddress.ToString()}:{sheetPrefix}{range.EndAddress.ToString()}";
            format.SetAttribute("target-range-address", calcextNs, rangeAddr, "calcext");

            var condition = new OdfNode(OdfNodeType.Element, "condition", calcextNs, "calcext");
            condition.SetAttribute("value", calcextNs, conditionValue, "calcext");
            condition.SetAttribute("style-name", calcextNs, styleName, "calcext");
            format.AppendChild(condition);

            formatsNode.AppendChild(format);
        }

        private List<OdfNode> GetRowsList()
        {
            var list = new List<OdfNode>();
            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                    list.Add(child);
            }
            return list;
        }

        private List<OdfNode> GetCellsInRow(OdfNode rowNode)
        {
            var list = new List<OdfNode>();
            foreach (var child in rowNode.Children)
            {
                if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                    list.Add(child);
            }
            return list;
        }

        private OdfNode GetOrCreateCellNode(int row, int col)
        {
            var rows = GetRowsList();
            while (rows.Count <= row)
            {
                var newRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
                TableNode.AppendChild(newRow);
                rows.Add(newRow);
            }

            var rowNode = rows[row];
            var cells = GetCellsInRow(rowNode);
            while (cells.Count <= col)
            {
                var newCell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                rowNode.AppendChild(newCell);
                cells.Add(newCell);
            }

            return cells[col];
        }

        private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
        {
            var rows = GetRowsList();
            while (rows.Count <= row)
            {
                var newRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
                TableNode.AppendChild(newRow);
                rows.Add(newRow);
            }

            var rowNode = rows[row];
            var cells = GetCellsInRow(rowNode);
            while (cells.Count <= col)
            {
                var newCell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                rowNode.AppendChild(newCell);
                cells.Add(newCell);
            }

            var oldCell = cells[col];
            rowNode.InsertBefore(newCellNode, oldCell);
            rowNode.RemoveChild(oldCell);
        }

        private OdfNode GetOrCreateColumnNode(int col)
        {
            var cols = new List<OdfNode>();
            OdfNode? insertBeforeNode = null;
            
            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cols.Add(child);
                }
                else if (cols.Count > 0 && insertBeforeNode == null)
                {
                    insertBeforeNode = child;
                }
            }

            while (cols.Count <= col)
            {
                var newCol = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
                if (insertBeforeNode != null)
                {
                    TableNode.InsertBefore(newCol, insertBeforeNode);
                }
                else
                {
                    TableNode.AppendChild(newCol);
                }
                cols.Add(newCol);
            }

            return cols[col];
        }
    }

    public class OdfCell
    {
        public OdfNode Node { get; }
        public int Row { get; }
        public int Column { get; }
        private readonly SpreadsheetDocument _doc;

        public OdfCell(OdfNode node, int row, int col, SpreadsheetDocument doc)
        {
            Node = node;
            Row = row;
            Column = col;
            _doc = doc;
        }

        public string ValueType
        {
            get => Node.GetAttribute("value-type", OdfNamespaces.Office) ?? string.Empty;
            set => Node.SetAttribute("value-type", OdfNamespaces.Office, value, "office");
        }

        public string Value
        {
            get => Node.GetAttribute("value", OdfNamespaces.Office) ?? string.Empty;
            set => Node.SetAttribute("value", OdfNamespaces.Office, value, "office");
        }

        public string TextContent
        {
            get
            {
                foreach (var child in Node.Children)
                {
                    if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                    {
                        return child.TextContent;
                    }
                }
                return Node.TextContent;
            }
            set
            {
                SetCellTextContent(value);
            }
        }

        public string Formula
        {
            get => Node.GetAttribute("formula", OdfNamespaces.Table) ?? string.Empty;
            set => Node.SetAttribute("formula", OdfNamespaces.Table, value, "table");
        }

        public void SetValue(double val)
        {
            ValueType = "float";
            Value = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
            TextContent = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public void SetValue(bool val)
        {
            ValueType = "boolean";
            Node.SetAttribute("boolean-value", OdfNamespaces.Office, val ? "true" : "false", "office");
            TextContent = val ? "TRUE" : "FALSE";
        }

        public void SetValue(DateTime date, bool useTimezoneNaive = false)
        {
            ValueType = "date";
            string isoDate;
            if (date == DateTime.MinValue || date == DateTime.MaxValue)
            {
                isoDate = useTimezoneNaive 
                    ? date.ToString("yyyy-MM-ddTHH:mm:ss")
                    : date.ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
            }
            else
            {
                isoDate = useTimezoneNaive 
                    ? date.ToString("yyyy-MM-ddTHH:mm:ss")
                    : date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
            Node.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
            TextContent = isoDate;
        }

        public void SetValue(string text)
        {
            ValueType = "string";
            TextContent = text;
        }

        private void SetCellTextContent(string text)
        {
            var toRemove = new List<OdfNode>();
            foreach (var child in Node.Children)
            {
                if (child.NamespaceUri == OdfNamespaces.Text)
                    toRemove.Add(child);
            }
            foreach (var child in toRemove)
            {
                Node.RemoveChild(child);
            }

            var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            bool needsWrap = false;
            
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '\n')
                {
                    pNode.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                    needsWrap = true;
                    i++;
                }
                else if (text[i] == '\t')
                {
                    pNode.AppendChild(new OdfNode(OdfNodeType.Element, "tab", OdfNamespaces.Text, "text"));
                    i++;
                }
                else if (text[i] == ' ')
                {
                    int spaceCount = 0;
                    while (i < text.Length && text[i] == ' ')
                    {
                        spaceCount++;
                        i++;
                    }

                    if (spaceCount == 1)
                    {
                        pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                    }
                    else
                    {
                        pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                        var sNode = new OdfNode(OdfNodeType.Element, "s", OdfNamespaces.Text, "text");
                        sNode.SetAttribute("c", OdfNamespaces.Text, (spaceCount - 1).ToString(), "text");
                        pNode.AppendChild(sNode);
                    }
                }
                else
                {
                    int start = i;
                    while (i < text.Length && text[i] != '\n' && text[i] != '\t' && text[i] != ' ')
                    {
                        i++;
                    }
                    string segment = text.Substring(start, i - start);
                    pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = segment });
                }
            }

            Node.AppendChild(pNode);

            if (needsWrap)
            {
                SetStyleProperty("table-cell-properties", "wrap-option", OdfNamespaces.Fo, "wrap", "fo");
            }
        }

        public void SetBorders(OdfBorder? top, OdfBorder? bottom, OdfBorder? left, OdfBorder? right)
        {
            if (top.HasValue) SetStyleProperty("table-cell-properties", "border-top", OdfNamespaces.Fo, top.Value.ToString(), "fo");
            if (bottom.HasValue) SetStyleProperty("table-cell-properties", "border-bottom", OdfNamespaces.Fo, bottom.Value.ToString(), "fo");
            if (left.HasValue) SetStyleProperty("table-cell-properties", "border-left", OdfNamespaces.Fo, left.Value.ToString(), "fo");
            if (right.HasValue) SetStyleProperty("table-cell-properties", "border-right", OdfNamespaces.Fo, right.Value.ToString(), "fo");
        }

        private void SetStyleProperty(string propertiesElement, string propertyAttr, string propertyNs, string value, string propertyPrefix)
        {
            _doc.StyleEngine.SetLocalStyleProperty(Node, "table-cell", propertiesElement, propertyAttr, propertyNs, value, propertyPrefix);
        }
    }
}
