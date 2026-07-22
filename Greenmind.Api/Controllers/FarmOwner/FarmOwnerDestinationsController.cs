using Greenmind.Application.Contracts.MasterData;
using Greenmind.Application.Dtos.MasterData;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/farm-owner/destinations")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class FarmOwnerDestinationsController(IMasterDataQueryService masterDataService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetActive(
        CancellationToken cancellationToken)
    {
        return Ok(await masterDataService.GetActiveDestinationsAsync(cancellationToken));
    }
}
