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
            var preferredNewPath = BuildNewTestFilePath(testProjectDirectory, changedRelative, preferredTestName);
            var preferredTestPath = FindExistingTestFile(testProjectDirectory, changedRelative, preferredTestName, preferredNewPath)
                ?? preferredNewPath;

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
        var fullRepositoryRoot = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        foreach (var patch in patches)
        {
            var safeRelativePath = NormalizeRelativePath(patch.RelativeTestFilePath);
            if (string.IsNullOrWhiteSpace(safeRelativePath))
            {
                continue;
            }

            var absolutePath = Path.GetFullPath(Path.Combine(repositoryRoot, safeRelativePath));
            if (!absolutePath.StartsWith(fullRepositoryRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(absolutePath, patch.Content, cancellationToken);
            updated.Add(safeRelativePath);
        }

        return updated.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var normalized = relativePath
            .Trim()
            .Replace('\t', Path.DirectorySeparatorChar)
            .Replace('\r', Path.DirectorySeparatorChar)
            .Replace('\n', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment) && segment != "." && segment != "..")
            .ToArray();

        return segments.Length == 0 ? string.Empty : Path.Combine(segments);
    }

    private static string? FindExistingTestFile(
        string root,
        string relativeSourceFile,
        string preferredName,
        string preferredNewPath)
    {
        if (File.Exists(preferredNewPath))
        {
            return preferredNewPath;
        }

        var exactMatches = Directory.EnumerateFiles(root, preferredName, SearchOption.AllDirectories).ToList();
        if (exactMatches.Count == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Count > 1)
        {
            var sourceRelativeDirectory = Path.GetDirectoryName(relativeSourceFile) ?? string.Empty;
            var normalizedSourceDir = sourceRelativeDirectory
                .Replace('\\', '/')
                .Trim('/');

            if (normalizedSourceDir.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedSourceDir = normalizedSourceDir[4..];
            }

            var scopedExact = exactMatches.FirstOrDefault(path =>
                path.Replace('\\', '/').Contains("/" + normalizedSourceDir + "/", StringComparison.OrdinalIgnoreCase));

            if (scopedExact is not null)
            {
                return scopedExact;
            }
        }

        var fuzzy = Directory.EnumerateFiles(root, "*Tests.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).StartsWith(preferredName.Replace("Tests.cs", string.Empty), StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).Equals(preferredName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return fuzzy;
    }

    private static string BuildNewTestFilePath(string testProjectDirectory, string relativeSourceFile, string preferredTestName)
    {
        var sourceDirectory = Path.GetDirectoryName(relativeSourceFile) ?? string.Empty;
        var normalizedSourceDirectory = sourceDirectory
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var segments = normalizedSourceDirectory
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count > 0 && segments[0].Equals("src", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(0);
        }

        var targetDirectory = segments.Count == 0
            ? testProjectDirectory
            : Path.Combine(testProjectDirectory, Path.Combine(segments.ToArray()));

        return Path.Combine(targetDirectory, preferredTestName);
    }
}
