using Greenmind.Application.Contracts.Support;
using Greenmind.Application.Dtos.Support;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Infrastructure.Support;

public sealed class SupportService(
    GreenmindDbContext dbContext,
    TimeProvider timeProvider) : ISupportService
{
    public async Task<SupportOperationResult<SupportConversationDto>> GetOrCreateCustomerConversationAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var farm = await dbContext.Farms
            .Include(item => item.Owner)
            .SingleOrDefaultAsync(item => item.OwnerUserId == ownerUserId, cancellationToken);
        if (farm is null)
            return SupportOperationResult<SupportConversationDto>.NotFound();

        var conversation = await dbContext.SupportConversations
            .Include(item => item.Farm).ThenInclude(item => item.Owner)
            .Where(item => item.FarmId == farm.Id && !item.IsClosed)
            .OrderByDescending(item => item.LastMessageAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            conversation = new SupportConversation
            {
                FarmId = farm.Id,
                LastMessageAtUtc = now
            };
            dbContext.SupportConversations.Add(conversation);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                conversation.Farm = farm;
            }
            catch (DbUpdateException)
            {
                dbContext.Entry(conversation).State = EntityState.Detached;
                var existingConversation = await dbContext.SupportConversations
                    .Include(item => item.Farm).ThenInclude(item => item.Owner)
                    .SingleOrDefaultAsync(
                        item => item.FarmId == farm.Id && !item.IsClosed,
                        cancellationToken);
                if (existingConversation is null)
                    throw;

                conversation = existingConversation;
            }
        }

        return SupportOperationResult<SupportConversationDto>.Success(
            await MapConversationAsync(conversation.Id, cancellationToken)
            ?? MapConversation(conversation, null));
    }

    public async Task<IReadOnlyList<SupportConversationDto>> ListAdminConversationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SupportConversations
            .AsNoTracking()
            .OrderByDescending(item => item.LastMessageAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new SupportConversationDto(
                item.Id,
                item.FarmId,
                item.Farm.Name,
                item.Farm.Owner.FullName,
                item.Farm.Owner.Email,
                item.IsClosed,
                item.LastMessageAtUtc,
                item.LastAdminReplyAtUtc,
                item.LastCustomerMessageAtUtc,
                dbContext.SupportMessages
                    .Where(message => message.SupportConversationId == item.Id)
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => message.Content)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportOperationResult<SupportMessagePageDto>> GetCustomerMessagesAsync(
        string ownerUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var conversationResult = await GetOrCreateCustomerConversationAsync(ownerUserId, cancellationToken);
        if (conversationResult.Status != SupportOperationStatus.Succeeded || conversationResult.Value is null)
            return SupportOperationResult<SupportMessagePageDto>.NotFound();

        return SupportOperationResult<SupportMessagePageDto>.Success(
            await GetMessagesPageAsync(conversationResult.Value.Id, pageNumber, pageSize, cancellationToken));
    }

    public async Task<SupportOperationResult<SupportMessagePageDto>> GetAdminMessagesAsync(
        int conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.SupportConversations.AnyAsync(item => item.Id == conversationId, cancellationToken))
            return SupportOperationResult<SupportMessagePageDto>.NotFound();

        return SupportOperationResult<SupportMessagePageDto>.Success(
            await GetMessagesPageAsync(conversationId, pageNumber, pageSize, cancellationToken));
    }

    public async Task<SupportOperationResult<SupportMessageDto>> SendCustomerMessageAsync(
        string ownerUserId,
        SendSupportMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var conversationResult = await GetOrCreateCustomerConversationAsync(ownerUserId, cancellationToken);
        if (conversationResult.Status != SupportOperationStatus.Succeeded)
            return SupportOperationResult<SupportMessageDto>.NotFound();

        return await AddMessageAsync(
            conversationResult.Value!.Id,
            ownerUserId,
            SupportMessageRole.Customer,
            request,
            cancellationToken);
    }

    public async Task<SupportOperationResult<SupportMessageDto>> SendAdminMessageAsync(
        int conversationId,
        string adminUserId,
        SendSupportMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.SupportConversations.AnyAsync(item => item.Id == conversationId, cancellationToken))
            return SupportOperationResult<SupportMessageDto>.NotFound();

        return await AddMessageAsync(
            conversationId,
            adminUserId,
            SupportMessageRole.Admin,
            request,
            cancellationToken);
    }

    private async Task<SupportOperationResult<SupportMessageDto>> AddMessageAsync(
        int conversationId,
        string senderUserId,
        SupportMessageRole role,
        SendSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        if (content.Length is < 1 or > 4000)
            return SupportOperationResult<SupportMessageDto>.Invalid("Support message content is invalid.");

        var conversation = await dbContext.SupportConversations
            .SingleAsync(item => item.Id == conversationId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var message = new SupportMessage
        {
            SupportConversationId = conversationId,
            SenderUserId = senderUserId,
            Role = role,
            Content = content,
            CreatedAt = now
        };

        conversation.LastMessageAtUtc = now;
        if (role == SupportMessageRole.Admin)
            conversation.LastAdminReplyAtUtc = now;
        else
            conversation.LastCustomerMessageAtUtc = now;

        dbContext.SupportMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        return SupportOperationResult<SupportMessageDto>.Success(
            await MapMessageAsync(message.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created support message could not be loaded."));
    }

    private async Task<SupportConversation?> GetOwnedConversationAsync(
        string ownerUserId,
        CancellationToken cancellationToken) =>
        await dbContext.SupportConversations
            .AsNoTracking()
            .Include(item => item.Farm).ThenInclude(item => item.Owner)
            .Where(item => item.Farm.OwnerUserId == ownerUserId && !item.IsClosed)
            .OrderByDescending(item => item.LastMessageAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<SupportMessagePageDto> GetMessagesPageAsync(
        int conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.SupportMessages
            .AsNoTracking()
            .Include(item => item.Sender)
            .Where(item => item.SupportConversationId == conversationId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SupportMessageDto(
                item.Id,
                item.SupportConversationId,
                item.Role,
                item.SenderUserId,
                item.Sender.FullName,
                item.Content,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return new SupportMessagePageDto(items, pageNumber, pageSize, total);
    }

    private async Task<SupportConversationDto?> MapConversationAsync(
        int conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.SupportConversations
            .AsNoTracking()
            .Include(item => item.Farm).ThenInclude(item => item.Owner)
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
            return null;

        var preview = await dbContext.SupportMessages
            .AsNoTracking()
            .Where(item => item.SupportConversationId == conversation.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Select(item => item.Content)
            .FirstOrDefaultAsync(cancellationToken);

        return MapConversation(conversation, preview);
    }

    private async Task<SupportMessageDto?> MapMessageAsync(
        int messageId,
        CancellationToken cancellationToken) =>
        await dbContext.SupportMessages
            .AsNoTracking()
            .Include(item => item.Sender)
            .Where(item => item.Id == messageId)
            .Select(item => new SupportMessageDto(
                item.Id,
                item.SupportConversationId,
                item.Role,
                item.SenderUserId,
                item.Sender.FullName,
                item.Content,
                item.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private static SupportConversationDto MapConversation(
        SupportConversation item,
        string? preview) =>
        new(
            item.Id,
            item.FarmId,
            item.Farm.Name,
            item.Farm.Owner.FullName,
            item.Farm.Owner.Email,
            item.IsClosed,
            item.LastMessageAtUtc,
            item.LastAdminReplyAtUtc,
            item.LastCustomerMessageAtUtc,
            preview);
}
