using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class SavingsStateConfiguration : IEntityTypeConfiguration<SavingsState>
{
    public void Configure(EntityTypeBuilder<SavingsState> builder)
    {
        builder.ToTable("SavingsState");

        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.Available).HasPrecision(18, 2);
        builder.Property(x => x.Reserved).HasPrecision(18, 2);
        builder.Property(x => x.EmergencyTotal).HasPrecision(18, 2);
        builder.Property(x => x.EmergencyAvailable).HasPrecision(18, 2);
        builder.Property(x => x.LastCalculatedMonth).HasMaxLength(7);
    }
}
