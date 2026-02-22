using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ExpenseKind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AutoDeduct)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Subscription)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Bucket)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.BucketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.DebtAccount)
            .WithMany()
            .HasForeignKey(x => x.DebtAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OccurredAtUtc);

        builder.HasIndex(x => x.AutoDeductTemplateId);
    }
}
