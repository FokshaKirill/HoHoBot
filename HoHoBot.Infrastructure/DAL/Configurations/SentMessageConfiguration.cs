using HoHoBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoHoBot.Infrastructure.DAL.Configurations
{
    public class SentMessageConfiguration : IEntityTypeConfiguration<SentMessage>
    {
        public void Configure(EntityTypeBuilder<SentMessage> builder)
        {
            builder.HasKey(sm => sm.Id);

            builder.Property(sm => sm.TelegramId)
                .IsRequired();

            builder.Property(sm => sm.SentAt)
                .IsRequired();

            builder.HasIndex(sm => sm.TelegramId)
                .HasDatabaseName("IX_SentMessages_TelegramId");
        }
    }
}