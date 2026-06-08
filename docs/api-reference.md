# API Reference

## Base URL

Local development default:

```text
http://localhost:5268
```

## Headers

Optional header used for traceability:

```http
X-Correlation-ID: any-client-generated-id
```

If omitted, the API uses the ASP.NET request trace identifier.

## Error Format

Unhandled application errors are returned as `application/problem+json`.

Examples:

- `404` for missing resources
- `409` for business rule conflicts
- `500` for unhandled exceptions

## Workflow Summary

Typical call order:

1. `POST /api/agent-configurations/validate`
2. `POST /api/runs`
3. `POST /api/runs/{runId}/profile`
4. `POST /api/runs/{runId}/repository-analysis`
5. `POST /api/runs/{runId}/mutation-reports`
6. `POST /api/runs/{runId}/test-decision`
7. `POST /api/runs/{runId}/tests/actions`
8. `POST /api/runs/{runId}/test-runs`
9. `POST /api/runs/{runId}/commits`
10. `POST /api/runs/{runId}/final-reports`
11. `POST /api/runs/{runId}/pipeline-notifications`

## Agent Configuration

### Validate Agent Configuration

`POST /api/agent-configurations/validate`

Request:

```json
{
  "agentName": "copilot-agent",
  "secretKey": "sample-secret",
  "endpointUrl": "https://agent.example/api"
}
```

Response `200 OK`:

```json
{
  "status": "OK"
}
```

Notes:

- Creates or updates an `AgentConfiguration` row.
- Current validation only checks that the values are non-empty and that `endpointUrl` is a valid absolute URI.

## Run Management

### Create Run

`POST /api/runs`

Request:

```json
{
  "repositoryUrl": "https://github.com/example/repo",
  "pullRequestId": 42,
  "agentConfigurationId": "11111111-1111-1111-1111-111111111111",
  "secretKey": "sample-secret",
  "maxIterations": 3,
  "outputFolder": "artifacts",
  "notifyPipeline": true
}
```

Response `201 Created`:

```json
{
  "runId": "22222222-2222-2222-2222-222222222222",
  "status": "Pending"
}
```

Business rules:

- The referenced agent configuration must exist.
- The agent configuration must already be valid.

### Get Run Status

`GET /api/runs/{runId}`

Response `200 OK`:

```json
{
  "runId": "22222222-2222-2222-2222-222222222222",
  "status": "InProgress",
  "currentIteration": 1,
  "updatedAtUtc": "2026-06-08T11:30:00.0000000+00:00"
}
```

## Profiling and Repository Analysis

### Profile Run

`POST /api/runs/{runId}/profile`

Response `202 Accepted`:

```json
{
  "language": "C#",
  "testFramework": "xUnit",
  "profileSummary": "Master prompt profile selected based on repository metadata.",
  "masterPromptApplied": true
}
```

Notes:

- If a repository analysis row already exists with language data, the stored profile is returned.
- Current implementation uses a repository URL heuristic instead of inspecting repository contents.

### Analyze Repository

`POST /api/runs/{runId}/repository-analysis`

Response `202 Accepted`:

```json
{
  "buildStatus": "Success",
  "changedFiles": [
    "src/Service/MutationService.cs",
    "tests/MutationServiceTests.cs"
  ],
  "repoSummary": "Repository 'https://github.com/example/repo' analyzed successfully."
}
```

### Get Latest Repository Analysis

`GET /api/runs/{runId}/repository-analysis`

Response `200 OK`:

```json
{
  "buildStatus": "Success",
  "changedFiles": [
    "src/Service/MutationService.cs",
    "tests/MutationServiceTests.cs"
  ],
  "repoSummary": "Repository 'https://github.com/example/repo' analyzed successfully."
}
```

## Mutation Reports

### Generate Mutation Report

`POST /api/runs/{runId}/mutation-reports`

Request:

```json
{
  "testProjectPath": "tests/Project.Tests/Project.Tests.csproj",
  "solutionPath": "Project.sln",
  "reporters": ["html", "json"],
  "thresholds": {
    "high": 80,
    "low": 60,
    "break": 50
  }
}
```

Response `202 Accepted`:

```json
{
  "mutationScore": 78.5,
  "reportPath": "artifacts/22222222222222222222222222222222/mutation/mutation-report.html",
  "jsonReportPath": "artifacts/22222222222222222222222222222222/mutation/mutation-report.json",
  "tool": "Stryker.NET"
}
```

Notes:

- Current service constructs the expected Stryker command and output paths.
- It returns a deterministic sample score rather than invoking the tool.

### Get Latest Mutation Report

`GET /api/runs/{runId}/mutation-reports/latest`

Response `200 OK`:

```json
{
  "mutationScore": 78.5,
  "reportPath": "artifacts/22222222222222222222222222222222/mutation/mutation-report.html",
  "jsonReportPath": "artifacts/22222222222222222222222222222222/mutation/mutation-report.json",
  "tool": "Stryker.NET"
}
```

## Test Decision and Actions

### Decide Test Action

`POST /api/runs/{runId}/test-decision`

Response `200 OK`:

```json
{
  "decision": "UpdateTests",
  "reason": "Mutation score between 60 and 80. Update existing tests.",
  "targetFiles": [
    "src/Service/MutationService.cs",
    "tests/MutationServiceTests.cs"
  ]
}
```

Decision rules:

- No mutation report: `ManualReviewRequired`
- Score lower than `60`: `CreateTests`
- Score from `60` up to `79.99`: `UpdateTests`
- Score `80` or higher: `Skip`

### Execute Test Action

`POST /api/runs/{runId}/tests/actions`

Request:

```json
{
  "decision": "UpdateTests",
  "maxIterations": 3,
  "agentPromptOverride": "Focus on mutation survivors in service layer.",
  "targetProjects": [
    "tests/Project.Tests"
  ]
}
```

Response `202 Accepted`:

```json
{
  "iteration": 3,
  "status": "Succeeded",
  "generatedFiles": [
    "tests/Project.Tests/Generated.Mutation.Tests.cs"
  ]
}
```

Notes:

- Current implementation only returns the file paths that would be generated or updated.

## Test Runs

### Execute Test Run

`POST /api/runs/{runId}/test-runs`

Response `202 Accepted`:

```json
{
  "testRunId": "33333333-3333-3333-3333-333333333333",
  "status": "Succeeded",
  "total": 120,
  "passed": 120,
  "failed": 0,
  "reportPath": "artifacts/22222222222222222222222222222222/test-results.trx"
}
```

### Get Test Run

`GET /api/runs/{runId}/test-runs/{testRunId}`

Response `200 OK`:

```json
{
  "testRunId": "33333333-3333-3333-3333-333333333333",
  "status": "Succeeded",
  "total": 120,
  "passed": 120,
  "failed": 0,
  "reportPath": "artifacts/22222222222222222222222222222222/test-results.trx"
}
```

## Commit and Reporting

### Commit Changes

`POST /api/runs/{runId}/commits`

Response `202 Accepted`:

```json
{
  "commitSha": "abc123def456",
  "branch": "refs/heads/pr/42",
  "pullRequestId": 42
}
```

Business rules:

- At least one test run must exist.
- The latest test run must have status `Succeeded`.

### Generate Final Report

`POST /api/runs/{runId}/final-reports`

Response `202 Accepted`:

```json
{
  "finalReportPath": "artifacts/22222222222222222222222222222222/final-report.json",
  "finalHtmlReportPath": "artifacts/22222222222222222222222222222222/final-report.html"
}
```

### Get Latest Final Report

`GET /api/runs/{runId}/final-reports/latest`

Response `200 OK`:

```json
{
  "finalReportPath": "artifacts/22222222222222222222222222222222/final-report.json",
  "finalHtmlReportPath": "artifacts/22222222222222222222222222222222/final-report.html"
}
```

### Notify Pipeline

`POST /api/runs/{runId}/pipeline-notifications`

Response `202 Accepted`:

```json
{
  "notificationStatus": "Notified: artifacts/22222222222222222222222222222222/final-report.json"
}
```

Business rule:

- A final report must already exist for the run.