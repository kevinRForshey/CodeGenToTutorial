using System.Diagnostics.CodeAnalysis;

using Paarde.Avalonia.Contracts.Services;
using Paarde.Avalonia.Helpers;
using Paarde.Avalonia.ViewModels;

using FluentAvalonia.UI.Controls;

namespace Paarde.Avalonia.Services;

public class NavigationViewService : INavigationViewService
{
    private readonly INavigationService _navigationService;

    private readonly IPageService _pageService;

    private NavigationView? _navigationView;

    public IList<object>? MenuItems => _navigationView?.MenuItems;

    public object? SettingsItem => _navigationView?.SettingsItem;

    public NavigationViewService(INavigationService navigationService, IPageService pageService)
    {
        _navigationService = navigationService;
        _pageService = pageService;
    }

    [MemberNotNull(nameof(_navigationView))]
    public void Initialize(NavigationView navigationView)
    {
        _navigationView = navigationView;
        _navigationView.BackRequested += OnBackRequested;
        _navigationView.ItemInvoked += OnItemInvoked;
    }

    public void UnregisterEvents()
    {
        if (_navigationView != null)
        {
            _navigationView.BackRequested -= OnBackRequested;
            _navigationView.ItemInvoked -= OnItemInvoked;
        }
    }

    public NavigationViewItem? GetSelectedItem(Type pageType)
    {
        if (_navigationView != null)
        {
            return GetSelectedItem(_navigationView.MenuItems, pageType) ?? GetSelectedItem(_navigationView.FooterMenuItems, pageType);
        }

        return null;
    }

    private void OnBackRequested(object? sender, NavigationViewBackRequestedEventArgs args) => _navigationService.GoBack();

    private void OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs args)
    {
        // DIAGNOSTIC (temporary, to be removed)
        System.IO.File.AppendAllText("/tmp/nav-diag.log", $"[{DateTime.Now:T}] OnItemInvoked fired. IsSettingsInvoked={args.IsSettingsInvoked}, InvokedItemContainer={args.InvokedItemContainer?.GetType().Name}, InvokedItem={args.InvokedItem}\n");

        if (args.IsSettingsInvoked)
        {
            _navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
        }
        else
        {
            var selectedItem = args.InvokedItemContainer as NavigationViewItem;
            var pageKey = selectedItem != null ? NavigationHelper.GetNavigateTo(selectedItem) : null;

            // DIAGNOSTIC (temporary, to be removed)
            System.IO.File.AppendAllText("/tmp/nav-diag.log", $"[{DateTime.Now:T}] selectedItem={selectedItem?.Content}, pageKey={pageKey ?? "(null)"}\n");

            if (selectedItem != null && pageKey is not null)
            {
                var navigated = _navigationService.NavigateTo(pageKey);
                System.IO.File.AppendAllText("/tmp/nav-diag.log", $"[{DateTime.Now:T}] NavigateTo({pageKey}) returned {navigated}\n");
            }
        }
    }

    private NavigationViewItem? GetSelectedItem(IEnumerable<object> menuItems, Type pageType)
    {
        foreach (var item in menuItems.OfType<NavigationViewItem>())
        {
            if (IsMenuItemForPageType(item, pageType))
            {
                return item;
            }

            var selectedChild = GetSelectedItem(item.MenuItems, pageType);
            if (selectedChild != null)
            {
                return selectedChild;
            }
        }

        return null;
    }

    private bool IsMenuItemForPageType(NavigationViewItem menuItem, Type sourcePageType)
    {
        if (NavigationHelper.GetNavigateTo(menuItem) is string pageKey)
        {
            return _pageService.GetPageType(pageKey) == sourcePageType;
        }

        return false;
    }
}
