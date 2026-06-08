using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MEngine.Domain.Entities;

namespace MEngine.Infrastructure.Persistence.Configurations;

public sealed class ExecutionRunConfig : IEntityTypeConfiguration<ExecutionRun>
{
    public void Configure(EntityTypeBuilder<ExecutionRun> builder)
    {
        builder.ToTable("ExecutionRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RepositoryUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SecretKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OutputFolder).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.AgentConfiguration)
            .WithMany()
            .HasForeignKey(x => x.AgentConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
