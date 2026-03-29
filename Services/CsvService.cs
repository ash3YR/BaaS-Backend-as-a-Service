using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using BaaS.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;

namespace BaaS.Services;

public class CsvService
{
    private const int SampleRowLimit = 5;

    public async Task<CsvUploadResponse> ParseCsvAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        return await ParseTabularFileAsync(file, cancellationToken);
    }

    public async Task<CsvUploadResponse> ParseTabularFileAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" or ".txt" => await ParseCsvAsync(stream, cancellationToken),
            ".xlsx" => await ParseExcelAsync(stream, cancellationToken),
            _ => throw new InvalidDataException("Unsupported file format. Please upload a CSV or XLSX file.")
        };
    }

    public async Task<CsvUploadResponse> ParseCsvAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            DetectColumnCountChanges = true,
            TrimOptions = TrimOptions.Trim
        });

        if (!await csv.ReadAsync())
        {
            throw new InvalidDataException("The uploaded CSV file is empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord;

        if (headers is null || headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Invalid CSV. The file must include a valid header row.");
        }

        var result = new CsvUploadResponse
        {
            Columns = headers.ToList()
        };

        while (result.SampleData.Count < SampleRowLimit && await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            result.SampleData.Add(row);
        }

        return result;
    }

    public async Task<CsvUploadResponse> ParseExcelAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Seek(0, SeekOrigin.Begin);

        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("Invalid Excel file. Workbook metadata is missing.");
        var workbookRelsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("Invalid Excel file. Workbook relationships are missing.");

        var workbookDocument = XDocument.Load(workbookEntry.Open());
        var workbookRelationshipsDocument = XDocument.Load(workbookRelsEntry.Open());

        XNamespace spreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        var firstSheet = workbookDocument
            .Descendants(spreadsheetNamespace + "sheet")
            .FirstOrDefault()
            ?? throw new InvalidDataException("Invalid Excel file. No worksheets were found.");

        var relationshipId = firstSheet.Attribute(relationshipNamespace + "id")?.Value
            ?? throw new InvalidDataException("Invalid Excel file. Worksheet relationship is missing.");

        var worksheetTarget = workbookRelationshipsDocument
            .Descendants(packageRelationshipNamespace + "Relationship")
            .FirstOrDefault(relationship => string.Equals(relationship.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal))
            ?.Attribute("Target")?.Value
            ?? throw new InvalidDataException("Invalid Excel file. Worksheet target could not be resolved.");

        var worksheetPath = worksheetTarget.StartsWith("/", StringComparison.Ordinal)
            ? worksheetTarget.TrimStart('/')
            : $"xl/{worksheetTarget.TrimStart('/')}";

        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidDataException("Invalid Excel file. Worksheet data is missing.");

        var sharedStrings = LoadSharedStrings(archive, spreadsheetNamespace);
        var worksheetDocument = XDocument.Load(worksheetEntry.Open());

        var rows = worksheetDocument
            .Descendants(spreadsheetNamespace + "row")
            .Select(row => ParseWorksheetRow(row, sharedStrings, spreadsheetNamespace))
            .Where(row => row.Count > 0)
            .ToList();

        if (rows.Count == 0)
        {
            throw new InvalidDataException("The uploaded Excel file is empty.");
        }

        var headers = rows[0]
            .Select(cell => cell.Trim())
            .ToList();

        if (headers.Count == 0 || headers.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Invalid Excel file. The first row must contain valid column names.");
        }

        var result = new CsvUploadResponse
        {
            Columns = headers
        };

        foreach (var row in rows.Skip(1).Take(SampleRowLimit))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                rowData[headers[index]] = index < row.Count ? row[index] : string.Empty;
            }

            result.SampleData.Add(rowData);
        }

        return result;
    }

    private static List<string> LoadSharedStrings(ZipArchive archive, XNamespace spreadsheetNamespace)
    {
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is null)
        {
            return [];
        }

        var document = XDocument.Load(sharedStringsEntry.Open());
        return document
            .Descendants(spreadsheetNamespace + "si")
            .Select(sharedItem => string.Concat(sharedItem.Descendants(spreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static List<string> ParseWorksheetRow(XElement rowElement, IReadOnlyList<string> sharedStrings, XNamespace spreadsheetNamespace)
    {
        var values = new List<string>();
        var currentColumnIndex = 0;

        foreach (var cell in rowElement.Elements(spreadsheetNamespace + "c"))
        {
            var cellReference = cell.Attribute("r")?.Value;
            var targetColumnIndex = GetColumnIndex(cellReference);

            while (currentColumnIndex < targetColumnIndex)
            {
                values.Add(string.Empty);
                currentColumnIndex++;
            }

            values.Add(ReadCellValue(cell, sharedStrings, spreadsheetNamespace));
            currentColumnIndex++;
        }

        return values;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        var columnReference = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        if (string.IsNullOrWhiteSpace(columnReference))
        {
            return 0;
        }

        var columnIndex = 0;
        foreach (var character in columnReference.ToUpperInvariant())
        {
            columnIndex = (columnIndex * 26) + (character - 'A' + 1);
        }

        return columnIndex - 1;
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace spreadsheetNamespace)
    {
        var dataType = cell.Attribute("t")?.Value;
        var value = cell.Element(spreadsheetNamespace + "v")?.Value
            ?? string.Concat(cell.Descendants(spreadsheetNamespace + "t").Select(text => text.Value));

        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return dataType switch
        {
            "s" when int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count
                => sharedStrings[sharedIndex],
            "inlineStr" => value,
            "b" => value == "1" ? "true" : "false",
            _ => value
        };
    }
}
