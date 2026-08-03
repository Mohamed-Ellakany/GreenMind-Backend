using System.Net.Mail;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Greenmind.Api.Services;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/admins")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public sealed class AdminManagementController(
    UserManager<ApplicationUser> userManager,
    GreenmindDbContext db,
    IAccountEmailService emailService,
    ILogger<AdminManagementController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminListItem>>> List(CancellationToken cancellationToken)
    {
        var admins = new List<AdminListItem>();
        foreach (var user in await userManager.GetUsersInRoleAsync(RoleNames.Admin))
        {
            admins.Add(new AdminListItem(user.Id, user.FullName, user.Email ?? "", user.IsActive,
                await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin)));
        }
        return Ok(admins.OrderByDescending(item => item.IsSuperAdmin).ThenBy(item => item.FullName));
    }

    [HttpGet("invitations")]
    public async Task<ActionResult<IEnumerable<InvitationListItem>>> Invitations(CancellationToken cancellationToken) =>
        Ok(await db.AdminInvitations.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Select(item => new InvitationListItem(item.Id, item.FullName, item.Email, item.ExpiresAtUtc, item.AcceptedAtUtc, item.RevokedAtUtc))
            .ToListAsync(cancellationToken));

    [HttpPost("invitations")]
    public async Task<ActionResult> Invite(CreateAdminInvitation request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Invitation cannot be sent", detail: "An account already uses this email.");
        if (await db.AdminInvitations.AnyAsync(item => item.Email == email && item.AcceptedAtUtc == null && item.RevokedAtUtc == null && item.ExpiresAtUtc > DateTime.UtcNow, cancellationToken))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Invitation cannot be sent", detail: "An active invitation already exists for this email.");

        if (!emailService.IsConfigured)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email service unavailable", detail: "SMTP credentials are not configured.");

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new AdminInvitation { FullName = request.FullName.Trim(), Email = email, TokenHash = Hash(rawToken),
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, ExpiresAtUtc = DateTime.UtcNow.AddDays(7) };
        db.AdminInvitations.Add(invitation); await db.SaveChangesAsync(cancellationToken);
        var sendError = await TrySendInvitationAsync(invitation.Email, invitation.FullName, rawToken);
        if (sendError is not null)
        {
            db.AdminInvitations.Remove(invitation);
            await db.SaveChangesAsync(cancellationToken);
            return sendError;
        }
        return CreatedAtAction(nameof(Invitations), new { invitation.Id });
    }

    [HttpPost("invitations/{id:int}/resend")]
    public async Task<ActionResult> Resend(int id, CancellationToken cancellationToken)
    {
        var invitation = await db.AdminInvitations.FindAsync([id], cancellationToken);
        if (invitation is null || invitation.AcceptedAtUtc is not null || invitation.RevokedAtUtc is not null) return NotFound();
        if (!emailService.IsConfigured)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email service unavailable", detail: "SMTP credentials are not configured.");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sendError = await TrySendInvitationAsync(invitation.Email, invitation.FullName, token);
        if (sendError is not null) return sendError;

        invitation.TokenHash = Hash(token); invitation.ExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("invitations/{id:int}/revoke")]
    public async Task<ActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        var invitation = await db.AdminInvitations.FindAsync([id], cancellationToken);
        if (invitation is null || invitation.AcceptedAtUtc is not null) return NotFound(); invitation.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPatch("{id}/active")]
    public async Task<ActionResult> SetActive(string id, SetActiveRequest request)
    {
        var user = await userManager.FindByIdAsync(id); if (user is null || !await userManager.IsInRoleAsync(user, RoleNames.Admin)) return NotFound();
        if (!request.IsActive && await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin) &&
            (await userManager.GetUsersInRoleAsync(RoleNames.SuperAdmin)).Count(item => item.IsActive) <= 1) return Conflict(new { message = "The last active super admin cannot be deactivated." });
        user.IsActive = request.IsActive; await userManager.UpdateAsync(user); return NoContent();
    }

    private async Task<ObjectResult?> TrySendInvitationAsync(string email, string fullName, string token)
    {
        try
        {
            await emailService.SendAdminInvitationAsync(email, fullName, token);
            return null;
        }
        catch (SmtpException exception)
        {
            logger.LogError(exception, "MailerSend rejected an admin invitation email to {RecipientDomain}.",
                email[(email.LastIndexOf('@') + 1)..]);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Email service unavailable", detail: "The email provider rejected the message. Verify the SMTP credentials and sender domain.");
        }
    }
    private static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}

public sealed record CreateAdminInvitation(
    [Required, MaxLength(200)] string FullName,
    [Required, EmailAddress, MaxLength(256)] string Email);
public sealed record SetActiveRequest(bool IsActive);
public sealed record AdminListItem(string Id, string FullName, string Email, bool IsActive, bool IsSuperAdmin);
public sealed record InvitationListItem(int Id, string FullName, string Email, DateTime ExpiresAtUtc, DateTime? AcceptedAtUtc, DateTime? RevokedAtUtc);
