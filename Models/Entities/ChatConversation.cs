using System.ComponentModel.DataAnnotations;

namespace WEBBANQUANAO.Models.Entities;

public class ChatConversation
{
    [Key]
    public int ConversationId { get; set; }

    public int? UserId { get; set; } // NULL nếu khách chưa đăng nhập
    public User? User { get; set; }

    public string UserMessage { get; set; } = null!;
    public string BotResponse { get; set; } = null!;

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
