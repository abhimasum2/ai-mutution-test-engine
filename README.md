# Mutation-Test-Engine

ASP.NET Core 10 Web API for orchestrating a mutation-driven test workflow around repository analysis, mutation scoring, test decisions, test execution, reporting, and pipeline notification.

This repository currently provides the orchestration API surface, persistence model, migration, and placeholder infrastructure services needed to wire the end-to-end flow together. Several external integrations are still implemented as stubs or deterministic sample services and should be replaced before using this in a real delivery pipeline.

## What This Service Does

The API manages a run-oriented workflow:

1. Validate an agent configuration.
2. Create a run bound to a repository and pull request.
3. Profile the repository and store metadata.
4. Analyze repository and build state.
5. Generate a mutation report.
6. Decide whether tests should be created, updated, skipped, or manually reviewed.
7. Execute the chosen test action.
8. Run tests.
9. Record commit metadata for successful test changes.
10. Generate final report artifacts.
11. Notify the downstream pipeline.

Each major step is persisted and correlated through `ExecutionRun`, `ExecutionStep`, and the related workflow entities in the SQLite database.

## Solution Structure

- `src/MEngine.Api`
  ASP.NET Core host, controllers, middleware, Swagger/OpenAPI configuration.
- `src/MEngine.Application`
  Use-case orchestration, DTOs, repository abstractions, service abstractions, application exceptions.
- `src/MEngine.Domain`
  Core entities, enums, and auditable base model.
- `src/MEngine.Infrastructure`
  EF Core DbContext, entity configurations, repositories, migration, and external service implementations.

## Quick Start

### Prerequisites

- .NET SDK 10
- EF Core tools available through `dotnet ef`

### Restore and Build

```powershell
dotnet restore
dotnet build M-Engine.slnx
```

### Apply the Database Migration

```powershell
dotnet ef database update --project src/MEngine.Infrastructure/MEngine.Infrastructure.csproj --startup-project src/MEngine.Api/MEngine.Api.csproj
```

### Run the API

```powershell
dotnet run --project src/MEngine.Api/MEngine.Api.csproj
```

Swagger is available during development at `http://localhost:5268/swagger/index.html`.

## Configuration

Default application configuration lives in:

- `src/MEngine.Api/appsettings.json`
- `src/MEngine.Api/appsettings.Development.json`

Relevant settings:

- `ConnectionStrings:MEngineDb`
  SQLite database location.
- `Orchestration:DefaultOutputFolder`
  Default artifact root.
- `Orchestration:DefaultMaxIterations`
  Default mutation/test loop limit.

## API Surface

### Agent Configuration

- `POST /api/agent-configurations/validate`

### Runs

- `POST /api/runs`
- `GET /api/runs/{runId}`
- `POST /api/runs/{runId}/profile`
- `POST /api/runs/{runId}/repository-analysis`
- `GET /api/runs/{runId}/repository-analysis`
- `POST /api/runs/{runId}/mutation-reports`
- `GET /api/runs/{runId}/mutation-reports/latest`
- `POST /api/runs/{runId}/test-decision`
- `POST /api/runs/{runId}/tests/actions`
- `POST /api/runs/{runId}/test-runs`
- `GET /api/runs/{runId}/test-runs/{testRunId}`
- `POST /api/runs/{runId}/commits`
- `POST /api/runs/{runId}/final-reports`
- `GET /api/runs/{runId}/final-reports/latest`
- `POST /api/runs/{runId}/pipeline-notifications`

## Documentation

- `docs/architecture.md`
- `docs/api-reference.md`
- `docs/local-development.md`

## Current Implementation Notes

- `GitService` currently returns a fixed successful analysis result and a synthetic PR branch.
- `AgentProfilingService` performs lightweight validation and heuristic language detection.
- `StrykerMutationTestingService` builds the mutation command and output paths but returns a sample score.
- `TestGenerationService` returns generated target file paths without modifying test projects.
- `TestExecutionService` returns a successful fixed result.
- `CommitService` returns a synthetic commit SHA.
- `PipelineNotifier` returns a deterministic notification message.

That means the orchestration API and persistence model are implemented, but the infrastructure integrations still need real git, agent, mutation, test, and pipeline adapters.

## Artifacts and Outputs

Generated report artifacts are written under the run output folder. The default implementation creates:

- `mutation/mutation-report.html`
- `mutation/mutation-report.json`
- `final-report.json`
- `final-report.html`

## Error Handling

Unhandled exceptions are converted into ProblemDetails responses. Known application exceptions map to:

- `NotFoundException` -> `404 Not Found`
- `ConflictException` -> `409 Conflict`
- all other exceptions -> `500 Internal Server Error`

Each response includes the request trace identifier, and the controllers also accept `X-Correlation-ID` so orchestration records can be traced end to end.

## Recommended Next Implementation Steps

- Replace placeholder infrastructure services with real adapters for git hosting, agent execution, mutation tooling, and CI notifications.
- Add authentication and authorization around orchestration endpoints.
- Add automated tests for controller behavior, orchestration branching, and repository adapters.
- Introduce background processing for long-running workflow stages.