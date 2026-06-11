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

        await EnsureCommitIdentityAsync(repositoryRoot, cancellationToken, timeout);

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

    private async Task EnsureCommitIdentityAsync(
        string repositoryRoot,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var configuredName = await GetGitConfigAsync(repositoryRoot, "user.name", cancellationToken, timeout);
        var configuredEmail = await GetGitConfigAsync(repositoryRoot, "user.email", cancellationToken, timeout);

        if (!string.IsNullOrWhiteSpace(configuredName) && !string.IsNullOrWhiteSpace(configuredEmail))
        {
            return;
        }

        var actor = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        var fallbackName = string.IsNullOrWhiteSpace(actor) ? "github-actions[bot]" : actor;
        var fallbackEmail = string.IsNullOrWhiteSpace(actor)
            ? "41898282+github-actions[bot]@users.noreply.github.com"
            : $"{actor}@users.noreply.github.com";

        if (string.IsNullOrWhiteSpace(configuredName))
        {
            var setName = await _runner.RunAsync("git", $"config user.name \"{fallbackName}\"", repositoryRoot, timeout, cancellationToken);
            if (!setName.IsSuccess)
            {
                throw new InvalidOperationException($"git config user.name failed. {setName.StdErr}");
            }
        }

        if (string.IsNullOrWhiteSpace(configuredEmail))
        {
            var setEmail = await _runner.RunAsync("git", $"config user.email \"{fallbackEmail}\"", repositoryRoot, timeout, cancellationToken);
            if (!setEmail.IsSuccess)
            {
                throw new InvalidOperationException($"git config user.email failed. {setEmail.StdErr}");
            }
        }
    }

    private async Task<string?> GetGitConfigAsync(
        string repositoryRoot,
        string key,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var result = await _runner.RunAsync("git", $"config --get {key}", repositoryRoot, timeout, cancellationToken);
        if (!result.IsSuccess)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(result.StdOut) ? null : result.StdOut.Trim();
    }
}
