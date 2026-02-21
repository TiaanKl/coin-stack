using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Data.Configurations;

internal sealed class DebtAccountConfiguration : IEntityTypeConfiguration<DebtAccount>
{
    public void Configure(EntityTypeBuilder<DebtAccount> builder)
    {
        builder.ToTable("DebtAccounts");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasMaxLength(200);

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.CurrentBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.MonthlyPaymentAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.InterestRatePercent)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.PaymentStartDateUtc);
    }
}