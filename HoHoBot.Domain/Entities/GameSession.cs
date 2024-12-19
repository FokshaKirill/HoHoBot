namespace HoHoBot.Domain.Entities;

public class GameSession
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Participant> Participants { get; set; }
}