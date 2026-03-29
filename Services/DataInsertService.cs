using System.Data;
using System.Globalization;
using BaaS.Data;
using BaaS.Models;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Services;

public class DataInsertService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DataInsertService> _logger;

    public DataInsertService(ApplicationDbContext dbContext, ILogger<DataInsertService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> InsertRowsAsync(
        string tableName,
        IEnumerable<string> columns,
        IEnumerable<SchemaColumnDefinition> schema,
        IEnumerable<Dictionary<string, string>> rows,
        CancellationToken cancellationToken = default)
    {
        var safeTableName = SqlIdentifierSanitizer.Sanitize(tableName, "table name").ToLowerInvariant();
        var safeColumns = columns
            .Select(column => SqlIdentifierSanitizer.Sanitize(column, "column name").ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var schemaMap = schema.ToDictionary(
            column => SqlIdentifierSanitizer.Sanitize(column.Name, "column name").ToLowerInvariant(),
            column => column.Type,
            StringComparer.OrdinalIgnoreCase);

        var rowList = rows.ToList();
        if (safeColumns.Count == 0 || rowList.Count == 0)
        {
            return 0;
        }

        try
        {
            var connection = _dbContext.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = BuildInsertSql(safeTableName, safeColumns, rowList.Count);

            var parameterIndex = 0;
            for (var rowIndex = 0; rowIndex < rowList.Count; rowIndex++)
            {
                foreach (var column in safeColumns)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = $"@p{parameterIndex++}";

                    rowList[rowIndex].TryGetValue(column, out var value);
                    schemaMap.TryGetValue(column, out var detectedType);
                    parameter.Value = ConvertValue(value, detectedType);

                    command.Parameters.Add(parameter);
                }
            }

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Data insertion failed for table {TableName}.", safeTableName);
            throw;
        }
    }

    private static string BuildInsertSql(string tableName, IReadOnlyList<string> columns, int rowCount)
    {
        var quotedColumns = string.Join(", ", columns.Select(column => $"\"{column}\""));
        var valueGroups = new List<string>();
        var parameterIndex = 0;

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var parameters = new List<string>();

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                parameters.Add($"@p{parameterIndex++}");
            }

            valueGroups.Add($"({string.Join(", ", parameters)})");
        }

        return $"INSERT INTO \"{tableName}\" ({quotedColumns}) VALUES {string.Join(", ", valueGroups)};";
    }

    private static object ConvertValue(string? rawValue, string? detectedType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return DBNull.Value;
        }

        var value = rawValue.Trim();
        var normalizedType = detectedType?.ToUpperInvariant() ?? "TEXT";

        return normalizedType switch
        {
            "INTEGER" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) => intValue,
            "DOUBLE" when double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue) => doubleValue,
            "BOOLEAN" when bool.TryParse(value, out var boolValue) => boolValue,
            "TIMESTAMP" when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out var roundtripDateTime) => roundtripDateTime,
            "TIMESTAMP" when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var fallbackDateTime) => fallbackDateTime,
            _ => value
        };
    }
}
