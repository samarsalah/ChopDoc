using Microsoft.AspNetCore.Mvc;

namespace ChopDoc.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "Healthy",
        service = "ChopDoc.Api",
        utc = DateTime.UtcNow
    });
}
