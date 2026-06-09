using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class ProjectDiscoveryService
{
    public async Task<(string TestProjectPath, TestingFramework Framework)> ResolveTestProjectAndFrameworkAsync(
        string repositoryRoot,
        string targetProjectPath,
        string? explicitTestProject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(explicitTestProject))
        {
            var framework = await DetectFrameworkFromProjectFileAsync(explicitTestProject, cancellationToken);
            return (explicitTestProject, framework);
        }

        var allProjectFiles = Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) && !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var targetDirectory = Path.GetDirectoryName(targetProjectPath) ?? repositoryRoot;
        var siblingCandidates = allProjectFiles
            .Where(path => !Path.GetFullPath(path).Equals(Path.GetFullPath(targetProjectPath), StringComparison.OrdinalIgnoreCase))
            .Where(path => IsLikelyTestProject(path, targetDirectory))
            .ToList();

        if (siblingCandidates.Count == 0)
        {
            throw new InvalidOperationException("No sibling test project detected. Provide --test explicitly.");
        }

        foreach (var candidate in siblingCandidates)
        {
            var framework = await DetectFrameworkFromProjectFileAsync(candidate, cancellationToken);
            if (framework != TestingFramework.Unknown)
            {
                return (candidate, framework);
            }
        }

        throw new InvalidOperationException("Found test project(s), but could not detect framework (NUnit/xUnit/MSTest).");
    }

    private static bool IsLikelyTestProject(string projectPath, string targetDirectory)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectPath);
        var isNamedLikeTest = fileName.Contains("Test", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Spec", StringComparison.OrdinalIgnoreCase);

        var isNearTarget = string.Equals(
            Directory.GetParent(projectPath)?.Parent?.FullName,
            Directory.GetParent(targetDirectory)?.FullName,
            StringComparison.OrdinalIgnoreCase);

        return isNamedLikeTest || isNearTarget;
    }

    private static async Task<TestingFramework> DetectFrameworkFromProjectFileAsync(string projectPath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(projectPath, cancellationToken);
        if (content.Contains("xunit", StringComparison.OrdinalIgnoreCase))
        {
            return TestingFramework.XUnit;
        }

        if (content.Contains("NUnit", StringComparison.OrdinalIgnoreCase))
        {
            return TestingFramework.NUnit;
        }

        if (content.Contains("MSTest", StringComparison.OrdinalIgnoreCase) || content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase))
        {
            return TestingFramework.MSTest;
        }

        return TestingFramework.Unknown;
    }
}
