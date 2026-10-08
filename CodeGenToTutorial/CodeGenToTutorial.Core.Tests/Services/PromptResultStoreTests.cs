using System.Collections.Specialized;
using System.ComponentModel;

using CodeGenToTutorial.Core.Services;
using CodeGenToTutorial.Core.ViewModels;

namespace CodeGenToTutorial.Core.Tests.Services;

public class PromptResultStoreTests
{
    [Fact]
    public void SetResult_StoresTutorialFilesAndWorkingDirectory()
    {
        var store = new PromptResultStore();
        var files = new[] { new ProposedFileChangeViewModel("a.cs", "content") };

        store.SetResult("tutorial text", files, "/working/dir");

        Assert.Equal("tutorial text", store.Tutorial);
        Assert.Equal("/working/dir", store.WorkingDirectoryPath);
        Assert.Single(store.Files);
        Assert.Same(files[0], store.Files[0]);
    }

    [Fact]
    public void SetResult_RaisesPropertyChangedForTutorialAndWorkingDirectoryPath()
    {
        var store = new PromptResultStore();
        var raisedProperties = new List<string?>();
        ((INotifyPropertyChanged)store).PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        store.SetResult("tutorial text", Array.Empty<ProposedFileChangeViewModel>(), "/working/dir");

        Assert.Contains(nameof(PromptResultStore.Tutorial), raisedProperties);
        Assert.Contains(nameof(PromptResultStore.WorkingDirectoryPath), raisedProperties);
    }

    [Fact]
    public void SetResult_RaisesCollectionChangedOnFiles()
    {
        var store = new PromptResultStore();
        var raiseCount = 0;
        ((INotifyCollectionChanged)store.Files).CollectionChanged += (_, _) => raiseCount++;

        store.SetResult("tutorial text", new[] { new ProposedFileChangeViewModel("a.cs", "content") }, "/working/dir");

        Assert.True(raiseCount > 0);
    }

    [Fact]
    public void SetResult_CalledTwice_MutatesTheSameFilesInstanceInPlace()
    {
        // Files is a fixed ObservableCollection instance (not reassigned) so anything that subscribed to
        // store.Files.CollectionChanged - rather than re-reading the Files reference - keeps seeing updates
        // across repeated prompt runs.
        var store = new PromptResultStore();
        var filesReference = store.Files;

        store.SetResult("first", new[] { new ProposedFileChangeViewModel("a.cs", "content") }, "/dir");
        store.SetResult("second", new[] { new ProposedFileChangeViewModel("b.cs", "content") }, "/dir");

        Assert.Same(filesReference, store.Files);
        Assert.Single(store.Files);
        Assert.Equal("b.cs", store.Files[0].FilePath);
    }
}
