﻿namespace HoHoBot.Domain.Entities;

public class Participant
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public long ChatId { get; set; }
    public Chat Chat { get; set; }
    public string UserName { get; set; }
    public string FullName { get; set; }
}

