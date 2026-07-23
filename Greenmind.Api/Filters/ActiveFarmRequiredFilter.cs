using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Greenmind.Api.Filters
{
    public class ActiveFarmRequiredFilter(GreenmindDbContext dbContext) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated ?? true)
            {
                await next();
                return;
            }

            if (user.IsInRole(AppRoles.PlatformAdmin))
            {
                await next();
                return;
            }

            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            var farm = await dbContext.Farms
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.OwnerUserId == userId);

            if (farm is null || farm.Status != FarmStatus.Active)
            {
                context.Result = new ObjectResult(new { message = "Your farm is not active. Access is restricted until an administrator approves or activates your farm." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            var ownerIsActive = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.IsActive)
                .FirstOrDefaultAsync();

            if (!ownerIsActive)
            {
                context.Result = new ObjectResult(new { message = "Your account is inactive. Access is restricted until an administrator activates your farm." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
