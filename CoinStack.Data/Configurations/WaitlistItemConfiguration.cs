using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class WaitlistItemConfiguration : IEntityTypeConfiguration<WaitlistItem>
{
    public void Configure(EntityTypeBuilder<WaitlistItem> builder)
    {
        builder.ToTable("WaitlistItems");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Url)
            .HasMaxLength(2000);

        builder.Property(x => x.EstimatedCost)
            .HasColumnType("TEXT");

        builder.Property(x => x.Priority)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CoolOffPeriod)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.EmotionAtTimeOfAdding)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.ReflectionNote)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.IsUnlocked);
        builder.HasIndex(x => x.IsPurchased);
        builder.HasIndex(x => x.CoolOffUntil);
    }
}
