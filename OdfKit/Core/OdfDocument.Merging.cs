using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Document Merging API


    /// <summary>
    /// 將另一份 ODF 文件附加到目前文件。
    /// </summary>
    /// <param name="otherDoc">要附加的來源文件。</param>
    /// <param name="options">合併選項。</param>
    public virtual void AppendDocument(OdfDocument otherDoc, OdfMergeOptions? options = null)
    {
        options ??= OdfMergeOptions.Default;
        if (otherDoc == null)
            throw new ArgumentNullException(nameof(otherDoc));

        var styleRenameMap = new Dictionary<string, string>(StringComparer.Ordinal);

        if (options.ImportStyles)
        {
            MergeStyles(otherDoc, options, styleRenameMap);
        }

        MergeContentNodes(otherDoc, options, styleRenameMap);
    }


    #endregion
}
