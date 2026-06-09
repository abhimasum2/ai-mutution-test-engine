namespace MutationWorkflowEngine.Services;

internal sealed class GitService
{
    private readonly ProcessRunner _runner = new();

    public async Task<IReadOnlyList<string>> GetChangedSourceFilesAsync(
        string repositoryRoot,
        string baseRef,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        // Compare base branch and HEAD to isolate PR changes.
        var diff = await _runner.RunAsync(
            "git",
            $"diff --name-only {baseRef}...HEAD",
            repositoryRoot,
            timeout,
            cancellationToken);

        if (!diff.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to query changed files. {diff.StdErr}");
        }

        return diff.StdOut
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("Test", StringComparison.OrdinalIgnoreCase) && !path.Contains("Tests", StringComparison.OrdinalIgnoreCase))
            .Select(path => path.Replace('/', Path.DirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task CommitAndPushAsync(
        string repositoryRoot,
        IReadOnlyList<string> filesToCommit,
        string message,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        if (filesToCommit.Count == 0)
        {
            return;
        }

        foreach (var file in filesToCommit)
        {
            var add = await _runner.RunAsync("git", $"add -- \"{file}\"", repositoryRoot, timeout, cancellationToken);
            if (!add.IsSuccess)
            {
                throw new InvalidOperationException($"git add failed for {file}. {add.StdErr}");
            }
        }

        var commit = await _runner.RunAsync("git", $"commit -m \"{message}\"", repositoryRoot, timeout, cancellationToken);
        if (!commit.IsSuccess)
        {
            if (commit.StdErr.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase) ||
                commit.StdOut.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidOperationException($"git commit failed. {commit.StdErr}");
        }

        var push = await _runner.RunAsync("git", "push", repositoryRoot, timeout, cancellationToken);
        if (!push.IsSuccess)
        {
            throw new InvalidOperationException($"git push failed. {push.StdErr}");
        }
    }
}
