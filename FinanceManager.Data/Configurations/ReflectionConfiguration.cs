using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceManager.Data.Configurations;

internal sealed class ReflectionConfiguration : IEntityTypeConfiguration<Reflection>
{
    public void Configure(EntityTypeBuilder<Reflection> builder)
    {
        builder.ToTable("Reflections");

        builder.Property(x => x.Trigger)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Prompt)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Response)
            .HasMaxLength(2000);

        builder.Property(x => x.EmotionTag)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
