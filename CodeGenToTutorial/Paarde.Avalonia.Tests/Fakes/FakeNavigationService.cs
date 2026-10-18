using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;

using Paarde.Avalonia.Contracts.Services;

namespace Paarde.Avalonia.Tests.Fakes;

public class FakeNavigationService : INavigationService
{
    // Required by INavigationService but never raised - nothing under test subscribes to it.
#pragma warning disable CS0067
    public event NavigatedEventHandler? Navigated;
#pragma warning restore CS0067

    public bool CanGoBack => false;

    public Frame? Frame
    {
        get; set;
    }

    public int NavigateToCallCount { get; private set; }

    public string? LastPageKey { get; private set; }

    public object? LastParameter { get; private set; }

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false)
    {
        NavigateToCallCount++;
        LastPageKey = pageKey;
        LastParameter = parameter;
        return true;
    }

    public bool GoBack() => false;
}
