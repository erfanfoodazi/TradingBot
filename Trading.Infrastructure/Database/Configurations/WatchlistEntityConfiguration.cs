using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trading.Infrastructure.Database.Entities;

namespace Trading.Infrastructure.Database.Configurations;

public sealed class WatchlistEntityConfiguration : IEntityTypeConfiguration<WatchlistEntity>
{
    public void Configure(EntityTypeBuilder<WatchlistEntity> builder)
    {
        builder.ToTable("Watchlists");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder
            .HasMany(x => x.Symbols)
            .WithOne(x => x.Watchlist)
            .HasForeignKey(x => x.WatchlistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}