using Greenmind.Domain.Consts.Enums;
using System.ComponentModel.DataAnnotations;

namespace Greenmind.Domain.Entities;

public sealed class SupportConversation : BaseEntity
{
    public int FarmId { get; set; }
    public DateTime LastMessageAtUtc { get; set; }
    public DateTime? LastAdminReplyAtUtc { get; set; }
    public DateTime? LastCustomerMessageAtUtc { get; set; }
    public bool IsClosed { get; set; }

    public Farm Farm { get; set; } = null!;
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}

public sealed class SupportMessage : BaseEntity
{
    public int SupportConversationId { get; set; }
    public SupportMessageRole Role { get; set; }

    [MaxLength(450)]
    public required string SenderUserId { get; set; }

    [MaxLength(4000)]
    public required string Content { get; set; }

    public SupportConversation Conversation { get; set; } = null!;
    public ApplicationUser Sender { get; set; } = null!;
}
