using System;
using System.IO;
using OdfKit.Spreadsheet;
using Sylvan.Data.Csv;

namespace OdfKit.Csv;

/// <summary>
/// 將 CSV 資料匯入為 SpreadsheetDocument 的靜態工具類別。
/// </summary>
public static class OdfCsvImporter
{
    /// <summary>
    /// 從資料流讀取 CSV 並建立新的 SpreadsheetDocument
    /// </summary>
    /// <param name="csvStream">CSV 來源資料流（不得為 null）。</param>
    /// <param name="options">CSV 選項；若為 null 則使用預設值。</param>
    /// <returns>包含 CSV 資料的新 SpreadsheetDocument 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 csvStream 為 null 時引發。</exception>
    public static SpreadsheetDocument ImportFromStream(Stream csvStream, OdfCsvOptions? options = null)
    {
        if (csvStream is null) throw new ArgumentNullException(nameof(csvStream));
        options ??= new OdfCsvOptions();

        var workbook = SpreadsheetDocument.Create();
        var sheet = workbook.Worksheets.Add(options.SheetName);

        var csvOptions = new CsvDataReaderOptions
        {
            HasHeaders = options.HasHeaders,
            Delimiter = options.Delimiter
        };

        using var textReader = new StreamReader(csvStream, options.Encoding, true, 4096, leaveOpen: true);
        using var csvReader = CsvDataReader.Create(textReader, csvOptions);

        int row = 0;
        if (options.HasHeaders)
        {
            for (int col = 0; col < csvReader.FieldCount; col++)
            {
                sheet.Cells[0, col].CellValue = csvReader.GetName(col);
            }
            row = 1;
        }

        while (csvReader.Read())
        {
            for (int col = 0; col < csvReader.FieldCount; col++)
            {
                sheet.Cells[row, col].CellValue = csvReader.GetString(col);
            }
            row++;
        }

        return workbook;
    }

    /// <summary>
    /// 從檔案路徑讀取 CSV 並建立新的 SpreadsheetDocument。
    /// </summary>
    /// <param name="csvPath">CSV 檔案路徑。</param>
    /// <param name="options">CSV 選項；若為 null 則使用預設值。</param>
    /// <returns>包含 CSV 資料的新 SpreadsheetDocument 執行個體。</returns>
    public static SpreadsheetDocument ImportFromFile(string csvPath, OdfCsvOptions? options = null)
    {
        if (csvPath is null) throw new ArgumentNullException(nameof(csvPath));
        using var stream = File.OpenRead(csvPath);
        return ImportFromStream(stream, options);
    }
}
