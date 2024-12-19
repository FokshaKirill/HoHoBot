namespace HoHoBot.Domain.Entities;

public class Chat
{
    public long ChatId { get; set; }
    public string Name { get; set; }
    public string ChatType { get; set; }
    public bool IsActive { get; set; } = true; // Новый флаг активности
    public List<Participant> Participants { get; set; } = new List<Participant>();
}
