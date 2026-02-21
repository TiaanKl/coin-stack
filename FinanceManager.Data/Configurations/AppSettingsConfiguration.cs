using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Data.Configurations;

internal sealed class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("AppSettings");

        builder.Property(x => x.Currency)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(x => x.MonthlyIncome)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MonthlySavingsAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.MonthlySavingsPercent)
            .HasColumnType("decimal(5,2)");

        builder.Property(x => x.SavingsInterestRate)
            .HasColumnType("decimal(5,4)");
    }
}
