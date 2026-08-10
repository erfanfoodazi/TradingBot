using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trading.Infrastructure.Database.Entities;

namespace Trading.Infrastructure.Database.Configurations;

public sealed class AppLogConfiguration : IEntityTypeConfiguration<AppLog>
{
    public void Configure(EntityTypeBuilder<AppLog> builder)
    {
        builder.ToTable("AppLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Level).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Component).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.StackTrace).HasMaxLength(8192);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Level);
    }
}