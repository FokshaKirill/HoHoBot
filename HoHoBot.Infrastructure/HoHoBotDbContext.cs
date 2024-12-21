using HoHoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using HoHoBot.Infrastructure.DAL.Configurations;

namespace HoHoBot.Infrastructure;

public class HoHoBotDbContext : DbContext
{
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Participant> Participants { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<SentMessage> SentMessages { get; set; }

    public HoHoBotDbContext(DbContextOptions<HoHoBotDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ChatConfiguration());
        modelBuilder.ApplyConfiguration(new ParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new GameSessionConfiguration());
        modelBuilder.ApplyConfiguration(new SentMessageConfiguration());
    }
}