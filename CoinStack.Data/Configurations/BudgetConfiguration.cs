using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");

        builder.Property(x => x.Year)
            .IsRequired();

        builder.Property(x => x.Month)
            .IsRequired();

        builder.Property(x => x.LimitAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Budgets)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Bucket)
            .WithMany(x => x.Budgets)
            .HasForeignKey(x => x.BucketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Year, x.Month, x.CategoryId })
            .IsUnique();
    }
}
