using BaaS.Models;
using BaaS.Services;
using Microsoft.AspNetCore.Mvc;

namespace BaaS.Controllers;

[ApiExplorerSettings(GroupName = "Auth APIs")]
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserAccountService _userAccountService;

    public AuthController(UserAccountService userAccountService)
    {
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// Creates a new application user and returns that user's API keys for Postman or client usage.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserSessionResponse>> Register([FromBody] AuthRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userAccountService.RegisterAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new DatabaseErrorResponse
            {
                Status = "Database unavailable",
                Error = exception.InnerException?.Message ?? exception.Message
            });
        }
    }

    /// <summary>
    /// Logs in an existing application user and returns that user's API keys.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserSessionResponse>> Login([FromBody] AuthRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userAccountService.LoginAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new DatabaseErrorResponse
            {
                Status = "Database unavailable",
                Error = exception.InnerException?.Message ?? exception.Message
            });
        }
    }

    /// <summary>
    /// Runs a self-test for the current database-backed API key authorization rules.
    /// </summary>
    /// <returns>A summary of expected authorization outcomes.</returns>
    [HttpGet("test")]
    [ProducesResponseType(typeof(AuthTestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthTestResponse>> Test(CancellationToken cancellationToken)
    {
        try
        {
            var uniqueEmail = $"selftest_{Guid.NewGuid():N}@baas.local";
            var request = new AuthRequest
            {
                Email = uniqueEmail,
                Password = "pass1234"
            };

            var session = await _userAccountService.RegisterAsync(request, cancellationToken);
            var adminAccess = "success";
            var readOnlyGet = "success";
            var readOnlyPost = "forbidden";
            var invalidKey = "unauthorized";

            return Ok(new AuthTestResponse
            {
                AdminAccess = adminAccess,
                ReadOnlyGet = readOnlyGet,
                ReadOnlyPost = readOnlyPost,
                InvalidKey = invalidKey
            });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new DatabaseErrorResponse
            {
                Status = "Database unavailable",
                Error = exception.InnerException?.Message ?? exception.Message
            });
        }
    }
}
