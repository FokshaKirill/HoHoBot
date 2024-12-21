namespace HoHoBot.Domain.Entities;

public class SentMessage
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public DateTime SentAt { get; set; }
}
