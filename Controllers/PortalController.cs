using BaaS.Models;
using BaaS.Services;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;

namespace BaaS.Controllers;

[ApiController]
[Route("api/portal")]
[ApiExplorerSettings(GroupName = "Upload APIs")]
public class PortalController : ControllerBase
{
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024;
    private readonly ProvisioningService _provisioningService;
    private readonly DynamicDataService _dynamicDataService;
    private readonly TableOwnershipService _tableOwnershipService;

    public PortalController(
        ProvisioningService provisioningService,
        DynamicDataService dynamicDataService,
        TableOwnershipService tableOwnershipService)
    {
        _provisioningService = provisioningService;
        _dynamicDataService = dynamicDataService;
        _tableOwnershipService = tableOwnershipService;
    }

    /// <summary>
    /// Uploads a CSV or XLSX file, provisions a dynamic table, inserts rows, and returns generated API details.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProvisioningResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAndProvision([FromForm] UploadCsvRequest request, CancellationToken cancellationToken)
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
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _provisioningService.ProvisionAsync(request.File, baseUrl, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.TableName))
            {
                await _tableOwnershipService.RegisterOwnershipAsync(
                    GetRequiredUserId(),
                    result.TableName,
                    request.File.FileName,
                    cancellationToken);
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
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
    /// Returns a preview of all rows for a generated table.
    /// </summary>
    [HttpGet("tables/{table}")]
    [ProducesResponseType(typeof(List<Dictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTablePreview(string table, CancellationToken cancellationToken)
    {
        try
        {
            await _tableOwnershipService.EnsureOwnershipAsync(GetRequiredUserId(), table, cancellationToken);
            var rows = await _dynamicDataService.GetAllAsync(table, cancellationToken);
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
            return Ok(new DatabaseErrorResponse
            {
                Status = "Database unavailable",
                Error = exception.InnerException?.Message ?? exception.Message
            });
        }
    }

    private int GetRequiredUserId()
    {
        if (HttpContext.Items["ApiUserId"] is int userId)
        {
            return userId;
        }

        throw new UnauthorizedAccessException("A valid API key is required.");
    }
}
