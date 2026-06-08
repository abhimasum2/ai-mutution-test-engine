using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MEngine.Domain.Entities;

namespace MEngine.Infrastructure.Persistence.Configurations;

public sealed class ExecutionStepConfig : IEntityTypeConfiguration<ExecutionStep>
{
    public void Configure(EntityTypeBuilder<ExecutionStep> builder)
    {
        builder.ToTable("ExecutionSteps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StepName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(4000);
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.ExecutionRun)
            .WithMany(x => x.Steps)
            .HasForeignKey(x => x.ExecutionRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
