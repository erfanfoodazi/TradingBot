using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trading.Infrastructure.Database.Entities;

namespace Trading.Infrastructure.Database.Configurations;

public sealed class WatchlistSymbolEntityConfiguration : IEntityTypeConfiguration<WatchlistSymbolEntity>
{
    public void Configure(EntityTypeBuilder<WatchlistSymbolEntity> builder)
    {
        builder.ToTable("WatchlistSymbols");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Symbol).HasMaxLength(32).IsRequired();

        builder.HasIndex(x => new { x.WatchlistId, x.Symbol }).IsUnique();
    }
}