using Greenmind.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers
{
    [ApiController]
    [Authorize]
    [ActiveFarmRequired]
    public abstract class FarmOwnerControllerBase : ControllerBase
    {
    }
}
