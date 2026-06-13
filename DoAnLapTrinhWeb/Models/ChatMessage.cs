namespace DoAnLapTrinhWeb.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public ChatConversation? Conversation { get; set; }

        public int SenderId { get; set; }
        public AppUser? Sender { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
