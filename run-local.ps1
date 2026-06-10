param(
    [string]$InputPath = "./input.json"
)

$ErrorActionPreference = "Stop"

Write-Host "[setup] Engine local run starting..."

if (-not (Test-Path $InputPath)) {
    throw "Input file not found: $InputPath"
}

# Ensure local tool manifest is available.
if (-not (Test-Path "./dotnet-tools.json")) {
    dotnet new tool-manifest | Out-Null
}

$localTools = dotnet tool list --local | Out-String
if ($localTools -notmatch "dotnet-stryker") {
    Write-Host "[setup] Installing local tool dotnet-stryker..."
    dotnet tool install --local dotnet-stryker
}

Write-Host "[setup] Restoring local dotnet tools..."
dotnet tool restore

Write-Host "[setup] Building mutation workflow engine..."
dotnet build ./src/MutationWorkflowEngine/MutationWorkflowEngine.csproj --configuration Release

Write-Host "[setup] Running engine with input file $InputPath"
dotnet run --project ./src/MutationWorkflowEngine/MutationWorkflowEngine.csproj --configuration Release --no-build -- --input $InputPath

Write-Host "[done] Local run complete."
