using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.UILogic;
using System.Collections.ObjectModel;


namespace MeetingScribe.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private NavigationItem? _selectedMenuItem;
    [ObservableProperty] private bool _isSidebarExpanded = true;

    public ObservableCollection<NavigationItem> MenuItems { get; } = new()
    {
        new NavigationItem { Label = "New Meeting", Icon = "PlusCircleOutline", Target = "New", Description = "Create new meeting" },
        new NavigationItem { Label = "Meeting Archive", Icon = "ArchiveOutline", Target = "Archive", Description = "View history" },
        new NavigationItem { Label = "Settings", Icon = "CogOutline", Target = "Settings", Description = "Configuration" },
        new NavigationItem { Label = "Team", Icon = "AccountMultipleOutline", Target = "Team", Description = "Participant list" }
    };

    public MainWindowViewModel()
    {
        _currentPage = new NewMeetingViewModel();
        SelectedMenuItem = MenuItems[0]; // Select first item by default
    }

    // Automatically switch page when SelectedMenuItem changes
    partial void OnSelectedMenuItemChanged(NavigationItem? value)
    {
        if (value == null) return;

        CurrentPage = value.Target switch
        {
            "New" => new NewMeetingViewModel(),
            "Archive" => new ArchiveViewModel(),
            "Settings" => new SettingsViewModel(),
            "Team" => new TeamViewModel(),
            _ => CurrentPage
        };
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;
}