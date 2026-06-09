# MutationWorkflowEngine

A .NET 8 console application for PR-focused mutation testing and AI-assisted test generation.

## What it does

1. Detects test framework in sibling test project (NUnit, xUnit, MSTest).
2. Uses git diff against base ref to discover changed C# source files in a PR.
3. Runs Stryker.NET on changed files and creates a pre-commit mutation report.
4. Calls OpenAI to generate or update test files for the changed source files.
5. Optionally commits and pushes generated tests back to the active PR branch.
6. Runs Stryker.NET again and creates a post-commit mutation report.
7. Produces a unified report with pre/post mutation score deltas.
8. Writes all reports under a designated report folder.

## Prerequisites

- .NET SDK 8+
- git configured in PATH
- Stryker.NET CLI available (`dotnet tool install -g dotnet-stryker`)
- Update `mutationworkflow.config.json` with your real project paths
- OpenAI API key in `mutationworkflow.config.json` (`OpenAiApiKey`) or env var `OPENAI_API_KEY`
- Optional local fallback: Ollama running at `http://localhost:11434`

## Build

```powershell
dotnet build src/MutationWorkflowEngine/MutationWorkflowEngine.csproj
```

## Run

```powershell
dotnet run --project src/MutationWorkflowEngine/MutationWorkflowEngine.csproj -- \
  --config ./mutationworkflow.config.json
```

## Configuration file

Main settings are loaded from `mutationworkflow.config.json`:

- `RepositoryRoot`
- `TargetProjectPath`
- `TestProjectPath` (optional)
- `ReportsDirectory`
- `BaseRef`
- `OpenAiModel`
- `OpenAiApiKey` (optional if env var is set)
- `UseOllamaFallback`
- `OllamaBaseUrl`
- `OllamaModel`
- `CommitAndPush`
- `MaxSourceFileChars`
- `MaxConcurrency`
- `ProcessTimeoutMinutes`
- `Verbose`

## CLI overrides

All config keys can still be overridden at runtime when needed:

- `--config` path to configuration file (default: `./mutationworkflow.config.json`)
- `--repo` repository root
- `--target` target app csproj
- `--test` test project csproj (optional; auto-discovered if omitted)
- `--base` git base ref for PR diff
- `--reports` report output folder
- `--openai-key` OpenAI API key
- `--openai-model` OpenAI model name
- `--ollama-fallback` true/false to enable local fallback
- `--ollama-url` Ollama base URL (default `http://localhost:11434`)
- `--ollama-model` Ollama model name
- `--commit` true/false to commit and push generated tests
- `--max-source-chars` per-file prompt cap for large files
- `--max-concurrency` OpenAI parallel generation workers
- `--process-timeout-minutes` timeout for external commands and API calls
- `--verbose` true/false log output

## Output structure

- `mutation-reports/pre-commit/**` Stryker pre-commit output and report
- `mutation-reports/post-commit/**` Stryker post-commit output and report
- `mutation-reports/unified-mutation-report.json`
- `mutation-reports/unified-mutation-report.md`
