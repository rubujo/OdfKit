using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula;

namespace OdfKit.Text;

/// <summary>
/// 表示 ODF 文件的郵件合併引擎。
/// </summary>
/// <param name="doc">目標文字文件</param>
public partial class OdfMailMergeEngine(TextDocument doc)
{
    private readonly TextDocument _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    private readonly Dictionary<(Type, string), PropertyInfo?> _propertyCache = [];

    /// <summary>
    /// 執行郵件合併作業。
    /// </summary>
    /// <param name="root">郵件合併的根節點</param>
    /// <param name="dataSource">用來合併的資料來源物件</param>
    public void Execute(OdfNode root, object dataSource)
    {
        Execute(root, dataSource, null);
    }

    /// <summary>
    /// 執行郵件合併作業並傳回報告，支援 options 設定。
    /// </summary>
    /// <param name="root">郵件合併的根節點</param>
    /// <param name="dataSource">用來合併的資料來源物件</param>
    /// <param name="options">郵件合併選項，若為 null 則使用預設值。</param>
    /// <returns>郵件合併執行報告。</returns>
    public OdfMailMergeReport Execute(OdfNode root, object dataSource, OdfMailMergeOptions? options)
    {
        if (root is null)
            throw new ArgumentNullException(nameof(root));

        options ??= new OdfMailMergeOptions();
        var report = new OdfMailMergeReport();

        if (dataSource is null)
            return report;

        // 1. 遞迴處理 TableStart / TableEnd 區域展開
        ProcessRegions(root, dataSource, options, 0, null, report);

        // 2. 替換剩餘的一般預留位置
        ReplacePlaceholdersWithReport(root, dataSource, null, report);

        return report;
    }

    private void ProcessRegions(OdfNode parent, object dataSource, OdfMailMergeOptions options, int depth, object? parentDataSource, OdfMailMergeReport report)
    {
        if (depth > options.MaxNestingDepth)
            return;

        // 兄弟節點掃描與區域展開
        for (int i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            string content = child.TextContent;
            int startTokenIdx = content.IndexOf(options.RegionStartToken);
            if (startTokenIdx != -1)
            {
                int endTokenIdx = content.IndexOf("}}", startTokenIdx);
                if (endTokenIdx != -1)
                {
                    string regionName = content.Substring(startTokenIdx + options.RegionStartToken.Length, endTokenIdx - (startTokenIdx + options.RegionStartToken.Length)).Trim();
                    int startIndex = i;
                    int endIndex = -1;

                    // 尋找對應的 TableEnd 兄弟節點
                    string endToken = options.RegionEndToken + regionName + "}}";
                    for (int j = startIndex + 1; j < parent.Children.Count; j++)
                    {
                        if (parent.Children[j].TextContent.Contains(endToken))
                        {
                            endIndex = j;
                            break;
                        }
                    }

                    if (endIndex != -1)
                    {
                        // 收集模板節點
                        var templateNodes = new List<OdfNode>();
                        for (int k = startIndex + 1; k < endIndex; k++)
                        {
                            templateNodes.Add(parent.Children[k]);
                        }

                        // 取得資料來源中該 regionName 的資料
                        bool resolved = TryResolveValuePath(dataSource, regionName, out object? val);
                        if (!resolved && parentDataSource is not null)
                        {
                            resolved = TryResolveValuePath(parentDataSource, regionName, out val);
                        }

                        var collection = val as IEnumerable;
                        if (resolved && collection is not null && collection is not string)
                        {
                            // 先從 parent 移除起點、模板與終點節點
                            parent.RemoveChild(parent.Children[startIndex]);
                            for (int k = 0; k < templateNodes.Count + 1; k++)
                            {
                                parent.RemoveChild(parent.Children[startIndex]);
                            }

                            int currentInsertIdx = startIndex;
                            foreach (var item in collection)
                            {
                                var clonedNodes = new List<OdfNode>();
                                foreach (var tNode in templateNodes)
                                {
                                    clonedNodes.Add(tNode.CloneNode(true));
                                }

                                // 建立一個虛擬的臨時父節點來包裝 clonedNodes，以利內層兄弟區段嵌套展開
                                var tempParent = OdfNodeFactory.CreateElement("temp-container", OdfNamespaces.Text, "text");
                                foreach (var clonedNode in clonedNodes)
                                {
                                    tempParent.AppendChild(clonedNode);
                                }

                                // 對臨時父節點進行內層的遞迴展開與置換
                                ProcessRegions(tempParent, item, options, depth + 1, dataSource, report);
                                ReplacePlaceholdersWithReport(tempParent, item, dataSource, report);

                                // 將展開後的所有子節點移回真正的 parent 中
                                var finalNodes = tempParent.Children.ToList();
                                foreach (var finalNode in finalNodes)
                                {
                                    tempParent.RemoveChild(finalNode);
                                    parent.Children.Insert(currentInsertIdx++, finalNode);
                                    finalNode.Parent = parent;
                                }
                            }

                            // 重設 i 到最新插入位置的後一個節點
                            i = currentInsertIdx - 1;
                            continue;
                        }
                        else
                        {
                            // 找不到資料或資料無效，直接清空該區域，並記錄為未解析
                            report.UnresolvedPlaceholders.Add(regionName);

                            parent.RemoveChild(parent.Children[startIndex]);
                            for (int k = 0; k < templateNodes.Count + 1; k++)
                            {
                                parent.RemoveChild(parent.Children[startIndex]);
                            }

                            i = startIndex - 1;
                            continue;
                        }
                    }
                }
            }

            // 遞迴子節點
            ProcessRegions(child, dataSource, options, depth, parentDataSource, report);
        }
    }
}
