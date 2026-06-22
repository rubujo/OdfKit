using System.Text.Json;
using OdfKit.Text;

namespace OdfKit.Collaboration;

/// <summary>
/// 將 ODF Toolkit 相容的 JSON operations 序列單向 merge 至 <see cref="TextDocument"/>。
/// </summary>
/// <remarks>
/// 對應 <see cref="OdtOperationsExporter"/> 所能匯出的 <c>addParagraph</c>／<c>addText</c>／<c>addTab</c>
/// 子集合；操作依陣列順序依序重播（單向 merge），不處理任意位置插入或刪除，亦不解析其他
/// operations 名稱（未知 operation 會被忽略，以容忍前向相容的擴充欄位）。
/// </remarks>
public static class OdtOperationsImporter
{
    /// <summary>
    /// 將 JSON operations 陣列字串重播至新建立的文字文件。
    /// </summary>
    /// <param name="operationsJson">JSON operations 陣列字串</param>
    /// <returns>套用 operations 後的文字文件</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="operationsJson"/> 為 null 時擲出</exception>
    public static TextDocument Merge(string operationsJson)
    {
        TextDocument document = TextDocument.Create();
        Merge(document, operationsJson);
        return document;
    }

    /// <summary>
    /// 將 JSON operations 陣列字串重播至既有的文字文件結尾。
    /// </summary>
    /// <param name="document">目標文字文件</param>
    /// <param name="operationsJson">JSON operations 陣列字串</param>
    /// <exception cref="ArgumentNullException">當任一參數為 null 時擲出</exception>
    public static void Merge(TextDocument document, string operationsJson)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (operationsJson is null)
        {
            throw new ArgumentNullException(nameof(operationsJson));
        }

        using JsonDocument parsed = JsonDocument.Parse(operationsJson);
        OdfParagraph? currentParagraph = null;

        foreach (JsonElement operation in parsed.RootElement.EnumerateArray())
        {
            string? name = operation.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString()
                : null;

            switch (name)
            {
                case "addParagraph":
                    currentParagraph = document.AddParagraph();
                    if (operation.TryGetProperty("attrs", out JsonElement attrs) &&
                        attrs.ValueKind == JsonValueKind.Object &&
                        attrs.TryGetProperty("styleName", out JsonElement styleNameElement))
                    {
                        currentParagraph.StyleName = styleNameElement.GetString();
                    }

                    break;

                case "addText":
                    if (currentParagraph is not null &&
                        operation.TryGetProperty("text", out JsonElement textElement))
                    {
                        string? text = textElement.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            currentParagraph.AddTextRun(text!);
                        }
                    }

                    break;

                case "addTab":
                    currentParagraph?.AddTab();
                    break;

                default:
                    // 未知 operation：容忍前向相容的擴充欄位，略過不處理。
                    break;
            }
        }
    }
}
