using System.Diagnostics;
using System.Text;

using Paarde.Core.Contracts.Services;
using Paarde.Core.Models;

namespace Paarde.Core.Services;

// Detects whether workingDirectoryPath sits inside a git working tree and, if so, which files are
// currently dirty there (T4.3.1), by shelling out to `git status --porcelain` rather than taking a
// LibGit2Sharp-style native dependency - matching the ClaudeCliRunner convention of treating an
// external CLI as the source of truth. Any failure to run git at all (not installed, directory
// missing, not a repository) is treated the same as "not a repository": this is an informational
// warning, not a security boundary, so it fails open rather than blocking an apply.
public class GitStatusService : IGitStatusService
{
    public GitStatus GetStatus(string workingDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(workingDirectoryPath) || !Directory.Exists(workingDirectoryPath))
        {
            return GitStatus.NotARepository;
        }

        if (!TryRunGit(workingDirectoryPath, out var output))
        {
            return GitStatus.NotARepository;
        }

        var dirtyFiles = output
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 3)
            .Select(ParsePorcelainPath)
            .ToList();

        return new GitStatus(IsRepository: true, DirtyFiles: dirtyFiles);
    }

    // Each porcelain line is "XY <path>", or "XY <path> -> <newPath>" for a rename; the path starts
    // after the two status characters and the space that follows them. git always reports the path
    // relative to the process's working directory (set below to workingDirectoryPath) using forward
    // slashes, the same convention ProposedFileChangeViewModel.FilePath already uses.
    private static string ParsePorcelainPath(string porcelainLine)
    {
        var path = porcelainLine[3..];
        var arrowIndex = path.IndexOf(" -> ", StringComparison.Ordinal);

        return arrowIndex >= 0 ? path[(arrowIndex + 4)..] : path;
    }

    private static bool TryRunGit(string workingDirectoryPath, out string output)
    {
        output = string.Empty;

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectoryPath,
            ArgumentList = { "status", "--porcelain" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return false;
            }

            output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            // git isn't on PATH, or the OS refused to start it - treated the same as "not a
            // repository" (see class remarks).
            return false;
        }
    }
}
