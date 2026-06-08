using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MEngine.Domain.Entities;

namespace MEngine.Infrastructure.Persistence.Configurations;

public sealed class AgentConfigurationConfig : IEntityTypeConfiguration<AgentConfiguration>
{
    public void Configure(EntityTypeBuilder<AgentConfiguration> builder)
    {
        builder.ToTable("AgentConfigurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AgentName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SecretKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EndpointUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.AgentName).IsUnique();
    }
}
