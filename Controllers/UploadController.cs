using BaaS.Models;
using BaaS.Services;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace BaaS.Controllers;

[ApiExplorerSettings(GroupName = "Upload APIs")]
[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024;
    private readonly CsvService _csvService;
    private readonly SchemaDetectionService _schemaDetectionService;
    private readonly TableService _tableService;
    private readonly DataInsertService _dataInsertService;

    public UploadController(
        CsvService csvService,
        SchemaDetectionService schemaDetectionService,
        TableService tableService,
        DataInsertService dataInsertService)
    {
        _csvService = csvService;
        _schemaDetectionService = schemaDetectionService;
        _tableService = tableService;
        _dataInsertService = dataInsertService;
    }

    /// <summary>
    /// Uploads a CSV or XLSX file, validates it, parses column names and sample rows, and detects schema.
    /// </summary>
    /// <param name="request">Multipart form data containing the CSV or XLSX file in the <c>file</c> field.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The parsed CSV columns, sample rows, and detected schema.</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CsvUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] UploadCsvRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        if (request.File.Length == 0)
        {
            return BadRequest(new { message = "The uploaded file is empty." });
        }

        if (request.File.Length > MaxFileSizeInBytes)
        {
            return BadRequest(new { message = "File size exceeds the 5 MB limit." });
        }

        try
        {
            var parsedCsv = await _csvService.ParseTabularFileAsync(request.File, cancellationToken);
            parsedCsv.Schema = _schemaDetectionService.DetectSchema(parsedCsv);

            return Ok(parsedCsv);
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
        catch (InvalidDataException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Runs the built-in CSV upload pipeline test using in-memory sample data and dynamic table creation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The created table name, detected schema, insert count, and final status.</returns>
    [HttpGet("test")]
    [ProducesResponseType(typeof(TableCreationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TableCreationResponse>> Test(CancellationToken cancellationToken)
    {
        var sampleCsv = """
                        name,age,isActive,createdAt
                        Yash,21,true,2024-01-01
                        John,25,false,2023-05-10
                        """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sampleCsv));
        var parsedCsv = await _csvService.ParseCsvAsync(stream, cancellationToken);
        parsedCsv.Schema = _schemaDetectionService.DetectSchema(parsedCsv);

        var result = await _tableService.CreateTableAsync(parsedCsv.Schema, cancellationToken);
        if (!string.Equals(result.Status, "Table created successfully", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(result.TableName))
        {
            return Ok(result);
        }

        try
        {
            result.RowsInserted = await _dataInsertService.InsertRowsAsync(
                result.TableName,
                parsedCsv.Columns,
                parsedCsv.Schema,
                parsedCsv.SampleData,
                cancellationToken);

            result.Status = "Success";
        }
        catch (Exception exception)
        {
            result.Status = "Database unavailable";
            result.Error = exception.Message;
        }

        return Ok(result);
    }
}
