using BaaS.Models;
using BaaS.Services;
using Microsoft.AspNetCore.Mvc;

namespace BaaS.Controllers;

/// <summary>
/// Provides self-tests for Swagger and developer experience configuration.
/// </summary>
[ApiController]
[Route("api/swagger")]
[ApiExplorerSettings(GroupName = "Auth APIs")]
public class SwaggerController : ControllerBase
{
    private readonly SwaggerConfigurationService _swaggerConfigurationService;

    public SwaggerController(SwaggerConfigurationService swaggerConfigurationService)
    {
        _swaggerConfigurationService = swaggerConfigurationService;
    }

    /// <summary>
    /// Verifies Swagger, API key security, and endpoint grouping configuration.
    /// </summary>
    /// <returns>Flags that confirm the Swagger developer experience is configured.</returns>
    [HttpGet("test")]
    [ProducesResponseType(typeof(SwaggerTestResponse), StatusCodes.Status200OK)]
    public ActionResult<SwaggerTestResponse> Test()
    {
        return Ok(new SwaggerTestResponse
        {
            SwaggerEnabled = _swaggerConfigurationService.IsSwaggerEnabled(),
            ApiKeySecurity = _swaggerConfigurationService.HasApiKeySecurity(),
            GroupsConfigured = _swaggerConfigurationService.HasGroupsConfigured()
        });
    }
}
