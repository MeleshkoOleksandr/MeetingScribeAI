using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MeetingScribe.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // The current view being displayed
    [ObservableProperty] private ViewModelBase _currentPage;

    // Header title
    [ObservableProperty] private string _currentPageTitle = "New Meeting";

    // Sidebar state (expanded or collapsed)
    [ObservableProperty] private bool _isSidebarExpanded = true;

    // ViewModels (cached instances to keep state)
    private readonly NewMeetingViewModel _newMeetingVm = new();
    private readonly ArchiveViewModel _archiveVm = new();
    private readonly TeamViewModel _teamVm = new();
    private readonly SettingsViewModel _settingsVm = new();

    public MainWindowViewModel()
    {
        // Initial page
        _currentPage = _newMeetingVm;
    }

    /// <summary>
    /// Logic for switching pages
    /// </summary>
    [RelayCommand]
    private void Navigate(string target)
    {
        switch (target)
        {
            case "New":
                CurrentPage = _newMeetingVm;
                CurrentPageTitle = "New Meeting";
                break;
            case "Archive":
                CurrentPage = _archiveVm;
                CurrentPageTitle = "Meeting Archive";
                break;
            case "Team":
                CurrentPage = _teamVm;
                CurrentPageTitle = "Team";
                break;
            case "Settings":
                CurrentPage = _settingsVm;
                CurrentPageTitle = "Settings";
                break;
        }
    }

    /// <summary>
    /// Toggles sidebar width
    /// </summary>
    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }
}