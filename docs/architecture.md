# Architecture

## Overview

Mutation-Test-Engine is organized as a layered .NET service with a thin HTTP API, a single orchestration use-case layer, and EF Core persistence. The design centers on `ExecutionRun` and the workflow records that describe each step of a mutation-driven test improvement cycle.

## Layers

### API Layer

Project: `src/MEngine.Api`

Responsibilities:

- expose HTTP endpoints
- bind request DTOs and return response DTOs
- generate correlation IDs from `X-Correlation-ID` or the ASP.NET trace identifier
- publish OpenAPI metadata through Swagger
- convert unhandled exceptions into `application/problem+json` responses

Main pieces:

- `Program.cs`
- `Controllers/AgentConfigurationsController.cs`
- `Controllers/RunsController.cs`
- `Middleware/ProblemDetailsExceptionMiddleware.cs`

### Application Layer

Project: `src/MEngine.Application`

Responsibilities:

- define repository and external service contracts
- implement orchestration rules for each workflow step
- translate persisted state into API DTOs
- enforce workflow invariants

Main pieces:

- `Abstractions/Persistence/*`
- `Abstractions/Services/*`
- `DTOs/*`
- `Services/OrchestrationService.cs`

Important application rules currently enforced:

- a run cannot be created with an invalid agent configuration
- a commit cannot occur before a successful latest test run
- pipeline notification requires an existing final report

### Domain Layer

Project: `src/MEngine.Domain`

Responsibilities:

- define workflow entities and enums
- keep status vocabulary independent of transport and storage concerns

Shared base type:

- `Common/AuditableEntity.cs`

Core entities:

- `AgentConfiguration`
- `ExecutionRun`
- `ExecutionStep`
- `RepositoryAnalysis`
- `MutationReport`
- `TestDecision`
- `TestRun`
- `CommitResult`
- `FinalReport`
- `PipelineNotification`

Core enums:

- `RunStatus`
- `ExecutionStepStatus`
- `TestDecisionType`

### Infrastructure Layer

Project: `src/MEngine.Infrastructure`

Responsibilities:

- configure EF Core persistence
- implement repositories
- register dependencies in DI
- provide adapters for git, profiling, mutation testing, test generation, test execution, commits, artifacts, and pipeline notifications

Main pieces:

- `Persistence/MEngineDbContext.cs`
- `Persistence/Configurations/*`
- `Persistence/Repositories/Repositories.cs`
- `DependencyInjection/ServiceCollectionExtensions.cs`
- `Services/ExternalServices.cs`

## Request Flow

Each request follows the same high-level path:

1. A controller receives the HTTP request.
2. The controller resolves a correlation ID.
3. The controller calls `IOrchestrationService`.
4. `OrchestrationService` loads current state from repositories.
5. It invokes one external or internal service.
6. It persists the resulting entity state.
7. It records an `ExecutionStep` when appropriate.
8. It returns a DTO to the controller.

## Run Lifecycle

The intended lifecycle is:

1. validate agent configuration
2. create a run
3. profile repository
4. analyze repository and build state
5. generate mutation report
6. decide test action
7. execute test action
8. execute test run
9. record commit metadata after a successful test run
10. generate final report
11. notify pipeline

This lifecycle is intentionally modeled as separate API calls so an external orchestrator, UI, or pipeline can drive the process incrementally.

## Persistence Model

Database provider: SQLite

Configuration source: `ConnectionStrings:MEngineDb`

Notable persistence characteristics:

- `AgentConfiguration.AgentName` is unique
- `ExecutionRun.AgentConfigurationId` uses `DeleteBehavior.Restrict`
- most child workflow entities cascade from `ExecutionRun`
- structured data such as changed files and thresholds are stored as JSON strings
- every entity carries timestamps and a correlation ID through `AuditableEntity`

## Correlation and Observability

Each persisted entity carries a `CorrelationId`, which lets clients trace multi-step workflow activity across database records and logs.

`ExecutionStep` provides step-level observability through:

- step name
- status
- details
- timestamps
- correlation ID

## External Service Boundary

The infrastructure layer already exposes stable interfaces for integration points:

- `IGitService`
- `IAgentProfilingService`
- `IMutationTestingService`
- `ITestGenerationService`
- `ITestExecutionService`
- `ICommitService`
- `IArtifactFileService`
- `IPipelineNotifier`

That boundary is important because it allows the placeholder adapters to be replaced without changing controller contracts or the orchestration service surface.

## Current Gaps

The architecture and persistence model are in place, but several infrastructure adapters are still placeholders:

- repository analysis does not inspect a real repository
- profiling uses URL heuristics instead of source inspection
- mutation execution does not invoke Stryker yet
- test generation does not modify source code
- test execution returns a fixed successful result
- commit and push behavior is simulated
- pipeline notification is simulated

The next engineering step is to replace these adapters with real integrations while preserving the existing contracts and orchestration behavior.