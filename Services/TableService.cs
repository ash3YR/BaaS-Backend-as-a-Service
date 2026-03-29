using BaaS.Data;
using BaaS.Models;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Services;

public class TableService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TableService> _logger;

    public TableService(ApplicationDbContext dbContext, ILogger<TableService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<TableCreationResponse> CreateTableAsync(
        IEnumerable<SchemaColumnDefinition> schema,
        CancellationToken cancellationToken = default)
    {
        var schemaList = schema.ToList();

        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return new TableCreationResponse
                {
                    Schema = schemaList,
                    Status = "Database unavailable",
                    Error = "Unable to connect to PostgreSQL. If you are using Supabase, switch from the direct db.<project-ref>.supabase.co host to the Session pooler connection string from the Supabase Connect screen."
                };
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Database connectivity check failed.");

            return new TableCreationResponse
            {
                Schema = schemaList,
                Status = "Database unavailable",
                Error = exception.Message
            };
        }

        try
        {
            var tableName = GenerateTableName();
            var safeColumns = BuildColumnDefinitions(schemaList);
            var createTableSql = BuildCreateTableSql(tableName, safeColumns);

            await _dbContext.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);

            return new TableCreationResponse
            {
                TableName = tableName,
                Schema = schemaList,
                Status = "Table created successfully"
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Table creation failed.");

            return new TableCreationResponse
            {
                Schema = schemaList,
                Status = "Database unavailable",
                Error = exception.Message
            };
        }
    }

    private static string GenerateTableName()
    {
        return $"data_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static List<(string Name, string Type)> BuildColumnDefinitions(IEnumerable<SchemaColumnDefinition> schema)
    {
        var safeColumns = new List<(string Name, string Type)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in schema)
        {
            var normalizedName = SqlIdentifierSanitizer
                .Sanitize(column.Name, "column name")
                .ToLowerInvariant();
            if (!names.Add(normalizedName))
            {
                throw new InvalidOperationException($"Duplicate column name '{column.Name}' is not allowed.");
            }

            safeColumns.Add((normalizedName, MapSqlType(column.Type)));
        }

        return safeColumns;
    }

    private static string BuildCreateTableSql(string tableName, IEnumerable<(string Name, string Type)> columns)
    {
        var safeTableName = SqlIdentifierSanitizer.Sanitize(tableName, "table name");

        var columnSql = string.Join(", ", columns.Select(column => $"\"{column.Name}\" {column.Type}"));
        return $"CREATE TABLE \"{safeTableName}\" (\"Id\" SERIAL PRIMARY KEY, {columnSql});";
    }

    private static string MapSqlType(string detectedType)
    {
        return detectedType.ToUpperInvariant() switch
        {
            "INTEGER" => "INTEGER",
            "DOUBLE" => "DOUBLE PRECISION",
            "BOOLEAN" => "BOOLEAN",
            "TIMESTAMP" => "TIMESTAMP",
            _ => "TEXT"
        };
    }
}
