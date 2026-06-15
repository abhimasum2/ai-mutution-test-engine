# Mutation Engine - User Manual (Step-by-Step)

## Table of Contents
1. Prerequisites
2. Configuration Setup
3. Running the Engine
4. Understanding Outputs
5. Example Walkthrough
6. Troubleshooting

---

## 1. Prerequisites

### Required Software
- **.NET SDK 10+** — Download from https://dotnet.microsoft.com/download
- **Git** — Configured in PATH
- **Stryker.NET CLI** — Install locally:
  ```powershell
  dotnet tool install --local dotnet-stryker
  ```

### Required Credentials
- **OpenAI API Key** — Get from https://platform.openai.com/api-keys
  - Set env var: `$env:OPENAI_API_KEY = "your-key"`
  - OR provide in `input.json`

### Project Structure
Your target repository must have:
```
target-repo/
├── src/
│   └── MyProject/
│       ├── MyProject.csproj
│       └── *.cs files
└── tests/
    └── MyProject.Tests/
        ├── MyProject.Tests.csproj
        └── *Tests.cs files
```

---

## 2. Configuration Setup

### Step 1: Create `input.json`
Create or update `input.json` in the engine directory:

```json
{
  "RepositoryRoot": "../target-repo",
  "TargetProjectPath": "src/MyProject/MyProject.csproj",
  "TestProjectPath": "tests/MyProject.Tests/MyProject.Tests.csproj",
  "ReportsDirectory": "mutation-reports/local-run",
  "BaseRef": "HEAD~1",
  "OpenAiModel": "gpt-4o",
  "OpenAiApiKey": "",
  "OpenAiBaseUrl": "https://api.openai.com/v1/",
  "CommitAndPush": false,
  "GenerationMaxIterations": 3,
  "MaxSourceFileChars": 24000,
  "MaxConcurrency": 2,
  "ProcessTimeoutMinutes": 30,
  "Verbose": true
}
```

### Step 2: Validate Configuration
```powershell
# Check that paths exist
Test-Path "../target-repo/src/MyProject/MyProject.csproj"
Test-Path "../target-repo/tests/MyProject.Tests/MyProject.Tests.csproj"

# Verify OpenAI API key is set
$env:OPENAI_API_KEY
# Should NOT be empty
```

### Step 3: Prepare Changed Files
The engine detects changes from `BaseRef` (default `HEAD~1`).

To test locally:
```powershell
cd ../target-repo

# Create a test branch
git checkout -b local/mutation-test

# Make a small source code change
Add-Content ./src/MyProject/MyClass.cs "// test change"

# Stage the change
git add src/MyProject/MyClass.cs
git commit -m "test change for mutation"

# Return to engine repo
cd ../ai-mutution-test-engine
```

---

## 3. Running the Engine

### Option A: Using `input.json` (Recommended)

```powershell
dotnet run --project src/MutationWorkflowEngine/MutationWorkflowEngine.csproj -- --input input.json
```

### Option B: Using CLI Overrides

```powershell
dotnet run --project src/MutationWorkflowEngine/MutationWorkflowEngine.csproj `
  --repo ../target-repo `
  --target src/MyProject/MyProject.csproj `
  --test tests/MyProject.Tests/MyProject.Tests.csproj `
  --base HEAD~1 `
  --verbose true
```

### Option C: Using PowerShell Script

```powershell
./run-local.ps1 -InputPath input.json
```

---

## 4. Understanding Outputs

### Stage 1: Framework and Project Discovery
```
[00:00:01] Resolving test project and framework...
[00:00:02] Detected framework: XUnit; test project: tests/MyProject.Tests/MyProject.Tests.csproj
```
✓ Verify: Test framework is correctly detected (NUnit, xUnit, or MSTest).

### Stage 2: Changed Files Detection
```
[00:00:03] Loading changed PR files using git diff...
[00:00:04] Changed source files: 2
```
✓ Verify: Count matches your expected changed files.

### Stage 3: Pre-Commit Mutation Analysis
```
[00:00:05] Running pre-commit mutation testing...
[00:00:20] Pre-commit score: 65.50%
[00:00:21] Pre-commit mutants identified: 120
[00:00:22] Pre-commit file: src/MyProject/MyClass.cs | killed=78, survived=35, total=113
```
✓ Verify: Score is computed, mutants are counted. Look for Survived or NoCoverage counts.

### Stage 4: AI Test Generation (if Actionable Mutants Exist)
```
[00:01:00] Actionable mutants found: 35 (Survived/NoCoverage)
[00:01:01] Generating test updates from mutation report with AI (attempt 1/3)...
[00:01:15] Tokens used this attempt — input: 2500, output: 1200
[00:01:16] Updated/created test files: 1
```
✓ Verify: AI generation ran. Token counts are positive.

### Stage 5: Build Validation
```
[00:01:17] Validating test project build...
[00:01:25] Test project build passed.
```
✓ Verify: Build succeeded. If failed, look for compiler errors in output.

### Stage 6: Test Execution Validation
```
[00:01:26] Running test suite to validate generated tests...
[00:01:35] All tests passed.
```
✓ Verify: Tests executed successfully. If failed, look at test output.

### Stage 7: Post-Commit Mutation Analysis
```
[00:01:36] Running post-commit mutation testing...
[00:01:55] Post-commit score: 72.30%
[00:01:56] Post-commit mutants identified: 120
[00:01:57] Post-commit file: src/MyProject/MyClass.cs | killed=85, survived=28, total=113
```
✓ Verify: Post score > Pre score (or at least equal if no new tests).

### Stage 8: Report Generation
```
[00:02:00] Creating unified report...
[00:02:01] Unified report JSON: ../Reports/unified-mutation-report.json
[00:02:02] Unified report Markdown: ../Reports/unified-mutation-report.md
[00:02:03] Token usage report: ../Reports/token-usage-report.json
[00:02:04] Performance report: ../Reports/performance-report.json
[00:02:05] Summary HTML: ./MutationSummary.html
[00:02:06] Total engine runtime: 00:02:06
```
✓ Verify: All report files are created in `ReportsDirectory`.

---

## 5. Checking Reports

### Report Files
After successful run, check:

#### `MutationSummary.html` (Visual Dashboard)
- Open in browser: `open ./MutationSummary.html` or `start ./MutationSummary.html`
- View: Pre/Post scores, mutation delta, token usage, stage timings, mutant list

#### `unified-mutation-report.json` (Structured Data)
```json
{
  "pre": { "score": 65.5, "totalKilled": 78, "totalSurvived": 35 },
  "post": { "score": 72.3, "totalKilled": 85, "totalSurvived": 28 },
  "overallDeltaScore": 6.8,
  "files": [...]
}
```

#### `unified-mutation-report.md` (Human-Readable Summary)
Text format of pre/post analysis with per-file deltas.

#### `token-usage-report.json` (Cost Accounting)
```json
{
  "totalInputTokens": 2500,
  "totalOutputTokens": 1200,
  "totalTokens": 3700,
  "perFile": [...]
}
```

#### `performance-report.json` (Timing Breakdown)
```json
{
  "startedAtUtc": "2026-06-15T10:00:00Z",
  "finishedAtUtc": "2026-06-15T10:02:06Z",
  "totalDurationSeconds": 126,
  "stages": [
    { "stage": "Pre-commit Mutation", "durationSeconds": 15 },
    { "stage": "AI Test Generation", "durationSeconds": 45 },
    { "stage": "Post-commit Mutation", "durationSeconds": 14 }
  ]
}
```

---

## 6. Example Walkthrough

### Scenario: New Method Without Tests

#### Expected Behavior:
1. New method is added to `InvestmentCalculator.cs`.
2. No tests exist for it (NoCoverage mutants detected).
3. AI generates tests for the uncovered method.
4. Tests pass build and execution.
5. Post-commit score improves.

#### Step-by-Step:

```powershell
# 1. Prepare repo
cd ../ai-mutution-test-main-code
git checkout -b feature/new-method
# Add new method to InvestmentCalculator.cs
git add src/InvestmentCalculator/InvestmentCalculator.cs
git commit -m "Add new method"
cd ../ai-mutution-test-engine

# 2. Run engine
$env:OPENAI_API_KEY = "sk-..."
dotnet run --project src/MutationWorkflowEngine/MutationWorkflowEngine.csproj -- --input input.json

# 3. Check logs for:
#   - "Actionable mutants found: X (Survived/NoCoverage)"
#   - "Generating test updates from mutation report with AI"
#   - "Test project build passed"
#   - "All tests passed"
#   - "Post-commit score: Y%"

# 4. View report
start mutation-reports/local-run/MutationSummary.html

# 5. Verify improvement
#   - Post score > Pre score
#   - "Updated/created test files: 1"
#   - No failed tests
```

---

## 7. Troubleshooting

### Issue: "No changed .cs source files found in PR diff"
**Cause:** BaseRef does not match any changes.
**Fix:**
```powershell
# Check git history
git log --oneline -5
# Update BaseRef in input.json to a commit that has changes
# OR create a test change as shown in Step 3
```

### Issue: "No OpenAI provider configured"
**Cause:** OpenAiApiKey or OpenAiModel is missing.
**Fix:**
```powershell
$env:OPENAI_API_KEY = "sk-..."
# Or update input.json with valid key and model
```

### Issue: "No sibling test project detected"
**Cause:** Test project discovery failed.
**Fix:**
```powershell
# Explicitly provide test project path in input.json:
"TestProjectPath": "tests/MyProject.Tests/MyProject.Tests.csproj"
```

### Issue: "Generated tests did not build after all attempts"
**Cause:** AI generated syntactically invalid C#.
**Fix:**
1. Check last build output for compiler error.
2. Increase `GenerationMaxIterations` (default 3).
3. Review generated test file in `ReportsDirectory`.
4. Optionally update prompt in [OpenAiTestGenerationService.cs](../src/MutationWorkflowEngine/Services/OpenAiTestGenerationService.cs#L128).

### Issue: "Generated tests did not pass after all attempts"
**Cause:** Generated tests have logic errors or flaky assertions.
**Fix:**
1. Check test execution output.
2. Review generated test assertions in `ReportsDirectory`.
3. Increase `GenerationMaxIterations` to allow more retries.

### Issue: "Stryker run failed"
**Cause:** Stryker.NET CLI not installed or project misconfiguration.
**Fix:**
```powershell
# Reinstall Stryker
dotnet tool install --local dotnet-stryker

# Restore local tools
dotnet tool restore

# Verify installation
dotnet stryker --version
```

### Issue: Process timeout exceeded
**Cause:** Mutation analysis or AI call took too long.
**Fix:**
```json
{
  "ProcessTimeoutMinutes": 60,
  "MaxConcurrency": 1
}
```
(Reduce parallelism and increase timeout.)

---

## 8. Quick Reference

| Command | Purpose |
|---|---|
| `dotnet build` | Verify engine builds |
| `dotnet run -- --input input.json` | Run with configuration file |
| `dotnet run -- --verbose true` | Enable detailed logging |
| `dotnet tool restore` | Install local Stryker |
| `git diff HEAD~1 --name-only` | See files that will be analyzed |

| Config Key | Impact | Default |
|---|---|---|
| `GenerationMaxIterations` | Max AI retries | 3 |
| `MaxConcurrency` | Parallel AI calls | 4 |
| `ProcessTimeoutMinutes` | External command timeout | 30 |
| `BaseRef` | Git ref for change detection | origin/main |
| `CommitAndPush` | Auto-commit generated tests | false |

---

## 9. Success Checklist

After running the engine:
- [ ] No errors in console output.
- [ ] `MutationSummary.html` exists and renders in browser.
- [ ] Pre-commit score is reported.
- [ ] Post-commit score is reported and > pre score.
- [ ] Token usage is logged.
- [ ] Performance timings are logged.
- [ ] If AI generation ran:
  - [ ] "All tests passed" message appears.
  - [ ] No build failures.
  - [ ] Updated test files in `ReportsDirectory`.
- [ ] If AI generation skipped:
  - [ ] "No actionable mutants" message appears.
  - [ ] Workflow continues to post-mutation and reports.

---

## 10. Next Steps

After verifying a successful run:
1. **Review the HTML report** — Identify which mutants are still uncovered.
2. **Iterate** — Make more source code changes and re-run the engine.
3. **Commit tests** — If `CommitAndPush=true`, generated tests are auto-committed.
4. **Analyze trends** — Compare pre/post scores across multiple runs to track test quality.
