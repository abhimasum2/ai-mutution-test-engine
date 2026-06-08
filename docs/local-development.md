# Local Development

## Prerequisites

- Windows, macOS, or Linux with .NET SDK 10 installed
- `dotnet ef` available
- Optional: an HTTP client such as Swagger UI, curl, or Postman

## Repository Setup

Restore packages:

```powershell
dotnet restore
```

Build the solution:

```powershell
dotnet build M-Engine.slnx
```

## Database Setup

The API uses SQLite through EF Core.

Default connection strings:

- Development: `Data Source=m-engine.dev.db`
- Default: `Data Source=m-engine.db`

Apply the initial migration:

```powershell
dotnet ef database update --project src/MEngine.Infrastructure/MEngine.Infrastructure.csproj --startup-project src/MEngine.Api/MEngine.Api.csproj
```

## Running the API

```powershell
dotnet run --project src/MEngine.Api/MEngine.Api.csproj
```

Default development endpoints from `launchSettings.json`:

- `http://localhost:5268`
- `https://localhost:7091`

Swagger UI:

```text
http://localhost:5268/swagger/index.html
```

## Recommended Manual Test Flow

### 1. Validate an Agent Configuration

```powershell
$headers = @{ "X-Correlation-ID" = "local-doc-test-001" }

$validateBody = @{
    agentName = "copilot-agent"
    secretKey = "sample-secret"
    endpointUrl = "https://agent.example/api"
} | ConvertTo-Json

Invoke-RestMethod \
    -Method Post \
    -Uri "http://localhost:5268/api/agent-configurations/validate" \
    -Headers $headers \
    -ContentType "application/json" \
    -Body $validateBody
```

Important note:

- The validate endpoint returns only status.
- The current API does not expose a read endpoint for agent configurations.
- To create a run you need the `AgentConfigurationId` persisted in the database.

### 2. Create a Run

Replace the placeholder GUID with the real `AgentConfiguration.Id` from the SQLite database.

```powershell
$createRunBody = @{
    repositoryUrl = "https://github.com/example/repo"
    pullRequestId = 42
    agentConfigurationId = "11111111-1111-1111-1111-111111111111"
    secretKey = "sample-secret"
    maxIterations = 3
    outputFolder = "artifacts"
    notifyPipeline = $true
} | ConvertTo-Json

Invoke-RestMethod \
    -Method Post \
    -Uri "http://localhost:5268/api/runs" \
    -Headers $headers \
    -ContentType "application/json" \
    -Body $createRunBody
```

### 3. Progress the Workflow

Use the `runId` returned from the create call.

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/profile" -Headers $headers
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/repository-analysis" -Headers $headers
```

Mutation report request:

```powershell
$mutationBody = @{
    testProjectPath = "tests/Project.Tests/Project.Tests.csproj"
    solutionPath = "Project.sln"
    reporters = @("html", "json")
    thresholds = @{
        high = 80
        low = 60
        break = 50
    }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod \
    -Method Post \
    -Uri "http://localhost:5268/api/runs/{runId}/mutation-reports" \
    -Headers $headers \
    -ContentType "application/json" \
    -Body $mutationBody
```

Continue:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/test-decision" -Headers $headers
```

Test action request:

```powershell
$actionBody = @{
    decision = "UpdateTests"
    maxIterations = 3
    agentPromptOverride = "Focus on mutation survivors in service layer."
    targetProjects = @("tests/Project.Tests")
} | ConvertTo-Json

Invoke-RestMethod \
    -Method Post \
    -Uri "http://localhost:5268/api/runs/{runId}/tests/actions" \
    -Headers $headers \
    -ContentType "application/json" \
    -Body $actionBody
```

Then:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/test-runs" -Headers $headers
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/commits" -Headers $headers
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/final-reports" -Headers $headers
Invoke-RestMethod -Method Post -Uri "http://localhost:5268/api/runs/{runId}/pipeline-notifications" -Headers $headers
```

## Working With the SQLite Database

Useful local files are typically created under `src/MEngine.Api` because that is the startup project.

Examples:

- `m-engine.dev.db`
- `m-engine.dev.db-shm`
- `m-engine.dev.db-wal`

These files should not be committed. The repository already includes `.gitignore` rules for `*.db`, `bin/`, `obj/`, and related local artifacts.

## Known Limitations

- The service is structurally complete, but several integrations are placeholders.
- The repository analysis, profiling, mutation execution, test execution, commit, and pipeline notification adapters do not yet call real external systems.
- There is no API endpoint yet to list agent configurations or execution steps.
- There are currently no automated tests in this repository.

## Recommended Next Steps

1. Replace the placeholder infrastructure services with real adapters.
2. Add integration tests for the orchestration service and controllers.
3. Expose read endpoints for execution steps and agent configurations.
4. Add authentication and secret management before connecting real services.