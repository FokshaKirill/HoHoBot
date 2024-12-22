using HoHoBot.Domain.ValueObjects.Enums;

namespace HoHoBot.Domain.Entities;

public class Chat
{
    public long ChatId { get; set; }
    public string Name { get; set; }
    public string ChatType { get; set; }
    public string GiftCurrency { get; set; }
    public decimal GiftAmount { get; set; }
    public List<Participant> Participants { get; set; } = new List<Participant>();
    public GameState GameState { get; set; } = GameState.NotStarted;
}
