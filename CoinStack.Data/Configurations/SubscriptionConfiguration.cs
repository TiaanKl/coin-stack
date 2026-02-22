using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Cycle)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Cost)
            .HasPrecision(18, 2);

        builder.Property(x => x.ColorHex)
            .HasMaxLength(16);

        builder.Property(x => x.CustomHexColor)
            .HasMaxLength(16);

        builder.HasIndex(x => x.Name);
    }
}
