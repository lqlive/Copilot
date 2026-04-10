using System.Diagnostics;
using System.Text;
using Copilot.Core.Abstractions;
using Copilot.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Copilot.Core;

public class DefaultWorkspaceManager : IWorkspaceManager
{
    private readonly WorkspaceOptions _workspaceOptions;
    private readonly GitClientOptions _gitClientOptions;
    private readonly ILogger<DefaultWorkspaceManager> _logger;
    public DefaultWorkspaceManager(
        IOptions<WorkspaceOptions> workspaceOptions,
        IOptions<GitClientOptions> gitClientOptions,
        ILogger<DefaultWorkspaceManager> logger)
    {
        _workspaceOptions = workspaceOptions.Value;
        _gitClientOptions = gitClientOptions.Value;
        _logger = logger;
    }

    public async Task<string> CloneAsync(string repositoryId, string branch, string cloneUrl, CancellationToken ct = default)
    {
        var workspacePath = Path.Combine(
            _workspaceOptions.BaseTempPath ?? Path.Combine(Path.GetTempPath(), "copilot"),
            $"{Sanitize(repositoryId)}-{Guid.NewGuid():N}");


        Directory.CreateDirectory(workspacePath);
        Log.RepositoryCloning(_logger, repositoryId, branch, workspacePath);

        await InvokeAsync(workspacePath, ["clone", "--depth", "1", "--branch", branch, "--single-branch", cloneUrl, "."], _gitClientOptions, ct);
        return workspacePath;
    }

    public async Task<string> CommitAndPushAsync(string workspacePath, string branchName, string commitMessage, CancellationToken ct = default)
    {
        var status = await InvokeAsync(workspacePath, ["status", "--porcelain"],_gitClientOptions, ct);
        if (string.IsNullOrWhiteSpace(status))
        {
            Log.NoChangesToCommit(_logger, workspacePath);
            return branchName;
        }

        await InvokeAsync(workspacePath, ["add", "-A"],_gitClientOptions ,ct);
        await InvokeAsync(workspacePath, ["commit", "-m", commitMessage], _gitClientOptions, ct);
        await InvokeAsync(workspacePath, ["push", "origin", branchName], _gitClientOptions, ct);
        Log.BranchPushed(_logger, branchName);
        return branchName;
    }

    public async Task<string> CreateBranchAsync(string workspacePath, string branchName, CancellationToken ct = default)
    {
        await InvokeAsync(workspacePath, ["checkout", "-b", branchName], _gitClientOptions, ct);
        Log.BranchCreated(_logger, branchName);
        return branchName;
    }
    public Task CleanupAsync(string workspacePath)
    {
        try
        {
            if (!Directory.Exists(workspacePath)) return Task.CompletedTask;

            foreach (var f in Directory.EnumerateFiles(
                workspacePath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }
            Directory.Delete(workspacePath, recursive: true);
            Log.WorkspaceCleanedUp(_logger, workspacePath);
        }
        catch (Exception ex)
        {
            Log.WorkspaceCleanupFailed(_logger, workspacePath, ex);
        }
        return Task.CompletedTask;
    }

    private async Task<string> InvokeAsync(string workingDir, string[] args, GitClientOptions gitClientOptions, CancellationToken cancellationToken)
    {
        if (RequiresRemoteAuth(args))
        {
            args = ApplyHttpPatAuth(args, gitClientOptions);
        }

        var escaped = string.Join(" ", args.Select(ProcessArgumentEscaper.Escape));
        var stdoutBuf = new StringBuilder();
        var stderrBuf = new StringBuilder();
        Log.GitCommandExecuting(_logger, escaped);

        var psi = new ProcessStartInfo
        {
            FileName = gitClientOptions.GitPath,
            Arguments = escaped,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var enviroment in gitClientOptions.Environment ?? new Dictionary<string, string>())
        {
            psi.Environment[enviroment.Key] = enviroment.Value;
        }

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(cancellationToken) is { } line)
            {
                lock (stderrBuf) stderrBuf.AppendLine(line);
            }
        }, cancellationToken);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        string stderr;
        lock (stderrBuf) stderr = stderrBuf.ToString().Trim();
        if (process.ExitCode != 0)
        {
            Log.GitCommandFailed(_logger, escaped, process.ExitCode, stderr);
            throw new IOException(
                $"git {escaped} failed (exit {process.ExitCode}).\nstderr: {stderr}");
        }
        return stdout;
    }
 
    private static string[] ApplyHttpPatAuth(string[] args, GitClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return args;
        }
        var username = string.IsNullOrWhiteSpace(options.AgentName)
            ? "oauth2"
            : options.AgentName;
        var auth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{options.AccessToken}"));
        return
        [
            "-c",
            $"http.extraHeader=Authorization: Basic {auth}",
        .. args
        ];
    }
    private static bool RequiresRemoteAuth(string[] args)
    {
        if (args.Length == 0) return false;
        return args[0] is "clone" or "fetch" or "pull" or "push" or "ls-remote";
    }

    private static class ProcessArgumentEscaper
    {
        public static string Escape(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            if (!NeedsQuoting(arg)) return arg;
            return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
        private static bool NeedsQuoting(string arg) =>
            arg.Any(c => char.IsWhiteSpace(c) || c is '"' or '\\' or '\'');
    }
    private static string Sanitize(string input) =>
        string.Concat(input.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static class Log
    {
        private static readonly Action<ILogger, string, string, string, Exception?> _repositoryCloning =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Information,
                new EventId(1, nameof(RepositoryCloning)),
                "Cloning {Repository} ({Branch}) → {Path}");

        private static readonly Action<ILogger, string, Exception?> _noChangesToCommit =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(2, nameof(NoChangesToCommit)),
                "No changes to commit in {Path}");

        private static readonly Action<ILogger, string, Exception?> _branchPushed =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(3, nameof(BranchPushed)),
                "Pushed branch {Branch}");

        private static readonly Action<ILogger, string, Exception?> _branchCreated =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(4, nameof(BranchCreated)),
                "Created branch {Branch}");

        private static readonly Action<ILogger, string, Exception?> _workspaceCleanedUp =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(5, nameof(WorkspaceCleanedUp)),
                "Cleaned up {Path}");

        private static readonly Action<ILogger, string, Exception?> _workspaceCleanupFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(6, nameof(WorkspaceCleanupFailed)),
                "Failed to cleanup {Path}");

        private static readonly Action<ILogger, string, Exception?> _gitCommandExecuting =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(7, nameof(GitCommandExecuting)),
                "git {Args}");

        private static readonly Action<ILogger, string, int, string, Exception?> _gitCommandFailed =
            LoggerMessage.Define<string, int, string>(
                LogLevel.Error,
                new EventId(8, nameof(GitCommandFailed)),
                "git {Args} failed (exit {Code}):\n{Stderr}");

        public static void RepositoryCloning(ILogger logger, string repository, string branch, string path) =>
            _repositoryCloning(logger, repository, branch, path, null);

        public static void NoChangesToCommit(ILogger logger, string path) =>
            _noChangesToCommit(logger, path, null);

        public static void BranchPushed(ILogger logger, string branch) =>
            _branchPushed(logger, branch, null);

        public static void BranchCreated(ILogger logger, string branch) =>
            _branchCreated(logger, branch, null);

        public static void WorkspaceCleanedUp(ILogger logger, string path) =>
            _workspaceCleanedUp(logger, path, null);

        public static void WorkspaceCleanupFailed(ILogger logger, string path, Exception? exception) =>
            _workspaceCleanupFailed(logger, path, exception);

        public static void GitCommandExecuting(ILogger logger, string args) =>
            _gitCommandExecuting(logger, args, null);

        public static void GitCommandFailed(ILogger logger, string args, int code, string stderr) =>
            _gitCommandFailed(logger, args, code, stderr, null);
    }
}