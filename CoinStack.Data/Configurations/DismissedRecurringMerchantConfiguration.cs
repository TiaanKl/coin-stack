using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class DismissedRecurringMerchantConfiguration : IEntityTypeConfiguration<DismissedRecurringMerchant>
{
    public void Configure(EntityTypeBuilder<DismissedRecurringMerchant> builder)
    {
        builder.ToTable("DismissedRecurringMerchants");

        builder.Property(x => x.NormalizedKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.NormalizedKey)
            .IsUnique();
    }
}
