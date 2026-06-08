using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MEngine.Domain.Entities;

namespace MEngine.Infrastructure.Persistence.Configurations;

public sealed class RepositoryAnalysisConfig : IEntityTypeConfiguration<RepositoryAnalysis>
{
    public void Configure(EntityTypeBuilder<RepositoryAnalysis> builder)
    {
        builder.ToTable("RepositoryAnalyses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BuildStatus).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RepoSummary).HasMaxLength(4000);
        builder.Property(x => x.Language).HasMaxLength(100);
        builder.Property(x => x.TestFramework).HasMaxLength(100);
        builder.Property(x => x.ProfileSummary).HasMaxLength(4000);
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}

public sealed class MutationReportConfig : IEntityTypeConfiguration<MutationReport>
{
    public void Configure(EntityTypeBuilder<MutationReport> builder)
    {
        builder.ToTable("MutationReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.JsonReportPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Tool).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}

public sealed class TestDecisionConfig : IEntityTypeConfiguration<TestDecision>
{
    public void Configure(EntityTypeBuilder<TestDecision> builder)
    {
        builder.ToTable("TestDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}

public sealed class TestRunConfig : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.ToTable("TestRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}

public sealed class CommitResultConfig : IEntityTypeConfiguration<CommitResult>
{
    public void Configure(EntityTypeBuilder<CommitResult> builder)
    {
        builder.ToTable("CommitResults");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CommitSha).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Branch).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}

public sealed class FinalReportConfig : IEntityTypeConfiguration<FinalReport>
{
    public void Configure(EntityTypeBuilder<FinalReport> builder)
    {
        builder.ToTable("FinalReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FinalReportPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FinalHtmlReportPath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}

public sealed class PipelineNotificationConfig : IEntityTypeConfiguration<PipelineNotification>
{
    public void Configure(EntityTypeBuilder<PipelineNotification> builder)
    {
        builder.ToTable("PipelineNotifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NotificationStatus).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
    }
}
