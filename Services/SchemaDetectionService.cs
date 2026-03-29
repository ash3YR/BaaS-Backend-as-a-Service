using System.Globalization;
using BaaS.Models;

namespace BaaS.Services;

public class SchemaDetectionService
{
    public List<SchemaColumnDefinition> DetectSchema(CsvUploadResponse parsedCsv)
    {
        return parsedCsv.Columns
            .Select(column => new SchemaColumnDefinition
            {
                Name = column,
                Type = DetectColumnType(parsedCsv.SampleData, column)
            })
            .ToList();
    }

    private static string DetectColumnType(IEnumerable<Dictionary<string, string>> rows, string column)
    {
        var values = rows
            .Select(row => row.TryGetValue(column, out var value) ? value : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        if (values.Count == 0)
        {
            return "TEXT";
        }

        if (values.All(IsInteger))
        {
            return "INTEGER";
        }

        if (values.All(IsDouble))
        {
            return "DOUBLE";
        }

        if (values.All(IsBoolean))
        {
            return "BOOLEAN";
        }

        if (values.All(IsTimestamp))
        {
            return "TIMESTAMP";
        }

        return "TEXT";
    }

    private static bool IsInteger(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsBoolean(string value)
    {
        return bool.TryParse(value, out _);
    }

    private static bool IsTimestamp(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
