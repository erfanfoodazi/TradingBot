using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trading.Infrastructure.Database.Entities;

namespace Trading.Infrastructure.Database.Configurations;

public sealed class StrategyEntityConfiguration : IEntityTypeConfiguration<StrategyEntity>
{
    public void Configure(EntityTypeBuilder<StrategyEntity> builder)
    {
        builder.ToTable("Strategies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.ParametersJson).HasMaxLength(4096).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}