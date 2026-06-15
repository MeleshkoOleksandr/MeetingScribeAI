using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.UILogic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace MeetingScribe.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // Navigation state
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private NavigationItem? _selectedMenuItem;
    [ObservableProperty] private bool _isSidebarExpanded = true;

    // Audio recording state
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _elapsedTime = "00:00:00";


    // -- ══════ Navigation  ══════ --//
    public List<NavigationItem> AllPages { get; } = new()
    {
        // Start Pages
        new NavigationItem { Label = "New Meeting", Icon = "PlusCircleOutline", Target = PageNames.New, Description = "Create new meeting", IsStartUp = true },
        new NavigationItem { Label = "Meeting Archive", Icon = "ArchiveOutline", Target = PageNames.Archive, Description = "View history" , IsStartUp = true},
        new NavigationItem { Label = "Settings", Icon = "CogOutline", Target = PageNames.Settings, Description = "Configuration", IsStartUp = true },
        new NavigationItem { Label = "Team", Icon = "AccountMultipleOutline", Target = PageNames.Team, Description = "Participant list", IsStartUp = true },
        // Other
        new NavigationItem { Label = "Meeting Recording", Icon = "Waveform", Target = PageNames.Recording, Description = "Meeting name", IsStartUp = false }
    };

    // Menu items for navigation
    public ObservableCollection<NavigationItem> MenuItems => new(AllPages.Where(p => p.IsStartUp));

    // Automatically switch page when SelectedMenuItem changes
    partial void OnSelectedMenuItemChanged(NavigationItem? value)
    {
        if (value == null) return;
        Navigate(value.Target);
    }

    // Logic for programmatic navigation
    private void Navigate(PageNames targetPage)
    {
        var navItem = AllPages.FirstOrDefault(p => p.Target == targetPage);
        if (navItem == null) return;

        // Update the ViewModel
        CurrentPage = targetPage switch
        {
            PageNames.New => new NewMeetingViewModel(),
            PageNames.Archive => new ArchiveViewModel(),
            PageNames.Settings => new SettingsViewModel(),
            PageNames.Team => new TeamViewModel(),
            PageNames.Recording => new ActiveMeetingViewModel(), // Our live page
            _ => CurrentPage
        };

        // Sync Sidebar selection (important for UI highlight)
        SelectedMenuItem = navItem;
    }


    // Triggered automatically by CommunityToolkit when IsRecording changes
    partial void OnIsRecordingChanged(bool value)
    {
        var recordingItem = AllPages.First(p => p.Target == PageNames.Recording);

        if (value) // Recording started
        {
            if (!MenuItems.Contains(recordingItem))
                MenuItems.Add(recordingItem);

            Navigate(PageNames.Recording);
        }
        else // Recording stopped
        {
            MenuItems.Remove(recordingItem);
            Navigate(PageNames.Archive); // Go to archive after stop
        }
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    // -- ══════ Recording  ══════ --//

    [RelayCommand]
    private void StartRecording()
    {
        IsRecording = true;
        // Переключаемся на страницу Live-транскрипции
        // Navigate("ActiveMeeting");
    }



    [RelayCommand]
    private void StopRecording()
    {
        IsRecording = false;
        // После остановки можем переключиться в архив или показать резюме
        //Navigate("Archive");
    }


    // -- ══════ Constructor  ══════ --//
    public MainWindowViewModel()
    {
        _currentPage = new NewMeetingViewModel();
        SelectedMenuItem = MenuItems[0]; // Select first item by default
    }
}