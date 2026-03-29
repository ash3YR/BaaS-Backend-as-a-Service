using System.Data;
using System.Text.Json;
using BaaS.Data;
using BaaS.Models;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Services;

public class DynamicDataService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DynamicDataService> _logger;

    public DynamicDataService(ApplicationDbContext dbContext, ILogger<DynamicDataService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DynamicQueryResponse> QueryAsync(
        string tableName,
        DynamicQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var safeTableName = NormalizeTableName(tableName);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 25 : request.PageSize;

        try
        {
            var columns = await GetColumnMetadataAsync(safeTableName, cancellationToken);
            var validColumnNames = columns.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
                ? "Id"
                : NormalizeSortColumn(request.SortBy, validColumnNames);
            var sortDirection = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
            var filters = NormalizeFilters(request.Filters, validColumnNames);

            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var countCommand = connection.CreateCommand();
            var whereClause = BuildWhereClause(countCommand, columns, request.Search, filters);
            countCommand.CommandText = $"SELECT COUNT(*) FROM \"{safeTableName}\"{whereClause};";
            var totalRows = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

            await using var dataCommand = connection.CreateCommand();
            var dataWhereClause = BuildWhereClause(dataCommand, columns, request.Search, filters);
            AddParameter(dataCommand, "@offset", (page - 1) * pageSize);
            AddParameter(dataCommand, "@limit", pageSize);
            dataCommand.CommandText =
                $"SELECT * FROM \"{safeTableName}\"{dataWhereClause} ORDER BY \"{sortBy}\" {sortDirection} OFFSET @offset LIMIT @limit;";

            await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
            var data = await ReadRowsAsync(reader, cancellationToken);

            return new DynamicQueryResponse
            {
                Data = data,
                Page = page,
                PageSize = pageSize,
                TotalRows = totalRows,
                TotalPages = totalRows == 0 ? 0 : (int)Math.Ceiling(totalRows / (double)pageSize),
                SortBy = sortBy,
                SortDirection = sortDirection.ToLowerInvariant(),
                Search = request.Search,
                Filters = filters
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to query rows from table {TableName}.", safeTableName);
            throw new InvalidOperationException("Database operation failed.", exception);
        }
    }

    public async Task<List<DynamicColumnMetadata>> GetColumnMetadataAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var safeTableName = NormalizeTableName(tableName);

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    c.column_name,
                    c.data_type,
                    c.is_nullable,
                    EXISTS (
                        SELECT 1
                        FROM information_schema.table_constraints tc
                        JOIN information_schema.key_column_usage kcu
                          ON tc.constraint_name = kcu.constraint_name
                         AND tc.table_schema = kcu.table_schema
                        WHERE tc.table_schema = 'public'
                          AND tc.table_name = c.table_name
                          AND tc.constraint_type = 'PRIMARY KEY'
                          AND kcu.column_name = c.column_name
                    ) AS is_primary_key
                FROM information_schema.columns c
                WHERE c.table_schema = 'public' AND c.table_name = @tableName
                ORDER BY c.ordinal_position;
                """;
            AddParameter(command, "@tableName", safeTableName);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columns = new List<DynamicColumnMetadata>();

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new DynamicColumnMetadata
                {
                    Name = reader.GetString(0),
                    DatabaseType = reader.GetString(1),
                    IsNullable = string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase),
                    IsPrimaryKey = !reader.IsDBNull(3) && Convert.ToBoolean(reader.GetValue(3))
                });
            }

            if (columns.Count == 0)
            {
                throw new ArgumentException($"Table '{safeTableName}' does not exist.");
            }

            return columns;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to read metadata for table {TableName}.", safeTableName);
            throw new InvalidOperationException("Database operation failed.", exception);
        }
    }

    public async Task<List<Dictionary<string, object?>>> GetAllAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(tableName, new DynamicQueryRequest(), cancellationToken);
        return result.Data;
    }

    public async Task<Dictionary<string, object?>?> GetByIdAsync(string tableName, int id, CancellationToken cancellationToken = default)
    {
        var safeTableName = NormalizeTableName(tableName);

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{safeTableName}\" WHERE \"Id\" = @id LIMIT 1;";
            AddParameter(command, "@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = await ReadRowsAsync(reader, cancellationToken);
            return rows.FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to read row {RowId} from table {TableName}.", id, safeTableName);
            throw new InvalidOperationException("Database operation failed.", exception);
        }
    }

    public async Task<Dictionary<string, object?>?> InsertAsync(
        string tableName,
        IDictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        var safeTableName = NormalizeTableName(tableName);
        var safePayload = NormalizePayload(payload, includeId: false);

        if (safePayload.Count == 0)
        {
            throw new ArgumentException("At least one field is required.", nameof(payload));
        }

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            var columns = safePayload.Keys.ToList();
            var quotedColumns = string.Join(", ", columns.Select(column => $"\"{column}\""));
            var parameterNames = columns.Select((_, index) => $"@p{index}").ToList();
            command.CommandText = $"INSERT INTO \"{safeTableName}\" ({quotedColumns}) VALUES ({string.Join(", ", parameterNames)}) RETURNING *;";

            for (var index = 0; index < columns.Count; index++)
            {
                AddParameter(command, parameterNames[index], safePayload[columns[index]]);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = await ReadRowsAsync(reader, cancellationToken);
            return rows.FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to insert row into table {TableName}.", safeTableName);
            throw new InvalidOperationException("Database operation failed.", exception);
        }
    }

    public async Task<Dictionary<string, object?>?> UpdateAsync(
        string tableName,
        int id,
        IDictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        var safeTableName = NormalizeTableName(tableName);
        var safePayload = NormalizePayload(payload, includeId: false);

        if (safePayload.Count == 0)
        {
            throw new ArgumentException("At least one field is required.", nameof(payload));
        }

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            var columns = safePayload.Keys.ToList();
            var assignments = columns.Select((column, index) => $"\"{column}\" = @p{index}").ToList();
            command.CommandText = $"UPDATE \"{safeTableName}\" SET {string.Join(", ", assignments)} WHERE \"Id\" = @id RETURNING *;";

            for (var index = 0; index < columns.Count; index++)
            {
                AddParameter(command, $"@p{index}", safePayload[columns[index]]);
            }

            AddParameter(command, "@id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = await ReadRowsAsync(reader, cancellationToken);
            return rows.FirstOrDefault();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update row {RowId} in table {TableName}.", id, safeTableName);
            throw new InvalidOperationException("Database operation failed.", exception);
        }
    }

    public async Task<bool> DeleteAsync(string tableName, int id, CancellationToken cancellationToken = default)
    {
        var safeTableName = NormalizeTableName(tableName);

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM \"{safeTableName}\" WHERE \"Id\" = @id;";
            AddParameter(command, "@id", id);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            return affectedRows > 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete row {RowId} from table {TableName}.", id, safeTableName);
            throw new InvalidOperationException("Database operation failed.", exception);
        }
    }

    private static string NormalizeTableName(string tableName)
    {
        return SqlIdentifierSanitizer.Sanitize(tableName, "table name").ToLowerInvariant();
    }

    private static string NormalizeSortColumn(string sortBy, ISet<string> validColumnNames)
    {
        var safeColumnName = SqlIdentifierSanitizer.Sanitize(sortBy, "sort column").ToLowerInvariant();
        if (!validColumnNames.Contains(safeColumnName))
        {
            throw new ArgumentException($"Column '{sortBy}' does not exist on this table.");
        }

        return safeColumnName;
    }

    private static Dictionary<string, string> NormalizeFilters(Dictionary<string, string>? filters, ISet<string> validColumnNames)
    {
        var normalizedFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (filters is null)
        {
            return normalizedFilters;
        }

        foreach (var filter in filters)
        {
            var safeColumnName = SqlIdentifierSanitizer.Sanitize(filter.Key, "filter column").ToLowerInvariant();
            if (!validColumnNames.Contains(safeColumnName))
            {
                throw new ArgumentException($"Column '{filter.Key}' does not exist on this table.");
            }

            normalizedFilters[safeColumnName] = filter.Value;
        }

        return normalizedFilters;
    }

    private static string BuildWhereClause(
        IDbCommand command,
        IReadOnlyList<DynamicColumnMetadata> columns,
        string? search,
        IReadOnlyDictionary<string, string> filters)
    {
        var clauses = new List<string>();
        var parameterIndex = 0;

        foreach (var filter in filters)
        {
            var parameterName = $"@filter{parameterIndex++}";
            clauses.Add($"\"{filter.Key}\"::text = {parameterName}");
            AddParameter(command, parameterName, filter.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchableColumns = columns
                .Where(column => !column.IsPrimaryKey)
                .Select(column => $"\"{column.Name}\"::text ILIKE @search")
                .ToList();

            if (searchableColumns.Count > 0)
            {
                clauses.Add($"({string.Join(" OR ", searchableColumns)})");
                AddParameter(command, "@search", $"%{search.Trim()}%");
            }
        }

        return clauses.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", clauses)}";
    }

    private static Dictionary<string, object?> NormalizePayload(IDictionary<string, object?> payload, bool includeId)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in payload)
        {
            var safeColumnName = SqlIdentifierSanitizer.Sanitize(pair.Key, "column name").ToLowerInvariant();
            if (!includeId && string.Equals(safeColumnName, "id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            normalized[safeColumnName] = NormalizeValue(pair.Value);
        }

        return normalized;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Null => DBNull.Value,
                JsonValueKind.String => jsonElement.GetString() ?? (object)DBNull.Value,
                JsonValueKind.Number when jsonElement.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when jsonElement.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => jsonElement.ToString()
            };
        }

        if (value is string stringValue)
        {
            return string.IsNullOrWhiteSpace(stringValue) ? DBNull.Value : stringValue.Trim();
        }

        return value;
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(IDataReader reader, CancellationToken cancellationToken)
    {
        var results = new List<Dictionary<string, object?>>();

        while (await ((System.Data.Common.DbDataReader)reader).ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.IsDBNull(index) ? null : reader.GetValue(index);
                row[reader.GetName(index)] = value;
            }

            results.Add(row);
        }

        return results;
    }
}
