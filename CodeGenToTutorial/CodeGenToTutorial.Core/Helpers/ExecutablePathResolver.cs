using System.Runtime.InteropServices;

namespace CodeGenToTutorial.Core.Helpers;

// Checks whether an executable would resolve on PATH, the same way the OS shell would find it,
// without actually starting a process (T6.1.3) - used as a cheap preflight before shelling out to
// `claude`, so a missing CLI can be reported with an actionable message instead of surfacing
// whatever exception Process.Start happens to throw.
public static class ExecutablePathResolver
{
    // pathVariable defaults to the real PATH environment variable; the parameter exists so tests
    // can point this at a temp directory instead of mutating process-wide state.
    public static bool ExistsOnPath(string executableName, string? pathVariable = null)
    {
        pathVariable ??= Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(pathVariable))
        {
            return false;
        }

        // Windows resolves a bare command name against these extensions in order (cmd.exe's
        // PATHEXT behavior); npm installs CLI shims as .cmd, so that has to be checked explicitly
        // rather than relying only on an exact, extension-less match like other platforms use.
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var directory in pathVariable.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                if (File.Exists(Path.Combine(directory, executableName + extension)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
