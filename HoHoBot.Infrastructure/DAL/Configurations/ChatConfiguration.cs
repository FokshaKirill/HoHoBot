using HoHoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoHoBot.Infrastructure.DAL.Configurations;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.HasKey(c => c.ChatId);

        builder.Property(c => c.Name)
            .IsRequired();

        builder.Property(c => c.ChatType)
            .IsRequired();

        // Настройка связи с Participant: один чат может иметь много участников
        builder.HasMany(c => c.Participants)
            .WithOne(p => p.Chat)
            .HasForeignKey(p => p.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}