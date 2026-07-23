using Greenmind.Api.Controllers;
using Greenmind.Api.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.IntegrationTests;

[Route("api/test/farm-owner")]
public class TestFarmOwnerController : FarmOwnerControllerBase
{
    [HttpGet("protected")]
    public IActionResult GetProtectedResource()
    {
        return Ok(new { message = "Access granted." });
    }
}
