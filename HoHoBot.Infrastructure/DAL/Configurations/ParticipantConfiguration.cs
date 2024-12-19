using HoHoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoHoBot.Infrastructure.DAL.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TelegramId)
            .IsRequired();

        builder.Property(p => p.UserName)
            .IsRequired();

        builder.Property(p => p.FullName)
            .IsRequired();

        // Настройка связи Participant -> Chat
        builder.HasOne(p => p.Chat)
            .WithMany(c => c.Participants)
            .HasForeignKey(p => p.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}