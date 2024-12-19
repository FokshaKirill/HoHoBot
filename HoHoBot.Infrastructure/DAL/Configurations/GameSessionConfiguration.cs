using HoHoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoHoBot.Infrastructure.DAL.Configurations;

public class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.HasKey(gs => gs.Id);

        builder.Property(gs => gs.Name)
            .IsRequired();

        builder.Property(gs => gs.CreatedAt)
            .IsRequired();

        // Настройка связи GameSession -> Participant: один сеанс может включать много участников
        builder.HasMany(gs => gs.Participants)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}