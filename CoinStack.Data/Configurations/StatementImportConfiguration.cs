using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class StatementImportConfiguration : IEntityTypeConfiguration<StatementImport>
{
    public void Configure(EntityTypeBuilder<StatementImport> builder)
    {
        builder.ToTable("StatementImports");

        builder.Property(x => x.FileName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.BankFormat)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.AccountNumber)
            .HasMaxLength(32);

        builder.Property(x => x.OpeningBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.ClosingBalance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
    }
}
