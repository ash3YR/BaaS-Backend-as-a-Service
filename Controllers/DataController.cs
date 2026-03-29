using System.Text;
using BaaS.Models;
using BaaS.Services;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;

namespace BaaS.Controllers;

[ApiExplorerSettings(GroupName = "Data APIs")]
[ApiController]
[Route("api/data")]
public class DataController : ControllerBase
{
    private readonly CsvService _csvService;
    private readonly SchemaDetectionService _schemaDetectionService;
    private readonly TableService _tableService;
    private readonly DataInsertService _dataInsertService;
    private readonly DynamicDataService _dynamicDataService;
    private readonly TableOwnershipService _tableOwnershipService;

    public DataController(
        CsvService csvService,
        SchemaDetectionService schemaDetectionService,
        TableService tableService,
        DataInsertService dataInsertService,
        DynamicDataService dynamicDataService,
        TableOwnershipService tableOwnershipService)
    {
        _csvService = csvService;
        _schemaDetectionService = schemaDetectionService;
        _tableService = tableService;
        _dataInsertService = dataInsertService;
        _dynamicDataService = dynamicDataService;
        _tableOwnershipService = tableOwnershipService;
    }

    /// <summary>
    /// Runs an end-to-end self-test for dynamic CRUD by creating a table, seeding rows, and performing CRUD operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The dynamic CRUD results including inserted, updated, and deleted row states.</returns>
    [HttpGet("test")]
    [ProducesResponseType(typeof(DynamicCrudTestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Test(CancellationToken cancellationToken)
    {
        try
        {
            var sampleCsv = """
                            name,age
                            Yash,21
                            John,25
                            """;

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sampleCsv));
            var parsedCsv = await _csvService.ParseCsvAsync(stream, cancellationToken);
            parsedCsv.Schema = _schemaDetectionService.DetectSchema(parsedCsv);

            var tableResult = await _tableService.CreateTableAsync(parsedCsv.Schema, cancellationToken);
            if (!string.Equals(tableResult.Status, "Table created successfully", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(tableResult.TableName))
            {
                return Ok(new DatabaseErrorResponse
                {
                    Status = tableResult.Status,
                    Error = tableResult.Error ?? "Unable to connect to PostgreSQL."
                });
            }

            await _dataInsertService.InsertRowsAsync(
                tableResult.TableName,
                parsedCsv.Columns,
                parsedCsv.Schema,
                parsedCsv.SampleData,
                cancellationToken);

            await _tableOwnershipService.RegisterOwnershipAsync(
                GetRequiredUserId(),
                tableResult.TableName,
                "self-test.csv",
                cancellationToken);

            var initialRows = await _dynamicDataService.GetAllAsync(tableResult.TableName, cancellationToken);
            var firstRowId = GetRequiredId(initialRows.FirstOrDefault());
            var singleRow = await _dynamicDataService.GetByIdAsync(tableResult.TableName, firstRowId, cancellationToken);

            var insertedRow = await _dynamicDataService.InsertAsync(
                tableResult.TableName,
                new Dictionary<string, object?>
                {
                    ["name"] = "Alice",
                    ["age"] = 30
                },
                cancellationToken);

            var insertedRowId = GetRequiredId(insertedRow);
            var updatedRow = await _dynamicDataService.UpdateAsync(
                tableResult.TableName,
                insertedRowId,
                new Dictionary<string, object?>
                {
                    ["name"] = "Alice Updated",
                    ["age"] = 31
                },
                cancellationToken);

            var deleted = await _dynamicDataService.DeleteAsync(tableResult.TableName, insertedRowId, cancellationToken);

            return Ok(new DynamicCrudTestResponse
            {
                TableName = tableResult.TableName,
                InitialRows = initialRows,
                SingleRow = singleRow,
                AfterInsert = insertedRow,
                AfterUpdate = updatedRow,
                AfterDelete = deleted ? "success" : "failed"
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HeaderValidationException)
        {
            return BadRequest(new { message = "Invalid CSV. The file must include a valid header row." });
        }
        catch (BadDataException)
        {
            return BadRequest(new { message = "Invalid CSV format." });
        }
        catch (CsvHelperException)
        {
            return BadRequest(new { message = "Invalid CSV format." });
        }
        catch (Exception exception)
        {
            return Ok(new DatabaseErrorResponse
            {
                Status = "Database unavailable",
                Error = exception.InnerException?.Message ?? exception.Message
            });
        }
    }

    /// <summary>
    /// Returns all rows from a runtime-created table.
    /// </summary>
    /// <param name="table">The dynamic table name.</param>
    /// <param name="page">The page number, starting from 1.</param>
    /// <param name="pageSize">The number of rows to return per page.</param>
    /// <param name="sortBy">The column name to sort by.</param>
    /// <param name="sortDirection">The sort direction: <c>asc</c> or <c>desc</c>.</param>
    /// <param name="search">A free-text search value matched across non-primary-key columns.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A paginated JSON result containing rows plus query metadata.</returns>
    [HttpGet("{table}")]
    public async Task<ActionResult<object>> GetAll(
        string table,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortDirection = "asc",
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var filters = Request.Query
                .Where(pair => pair.Key.StartsWith("filter_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    pair => pair.Key["filter_".Length..],
                    pair => pair.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            var query = new DynamicQueryRequest
            {
                Page = page,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDirection = sortDirection,
                Search = search,
                Filters = filters
            };

            var rows = await _dynamicDataService.QueryAsync(table, query, cancellationToken);
            return Ok(rows);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    /// <summary>
    /// Returns column metadata for a runtime-created table.
    /// </summary>
    [HttpGet("{table}/metadata")]
    public async Task<ActionResult<object>> GetMetadata(string table, CancellationToken cancellationToken)
    {
        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var metadata = await _dynamicDataService.GetColumnMetadataAsync(table, cancellationToken);
            return Ok(metadata);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    /// <summary>
    /// Returns an OpenAPI-style description for a runtime-created table and its generated endpoints.
    /// </summary>
    [HttpGet("{table}/openapi")]
    public async Task<ActionResult<object>> GetOpenApiSpec(string table, CancellationToken cancellationToken)
    {
        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var columns = await _dynamicDataService.GetColumnMetadataAsync(table, cancellationToken);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var tablePath = $"/api/data/{table}";

            var spec = new DynamicTableOpenApiSpec
            {
                TableName = table,
                Columns = columns,
                Endpoints =
                [
                    new DynamicEndpointSpec
                    {
                        Method = "GET",
                        Path = $"{baseUrl}{tablePath}",
                        Description = "Returns rows with optional filtering, pagination, sorting, and search.",
                        QuerySchema = new
                        {
                            page = "number",
                            pageSize = "number",
                            sortBy = "string",
                            sortDirection = "asc|desc",
                            search = "string",
                            filters = "Use query parameters like filter_columnName=value"
                        },
                        ResponseSchema = new
                        {
                            data = "array<object>",
                            page = "number",
                            pageSize = "number",
                            totalRows = "number",
                            totalPages = "number"
                        }
                    },
                    new DynamicEndpointSpec
                    {
                        Method = "GET",
                        Path = $"{baseUrl}{tablePath}/{{id}}",
                        Description = "Returns one row by primary key.",
                        ResponseSchema = new { row = "object" }
                    },
                    new DynamicEndpointSpec
                    {
                        Method = "POST",
                        Path = $"{baseUrl}{tablePath}",
                        Description = "Creates a new row.",
                        RequestBodySchema = columns
                            .Where(column => !column.IsPrimaryKey)
                            .ToDictionary(column => column.Name, column => column.DatabaseType),
                        ResponseSchema = new { row = "object" }
                    },
                    new DynamicEndpointSpec
                    {
                        Method = "PUT",
                        Path = $"{baseUrl}{tablePath}/{{id}}",
                        Description = "Updates a row by primary key.",
                        RequestBodySchema = columns
                            .Where(column => !column.IsPrimaryKey)
                            .ToDictionary(column => column.Name, column => column.DatabaseType),
                        ResponseSchema = new { row = "object" }
                    },
                    new DynamicEndpointSpec
                    {
                        Method = "DELETE",
                        Path = $"{baseUrl}{tablePath}/{{id}}",
                        Description = "Deletes a row by primary key.",
                        ResponseSchema = new { status = "success" }
                    },
                    new DynamicEndpointSpec
                    {
                        Method = "GET",
                        Path = $"{baseUrl}{tablePath}/metadata",
                        Description = "Returns column metadata for the generated table.",
                        ResponseSchema = new { columns = "array<object>" }
                    }
                ]
            };

            return Ok(spec);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    /// <summary>
    /// Returns a single row from a runtime-created table by its identifier.
    /// </summary>
    /// <param name="table">The dynamic table name.</param>
    /// <param name="id">The row identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A JSON object representing the matching row.</returns>
    [HttpGet("{table}/{id:int}")]
    public async Task<ActionResult<object>> GetById(string table, int id, CancellationToken cancellationToken)
    {
        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var row = await _dynamicDataService.GetByIdAsync(table, id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    /// <summary>
    /// Inserts a new row into a runtime-created table using a dynamic JSON body.
    /// </summary>
    /// <param name="table">The dynamic table name.</param>
    /// <param name="payload">A JSON object whose properties map to table columns.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The inserted row as JSON.</returns>
    [HttpPost("{table}")]
    public async Task<ActionResult<object>> Insert(string table, [FromBody] Dictionary<string, object?>? payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.Count == 0)
        {
            return BadRequest(new { message = "Request body cannot be empty." });
        }

        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var insertedRow = await _dynamicDataService.InsertAsync(table, payload, cancellationToken);
            return Ok(insertedRow);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    /// <summary>
    /// Updates an existing row in a runtime-created table using a dynamic JSON body.
    /// </summary>
    /// <param name="table">The dynamic table name.</param>
    /// <param name="id">The row identifier.</param>
    /// <param name="payload">A JSON object whose properties map to table columns.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The updated row as JSON.</returns>
    [HttpPut("{table}/{id:int}")]
    public async Task<ActionResult<object>> Update(string table, int id, [FromBody] Dictionary<string, object?>? payload, CancellationToken cancellationToken)
    {
        if (payload is null || payload.Count == 0)
        {
            return BadRequest(new { message = "Request body cannot be empty." });
        }

        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var updatedRow = await _dynamicDataService.UpdateAsync(table, id, payload, cancellationToken);
            return updatedRow is null ? NotFound() : Ok(updatedRow);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    /// <summary>
    /// Deletes a row from a runtime-created table by its identifier.
    /// </summary>
    /// <param name="table">The dynamic table name.</param>
    /// <param name="id">The row identifier.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A success payload when the row is deleted.</returns>
    [HttpDelete("{table}/{id:int}")]
    public async Task<ActionResult<object>> Delete(string table, int id, CancellationToken cancellationToken)
    {
        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var deleted = await _dynamicDataService.DeleteAsync(table, id, cancellationToken);
            return deleted ? Ok(new { status = "success" }) : NotFound();
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateDatabaseError(exception));
        }
    }

    private static int GetRequiredId(Dictionary<string, object?>? row)
    {
        if (row is null || !row.TryGetValue("Id", out var idValue) || idValue is null)
        {
            throw new InvalidOperationException("Row Id was not returned from the database.");
        }

        return Convert.ToInt32(idValue);
    }

    private int GetRequiredUserId()
    {
        if (HttpContext.Items["ApiUserId"] is int userId)
        {
            return userId;
        }

        throw new UnauthorizedAccessException("A valid API key is required.");
    }

    private static DatabaseErrorResponse CreateDatabaseError(Exception exception)
    {
        return new DatabaseErrorResponse
        {
            Error = exception.InnerException?.Message ?? exception.Message
        };
    }
}
