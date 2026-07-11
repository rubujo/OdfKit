using System;
using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Provides high-level lifecycle operations for embedded formula objects.
/// 提供嵌入公式物件的高階生命週期操作。
/// </summary>
public partial class TextDocument
{
    /// <summary>
    /// Gets all embedded formula objects in document order.
    /// 依文件順序取得所有嵌入公式物件。
    /// </summary>
    /// <returns>The formula object list. / 公式物件清單。</returns>
    public IReadOnlyList<OdfFormulaObject> GetFormulaObjects()
    {
        var formulas = new List<OdfFormulaObject>();
        CollectFormulaObjects(BodyTextRoot, formulas);
        return formulas.AsReadOnly();
    }

    /// <summary>
    /// Finds a formula object by its frame name or package folder.
    /// 依外框名稱或封裝包資料夾尋找公式物件。
    /// </summary>
    /// <param name="identifier">The exact frame name or package folder. / 精確的外框名稱或封裝包資料夾。</param>
    /// <returns>The matching formula object, or <see langword="null"/>. / 相符的公式物件；若不存在則為 <see langword="null"/>。</returns>
    public OdfFormulaObject? FindFormulaObject(string identifier)
    {
        foreach (OdfFormulaObject formula in GetFormulaObjects())
        {
            if (string.Equals(formula.Name, identifier, StringComparison.Ordinal) ||
                string.Equals(NormalizeObjectFolder(formula.FormulaFolder), NormalizeObjectFolder(identifier), StringComparison.Ordinal))
            {
                return formula;
            }
        }
        return null;
    }

    /// <summary>
    /// Removes a formula object and its unreferenced package subdocument.
    /// 移除公式物件及其未被參照的封裝包子文件。
    /// </summary>
    /// <param name="identifier">The exact frame name or package folder. / 精確的外框名稱或封裝包資料夾。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveFormulaObject(string identifier)
    {
        OdfFormulaObject? formula = FindFormulaObject(identifier);
        if (formula?.FrameNode.Parent is null)
            return false;
        string folder = NormalizeObjectFolder(formula.FormulaFolder);
        formula.FrameNode.Parent.RemoveChild(formula.FrameNode);
        if (folder.Length > 0 && !HasFormulaFolderReference(folder))
            RemoveFormulaPackage(folder);
        return true;
    }

    /// <summary>
    /// Removes all formula objects and unreferenced formula subdocuments.
    /// 移除所有公式物件與未被參照的公式子文件。
    /// </summary>
    /// <returns>The number of removed formula objects. / 已移除的公式物件數量。</returns>
    public int ClearFormulaObjects()
    {
        List<OdfFormulaObject> formulas = [.. GetFormulaObjects()];
        int removed = 0;
        foreach (OdfFormulaObject formula in formulas)
        {
            string identifier = formula.Name ?? formula.FormulaFolder ?? string.Empty;
            if (RemoveFormulaObject(identifier))
                removed++;
        }
        return removed;
    }

    private void CollectFormulaObjects(OdfNode root, List<OdfFormulaObject> formulas)
    {
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw)
            {
                OdfNode? objectNode = FindDirectObject(child);
                string folder = NormalizeObjectFolder(objectNode?.GetAttribute("href", OdfNamespaces.XLink));
                if (objectNode is not null && IsFormulaPackageFolder(folder))
                    formulas.Add(new OdfFormulaObject(child, objectNode, this));
            }
            CollectFormulaObjects(child, formulas);
        }
    }

    private bool IsFormulaPackageFolder(string folder)
    {
        if (folder.Length == 0 || !Package.HasEntry(folder + "/mimetype"))
            return false;
        byte[]? bytes = Package.ReadEntry(folder + "/mimetype");
        return bytes is not null && string.Equals(
            Encoding.UTF8.GetString(bytes),
            "application/vnd.oasis.opendocument.formula",
            StringComparison.Ordinal);
    }

    private bool HasFormulaFolderReference(string folder)
    {
        foreach (OdfFormulaObject formula in GetFormulaObjects())
        {
            if (string.Equals(NormalizeObjectFolder(formula.FormulaFolder), folder, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private void RemoveFormulaPackage(string folder)
    {
        string prefix = folder + "/";
        var entries = new List<string>();
        foreach (string entry in Package.Entries.Keys)
        {
            if (entry.StartsWith(prefix, StringComparison.Ordinal))
                entries.Add(entry);
        }
        foreach (string entry in entries)
            Package.RemoveEntry(entry);
        Package.RemoveEntry(folder + "/");
        Package.SaveManifestToEntries();
    }

    private static OdfNode? FindDirectObject(OdfNode frame)
    {
        foreach (OdfNode child in frame.Children)
        {
            if (child.LocalName == "object" && child.NamespaceUri == OdfNamespaces.Draw)
                return child;
        }
        return null;
    }

    private static string NormalizeObjectFolder(string? folder) =>
        (folder ?? string.Empty).Replace('\\', '/').Trim().TrimStart('.').Trim('/');
}
