using Paarde.Core.Models;

namespace Paarde.Avalonia.ViewModels;

// T8.1.2: pairs a FileTreeEntry (Core, pure fs structure) with IsTouched - whether this file is
// part of the current proposal in IPromptResultStore. Kept Avalonia-side rather than on
// FileTreeEntry itself since "touched by the current proposal" is UI/session state, not
// something inherent to a file on disk.
public sealed record FileTreeNodeViewModel(string Name, string FullPath, bool IsDirectory, bool IsTouched, IReadOnlyList<FileTreeNodeViewModel> Children)
{
    public static FileTreeNodeViewModel FromEntry(FileTreeEntry entry, IReadOnlySet<string> touchedFullPaths)
    {
        var children = entry.Children.Select(child => FromEntry(child, touchedFullPaths)).ToArray();

        return new FileTreeNodeViewModel(entry.Name, entry.FullPath, entry.IsDirectory, touchedFullPaths.Contains(entry.FullPath), children);
    }
}
