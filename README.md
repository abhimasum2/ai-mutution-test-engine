# MutationWorkflowEngine

A .NET 10 console application for PR-focused mutation testing and AI-assisted test generation.

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

- .NET SDK 10+
- git configured in PATH
- Stryker.NET CLI available (`dotnet tool install -g dotnet-stryker`)
- Update `input.json` with your real project paths
- OpenAI API key in `input.json` (`OpenAiApiKey`) or env var `OPENAI_API_KEY`
- Optional local fallback: Gemini via `GoogleApiKey` in `input.json` or env var `GOOGLE_API_KEY`

## Build

```powershell
dotnet build src/MutationWorkflowEngine/MutationWorkflowEngine.csproj
```

## Local quick setup

```powershell
# from ai-mutution-test-engine
$env:GOOGLE_API_KEY = "your-test-key"

# ensure at least one changed .cs file exists for diff against BaseRef (default HEAD~1)
Set-Location ../ai-mutution-test-main-code
git checkout -b local/mutation-test
Add-Content ./src/InvestmentCalculator/InvestmentCalculator.cs "// local mutation test marker"

Set-Location ../ai-mutution-test-engine
./run-local.ps1
```

Notes:

- `input.json` is preconfigured for local testing and uses `BaseRef: HEAD~1`.
- `CommitAndPush` is disabled for local runs.
- Replace `GoogleApiKey` in `input.json` or use `GOOGLE_API_KEY` env var.

## Run

```powershell
dotnet run --project src/MutationWorkflowEngine/MutationWorkflowEngine.csproj -- \
  --input ./input.json
```

## Configuration file

Main settings are loaded from `input.json`:

- `RepositoryRoot`
- `TargetProjectPath`
- `TestProjectPath` (optional)
- `ReportsDirectory`
- `BaseRef`
- `OpenAiModel`
- `OpenAiApiKey` (optional if env var is set)
- `UseGeminiFallback`
- `GoogleApiKey` (optional if env var is set)
- `GeminiModel`
- `CommitAndPush`
- `MaxSourceFileChars`
- `MaxConcurrency`
- `ProcessTimeoutMinutes`
- `Verbose`

## CLI overrides

All config keys can still be overridden at runtime when needed:

- `--input` path to input file (default: `./input.json`)
- `--config` legacy alias for input file path
- `--repo` repository root
- `--target` target app csproj
- `--test` test project csproj (optional; auto-discovered if omitted)
- `--base` git base ref for PR diff
- `--reports` report output folder
- `--openai-key` OpenAI API key
- `--openai-model` OpenAI model name
- `--gemini-fallback` true/false to enable Gemini fallback
- `--google-key` Google API key
- `--gemini-model` Gemini model name (default `gemini-3.5-flash`)
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
