using CivicTrace.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var databaseAvailable = await dbContext.Database.CanConnectAsync(ct);

        var body = new
        {
            status = databaseAvailable ? "ok" : "degraded",
            database = databaseAvailable ? "ok" : "unavailable"
        };

        return databaseAvailable
            ? Ok(body)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}