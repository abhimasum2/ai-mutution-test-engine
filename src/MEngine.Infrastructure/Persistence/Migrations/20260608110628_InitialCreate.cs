using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEngine.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SecretKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EndpointUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PullRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentConfigurationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SecretKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MaxIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputFolder = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    NotifyPipeline = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentIteration = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionRuns_AgentConfigurations_AgentConfigurationId",
                        column: x => x.AgentConfigurationId,
                        principalTable: "AgentConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PullRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitResults_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionSteps_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FinalReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FinalReportPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FinalHtmlReportPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinalReports_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MutationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MutationScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReportPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    JsonReportPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Tool = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ThresholdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MutationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MutationReports_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NotificationStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineNotifications_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuildStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangedFilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    RepoSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TestFramework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProfileSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    MasterPromptApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryAnalyses_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Decision = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TargetFilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestDecisions_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Iteration = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    Passed = table.Column<int>(type: "INTEGER", nullable: false),
                    Failed = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestRuns_ExecutionRuns_ExecutionRunId",
                        column: x => x.ExecutionRunId,
                        principalTable: "ExecutionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigurations_AgentName",
                table: "AgentConfigurations",
                column: "AgentName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitResults_ExecutionRunId",
                table: "CommitResults",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionRuns_AgentConfigurationId",
                table: "ExecutionRuns",
                column: "AgentConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionSteps_ExecutionRunId",
                table: "ExecutionSteps",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_FinalReports_ExecutionRunId",
                table: "FinalReports",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_MutationReports_ExecutionRunId",
                table: "MutationReports",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineNotifications_ExecutionRunId",
                table: "PipelineNotifications",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryAnalyses_ExecutionRunId",
                table: "RepositoryAnalyses",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDecisions_ExecutionRunId",
                table: "TestDecisions",
                column: "ExecutionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_ExecutionRunId",
                table: "TestRuns",
                column: "ExecutionRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitResults");

            migrationBuilder.DropTable(
                name: "ExecutionSteps");

            migrationBuilder.DropTable(
                name: "FinalReports");

            migrationBuilder.DropTable(
                name: "MutationReports");

            migrationBuilder.DropTable(
                name: "PipelineNotifications");

            migrationBuilder.DropTable(
                name: "RepositoryAnalyses");

            migrationBuilder.DropTable(
                name: "TestDecisions");

            migrationBuilder.DropTable(
                name: "TestRuns");

            migrationBuilder.DropTable(
                name: "ExecutionRuns");

            migrationBuilder.DropTable(
                name: "AgentConfigurations");
        }
    }
}
