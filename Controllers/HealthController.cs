using Microsoft.AspNetCore.Mvc;

namespace BaaS.Controllers;

[ApiExplorerSettings(GroupName = "Upload APIs")]
[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Returns a simple health response indicating the API is running.
    /// </summary>
    /// <returns>A plain text status message.</returns>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("API is working");
    }
}
