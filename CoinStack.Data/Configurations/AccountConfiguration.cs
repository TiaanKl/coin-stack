using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AccountNumber)
            .HasMaxLength(32);

        builder.Property(x => x.Balance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne(x => x.DebtAccount)
            .WithMany()
            .HasForeignKey(x => x.DebtAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.DebtAccountId)
            .IsUnique();

        builder.HasIndex(x => x.AccountNumber);
    }
}
