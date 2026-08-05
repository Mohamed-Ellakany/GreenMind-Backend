using System.Security.Claims;
using Greenmind.Domain.Consts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Greenmind.Api.Hubs;

[Authorize]
public sealed class SupportHub : Hub
{
    public const string AdminGroup = "support-admins";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        if (Context.User?.IsInRole(RoleNames.Admin) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

        await base.OnConnectedAsync();
    }

    public static string UserGroup(string userId) => $"support-user-{userId}";
}
