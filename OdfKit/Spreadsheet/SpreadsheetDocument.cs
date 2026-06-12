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
                mapNode = OdfNodeFactory.CreateElement("config-item-map-named", OdfNamespaces.Config, "config");
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
                entry = OdfNodeFactory.CreateElement("config-item-map-entry", OdfNamespaces.Config, "config");
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

        public bool VerifyWorkbookPassword(string password)
        {
            if (!WorkbookStructureProtected) return true;

            var docSettings = FindSettingsNode(SettingsDom, "document-settings");
            if (docSettings == null) return false;

            OdfNode? mapNode = null;
            foreach (var child in docSettings.Children)
            {
                if (child.LocalName == "config-item-map-named" && child.GetAttribute("name", OdfNamespaces.Config) == "WorkbookSettings")
                {
                    mapNode = child;
                    break;
                }
            }
            if (mapNode == null || mapNode.Children.Count == 0) return false;
            var entry = mapNode.Children[0];

            string? keyStr = FindConfigItemValue(entry, "WorkbookProtectionKey");
            string? algo = FindConfigItemValue(entry, "WorkbookProtectionKeyDigestAlgorithm");
            string? saltStr = FindConfigItemValue(entry, "WorkbookProtectionKeyDigestSalt");

            if (keyStr == null || algo == null || saltStr == null) return false;

            byte[] salt = Convert.FromBase64String(saltStr);
            byte[] expectedHash = Convert.FromBase64String(keyStr);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

            byte[] input = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

            byte[] actualHash;
            if (algo == "http://www.w3.org/2001/04/xmlenc#sha256")
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    actualHash = sha.ComputeHash(input);
                }
            }
            else
            {
                return false;
            }

            return CompareBytes(expectedHash, actualHash);
        }

        private string? FindConfigItemValue(OdfNode entry, string name)
        {
            foreach (var child in entry.Children)
            {
                if (child.LocalName == "config-item" && child.GetAttribute("name", OdfNamespaces.Config) == name)
                {
                    return child.TextContent;
                }
            }
            return null;
        }

        private static bool CompareBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
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

        public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
        {
            var namedExpressions = FindOrCreateChild(SheetsRoot, "named-expressions", OdfNamespaces.Table, "table");
            var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
            namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
            if (baseCell.HasValue)
            {
                namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
            }
            namedExpressions.AppendChild(namedRange);
        }

        public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
        {
            var namedExpressions = FindOrCreateChild(SheetsRoot, "named-expressions", OdfNamespaces.Table, "table");
            var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
            namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
            namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
            if (baseCell.HasValue)
            {
                namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
            }
            namedExpressions.AppendChild(namedExpr);
        }

        public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range)
        {
            var databaseRanges = FindOrCreateChild(SheetsRoot, "database-ranges", OdfNamespaces.Table, "table");
            var dbRangeNode = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
            dbRangeNode.SetAttribute("name", OdfNamespaces.Table, name, "table");
            dbRangeNode.SetAttribute("target-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
            databaseRanges.AppendChild(dbRangeNode);
            return new OdfDatabaseRange(dbRangeNode, this);
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

        public bool VerifyPassword(string password)
        {
            if (!IsProtected) return true;

            string? keyStr = TableNode.GetAttribute("protection-key", OdfNamespaces.Table);
            string? algo = TableNode.GetAttribute("protection-key-digest-algorithm", OdfNamespaces.Table);
            string? saltStr = TableNode.GetAttribute("protection-key-digest-salt", OdfNamespaces.Table);

            if (keyStr == null || algo == null || saltStr == null) return false;

            byte[] salt = Convert.FromBase64String(saltStr);
            byte[] expectedHash = Convert.FromBase64String(keyStr);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

            byte[] input = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

            byte[] actualHash;
            if (algo == "http://www.w3.org/2000/09/xmldsig#sha256")
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    actualHash = sha.ComputeHash(input);
                }
            }
            else
            {
                return false;
            }

            return CompareBytes(expectedHash, actualHash);
        }

        private static bool CompareBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
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
            var startAddr = range.StartAddress;
            if (startAddr.SheetName == null) startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, Name, startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
            var endAddr = range.EndAddress;
            if (endAddr.SheetName == null) endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, Name, endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);

            string rangeAddr = $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}";
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

        private OdfNode SplitRepeatedRow(OdfNode rowNode, int targetRowIndex, int currentRowIndex, int repeatedCount)
        {
            int beforeCount = targetRowIndex - currentRowIndex;
            int afterCount = (currentRowIndex + repeatedCount) - (targetRowIndex + 1);

            OdfNode targetRowNode = rowNode;

            if (beforeCount > 0)
            {
                var beforeRow = rowNode.CloneNode(true);
                if (beforeCount > 1)
                    beforeRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
                else
                    beforeRow.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
                TableNode.InsertBefore(beforeRow, rowNode);
            }

            if (afterCount > 0)
            {
                var afterRow = rowNode.CloneNode(true);
                if (afterCount > 1)
                    afterRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
                else
                    afterRow.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
                TableNode.InsertAfter(afterRow, rowNode);
            }

            targetRowNode.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
            return targetRowNode;
        }

        private OdfNode SplitRepeatedCell(OdfNode cellNode, int targetColIndex, int currentColIndex, int repeatedCount, OdfNode rowNode)
        {
            int beforeCount = targetColIndex - currentColIndex;
            int afterCount = (currentColIndex + repeatedCount) - (targetColIndex + 1);

            OdfNode targetCellNode = cellNode;

            if (beforeCount > 0)
            {
                var beforeCell = cellNode.CloneNode(true);
                if (beforeCount > 1)
                    beforeCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
                else
                    beforeCell.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
                rowNode.InsertBefore(beforeCell, cellNode);
            }

            if (afterCount > 0)
            {
                var afterCell = cellNode.CloneNode(true);
                if (afterCount > 1)
                    afterCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
                else
                    afterCell.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
                rowNode.InsertAfter(afterCell, cellNode);
            }

            targetCellNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            return targetCellNode;
        }

        private OdfNode SplitRepeatedColumn(OdfNode colNode, int targetColIndex, int currentColIndex, int repeatedCount)
        {
            int beforeCount = targetColIndex - currentColIndex;
            int afterCount = (currentColIndex + repeatedCount) - (targetColIndex + 1);

            OdfNode targetColNode = colNode;

            if (beforeCount > 0)
            {
                var beforeCol = colNode.CloneNode(true);
                if (beforeCount > 1)
                    beforeCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
                else
                    beforeCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
                TableNode.InsertBefore(beforeCol, colNode);
            }

            if (afterCount > 0)
            {
                var afterCol = colNode.CloneNode(true);
                if (afterCount > 1)
                    afterCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
                else
                    afterCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
                TableNode.InsertAfter(afterCol, colNode);
            }

            targetColNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            return targetColNode;
        }

        private OdfNode GetOrCreateRowNodeInternal(int row, bool forWrite)
        {
            int currentRowIndex = 0;
            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    int repeatedCount = 1;
                    string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    {
                        repeatedCount = rc;
                    }

                    if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                    {
                        if (forWrite && repeatedCount > 1)
                        {
                            return SplitRepeatedRow(child, row, currentRowIndex, repeatedCount);
                        }
                        return child;
                    }
                    currentRowIndex += repeatedCount;
                }
            }

            OdfNode? lastRow = null;
            while (currentRowIndex <= row)
            {
                lastRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
                TableNode.AppendChild(lastRow);
                currentRowIndex++;
            }
            return lastRow!;
        }

        private OdfNode GetOrCreateCellNodeInternal(OdfNode rowNode, int col, bool forWrite)
        {
            int currentColIndex = 0;
            foreach (var child in rowNode.Children)
            {
                if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                {
                    int repeatedCount = 1;
                    string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    {
                        repeatedCount = rc;
                    }

                    if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                    {
                        if (forWrite && repeatedCount > 1)
                        {
                            return SplitRepeatedCell(child, col, currentColIndex, repeatedCount, rowNode);
                        }
                        return child;
                    }
                    currentColIndex += repeatedCount;
                }
            }

            OdfNode? lastCell = null;
            while (currentColIndex <= col)
            {
                lastCell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
                rowNode.AppendChild(lastCell);
                currentColIndex++;
            }
            return lastCell!;
        }

        private OdfNode GetOrCreateCellNode(int row, int col)
        {
            var rowNode = GetOrCreateRowNodeInternal(row, forWrite: true);
            return GetOrCreateCellNodeInternal(rowNode, col, forWrite: true);
        }

        private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
        {
            var rowNode = GetOrCreateRowNodeInternal(row, forWrite: true);
            var oldCell = GetOrCreateCellNodeInternal(rowNode, col, forWrite: true);
            rowNode.InsertBefore(newCellNode, oldCell);
            rowNode.RemoveChild(oldCell);
        }

        private OdfNode GetOrCreateColumnNode(int col)
        {
            int currentColIndex = 0;
            OdfNode? insertBeforeNode = null;
            var cols = new List<OdfNode>();

            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    cols.Add(child);
                    int repeatedCount = 1;
                    string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    {
                        repeatedCount = rc;
                    }

                    if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                    {
                        if (repeatedCount > 1)
                        {
                            return SplitRepeatedColumn(child, col, currentColIndex, repeatedCount);
                        }
                        return child;
                    }
                    currentColIndex += repeatedCount;
                }
                else if (cols.Count > 0 && insertBeforeNode == null)
                {
                    insertBeforeNode = child;
                }
            }

            OdfNode? lastCol = null;
            while (currentColIndex <= col)
            {
                lastCol = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
                if (insertBeforeNode != null)
                {
                    TableNode.InsertBefore(lastCol, insertBeforeNode);
                }
                else
                {
                    TableNode.AppendChild(lastCol);
                }
                currentColIndex++;
            }
            return lastCol!;
        }

        private OdfNode GetOrCreateRowNode(int row)
        {
            return GetOrCreateRowNodeInternal(row, forWrite: true);
        }

        public void SetRowVisible(int row, bool visible)
        {
            var rowNode = GetOrCreateRowNode(row);
            rowNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
        }

        public void SetColumnVisible(int col, bool visible)
        {
            var colNode = GetOrCreateColumnNode(col);
            colNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
        }

        public bool IsRowVisible(int row)
        {
            int currentRowIndex = 0;
            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    int repeatedCount = 1;
                    string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    {
                        repeatedCount = rc;
                    }

                    if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                    {
                        return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                    }
                    currentRowIndex += repeatedCount;
                }
            }
            return true;
        }

        public bool IsColumnVisible(int col)
        {
            int currentColIndex = 0;
            foreach (var child in TableNode.Children)
            {
                if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                {
                    int repeatedCount = 1;
                    string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                    {
                        repeatedCount = rc;
                    }

                    if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                    {
                        return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                    }
                    currentColIndex += repeatedCount;
                }
            }
            return true;
        }

        public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
        {
            var namedExpressions = FindOrCreateChild(TableNode, "named-expressions", OdfNamespaces.Table, "table");
            var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
            namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
            namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
            if (baseCell.HasValue)
            {
                namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
            }
            namedExpressions.AppendChild(namedRange);
        }

        public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
        {
            var namedExpressions = FindOrCreateChild(TableNode, "named-expressions", OdfNamespaces.Table, "table");
            var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
            namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
            namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
            if (baseCell.HasValue)
            {
                namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
            }
            namedExpressions.AppendChild(namedExpr);
        }

        private OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
        {
            foreach (var child in parent.Children)
            {
                if (child.LocalName == localName && child.NamespaceUri == ns)
                    return child;
            }
            var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
            parent.AppendChild(node);
            return node;
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

        public void AddConditionalFormatMap(string condition, string applyStyleName, OdfCellAddress? baseCell = null)
        {
            var styleNode = _doc.StyleEngine.GetOrCreateLocalStyle(Node, "table-cell");
            var mapNode = new OdfNode(OdfNodeType.Element, "map", OdfNamespaces.Style, "style");
            mapNode.SetAttribute("condition", OdfNamespaces.Style, condition, "style");
            mapNode.SetAttribute("apply-style-name", OdfNamespaces.Style, applyStyleName, "style");
            if (baseCell.HasValue)
            {
                mapNode.SetAttribute("base-cell-address", OdfNamespaces.Style, baseCell.Value.ToOdfString(false), "style");
            }
            styleNode.AppendChild(mapNode);
        }

        private void SetStyleProperty(string propertiesElement, string propertyAttr, string propertyNs, string value, string propertyPrefix)
        {
            _doc.StyleEngine.SetLocalStyleProperty(Node, "table-cell", propertiesElement, propertyAttr, propertyNs, value, propertyPrefix);
        }
    }
}
