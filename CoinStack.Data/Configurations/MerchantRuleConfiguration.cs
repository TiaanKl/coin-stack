using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoinStack.Data.Configurations;

internal sealed class MerchantRuleConfiguration : IEntityTypeConfiguration<MerchantRule>
{
    public void Configure(EntityTypeBuilder<MerchantRule> builder)
    {
        builder.ToTable("MerchantRules");

        builder.Property(x => x.NormalizedKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.MerchantName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.NormalizedKey)
            .IsUnique();

        // Deleting a category forgets the rules that pointed at it.
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
