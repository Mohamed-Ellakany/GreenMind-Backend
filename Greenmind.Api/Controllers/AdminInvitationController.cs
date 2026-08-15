using System.Security.Cryptography;
using System.Text;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.Controllers;

[ApiController]
[Route("api/admin-invitations")]
public sealed class AdminInvitationController(UserManager<ApplicationUser> users, GreenmindDbContext db) : ControllerBase
{
    [HttpPost("accept")]
    public async Task<ActionResult> Accept(AcceptAdminInvitation request, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));
        var invitation = await db.AdminInvitations.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (invitation is null || invitation.AcceptedAtUtc is not null || invitation.RevokedAtUtc is not null || invitation.ExpiresAtUtc < DateTime.UtcNow) return BadRequest(new { message = "This invitation is invalid or expired." });
        if (await users.FindByEmailAsync(invitation.Email) is not null) return BadRequest(new { message = "This invitation is no longer available." });
        var user = new ApplicationUser { UserName = invitation.Email, Email = invitation.Email, FullName = invitation.FullName, IsActive = true, EmailConfirmed = true };
        var created = await users.CreateAsync(user, request.Password); if (!created.Succeeded) return BadRequest(created.Errors.Select(error => error.Description));
        await users.AddToRoleAsync(user, RoleNames.Admin); invitation.AcceptedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(cancellationToken); return NoContent();
    }
}
public sealed record AcceptAdminInvitation(string Token, string Password);
