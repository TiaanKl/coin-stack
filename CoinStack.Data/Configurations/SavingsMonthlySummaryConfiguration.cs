using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class SavingsMonthlySummaryConfiguration : IEntityTypeConfiguration<SavingsMonthlySummary>
{
    public void Configure(EntityTypeBuilder<SavingsMonthlySummary> builder)
    {
        builder.ToTable("SavingsMonthlySummaries");

        builder.Property(x => x.Month).HasMaxLength(7).IsRequired();
        builder.Property(x => x.Base).HasPrecision(18, 2);
        builder.Property(x => x.Interest).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.RunningTotal).HasPrecision(18, 2);

        builder.HasIndex(x => x.Month).IsUnique();
    }
}
