using Greenmind.Application.Dtos.Contact;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Greenmind.Api.Controllers.Public;

[ApiController]
[Route("api/contact")]
public sealed class ContactController(GreenmindDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ContactMessageDto>> SubmitContactMessage(
        [FromBody] CreateContactMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = new ContactMessage
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Message = request.Message.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ContactMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ContactMessageDto(
            message.Id,
            message.FullName,
            message.Email,
            message.PhoneNumber,
            message.Subject,
            message.Message,
            message.IsRead,
            message.ReadAtUtc,
            message.CreatedAt);

        return Ok(dto);
    }
}
