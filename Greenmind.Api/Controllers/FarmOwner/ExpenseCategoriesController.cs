using System.Security.Claims;
using Greenmind.Application.Contracts.Costs;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.FarmOwner;

[ApiController]
[Route("api/expense-categories")]
[Authorize(Roles = RoleNames.FarmOwner)]
public sealed class ExpenseCategoriesController(
    ICostManagementService costService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseCategoryDto>>> List(
        CancellationToken cancellationToken)
    {
        var result = await costService.GetCategoriesAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            cancellationToken);
        return Ok(result.Value);
    }
}
