using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class SavingsFallbackEventConfiguration : IEntityTypeConfiguration<SavingsFallbackEvent>
{
    public void Configure(EntityTypeBuilder<SavingsFallbackEvent> builder)
    {
        builder.ToTable("SavingsFallbackEvents");

        builder.Property(x => x.AmountUsed).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.SourceName).HasMaxLength(200);

        builder.HasIndex(x => x.OccurredAtUtc);
    }
}
