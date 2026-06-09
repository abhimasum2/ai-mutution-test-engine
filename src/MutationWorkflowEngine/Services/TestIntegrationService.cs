using MutationWorkflowEngine.Models;

namespace MutationWorkflowEngine.Services;

internal sealed class TestIntegrationService
{
    public IReadOnlyList<(string SourceFileAbsolute, string RelativeSourceFile, string TestFileAbsolute, string RelativeTestFile)> BuildGenerationPlan(
        string repositoryRoot,
        string testProjectPath,
        IReadOnlyList<string> changedSourceFiles)
    {
        var testProjectDirectory = Path.GetDirectoryName(testProjectPath)
            ?? throw new InvalidOperationException("Invalid test project path.");

        var plan = new List<(string, string, string, string)>();
        foreach (var changedRelative in changedSourceFiles)
        {
            var sourceAbsolute = Path.GetFullPath(Path.Combine(repositoryRoot, changedRelative));
            if (!File.Exists(sourceAbsolute))
            {
                continue;
            }

            var sourceName = Path.GetFileNameWithoutExtension(changedRelative);
            var preferredTestName = sourceName + "Tests.cs";
            var preferredTestPath = FindExistingTestFile(testProjectDirectory, preferredTestName)
                ?? Path.Combine(testProjectDirectory, preferredTestName);

            var relativeTestPath = Path.GetRelativePath(repositoryRoot, preferredTestPath);
            plan.Add((sourceAbsolute, changedRelative, preferredTestPath, relativeTestPath));
        }

        return plan;
    }

    public async Task<IReadOnlyList<string>> ApplyGeneratedPatchesAsync(
        string repositoryRoot,
        IReadOnlyList<GeneratedTestPatch> patches,
        CancellationToken cancellationToken)
    {
        var updated = new List<string>();

        foreach (var patch in patches)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(repositoryRoot, patch.RelativeTestFilePath));
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(absolutePath, patch.Content, cancellationToken);
            updated.Add(patch.RelativeTestFilePath);
        }

        return updated.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? FindExistingTestFile(string root, string preferredName)
    {
        var exact = Directory.EnumerateFiles(root, preferredName, SearchOption.AllDirectories).FirstOrDefault();
        if (exact is not null)
        {
            return exact;
        }

        var fuzzy = Directory.EnumerateFiles(root, "*Tests.cs", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).StartsWith(preferredName.Replace("Tests.cs", string.Empty), StringComparison.OrdinalIgnoreCase));

        return fuzzy;
    }
}
