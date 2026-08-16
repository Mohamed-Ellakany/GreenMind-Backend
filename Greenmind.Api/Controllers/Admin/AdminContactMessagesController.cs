using Greenmind.Application.Dtos.Contact;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/contact-messages")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminContactMessagesController(GreenmindDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ContactMessagePageDto>> List(
        [FromQuery] bool? unreadOnly,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize is < 1 or > 100) pageSize = 50;

        var query = dbContext.ContactMessages.AsNoTracking();

        if (unreadOnly == true)
        {
            query = query.Where(item => !item.IsRead);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(item =>
                item.FullName.ToLower().Contains(term) ||
                item.Email.ToLower().Contains(term) ||
                (item.PhoneNumber != null && item.PhoneNumber.Contains(term)) ||
                (item.Subject != null && item.Subject.ToLower().Contains(term)) ||
                item.Message.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await dbContext.ContactMessages.CountAsync(item => !item.IsRead, cancellationToken);

        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ContactMessageDto(
                item.Id,
                item.FullName,
                item.Email,
                item.PhoneNumber,
                item.Subject,
                item.Message,
                item.IsRead,
                item.ReadAtUtc,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new ContactMessagePageDto(items, pageNumber, pageSize, totalCount, unreadCount));
    }

    [HttpPatch("{id:int}/read")]
    public async Task<ActionResult<ContactMessageDto>> MarkAsRead(
        int id,
        [FromBody] MarkReadRequest? request,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.ContactMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (message is null) return NotFound();

        var isRead = request?.IsRead ?? true;
        message.IsRead = isRead;
        message.ReadAtUtc = isRead ? DateTime.UtcNow : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ContactMessageDto(
            message.Id,
            message.FullName,
            message.Email,
            message.PhoneNumber,
            message.Subject,
            message.Message,
            message.IsRead,
            message.ReadAtUtc,
            message.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var message = await dbContext.ContactMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (message is null) return NotFound();

        dbContext.ContactMessages.Remove(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

public sealed record MarkReadRequest(bool IsRead = true);
