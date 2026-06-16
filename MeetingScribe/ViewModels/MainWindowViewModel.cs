using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.UILogic;
using MeetingScribe.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace MeetingScribe.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    //  -- ══════ Fields & Properties  ══════ --//

    // Navigation state
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isSidebarExpanded = true;

    // We use a custom property for SelectedMenuItem to prevent unwanted null assignment from UI.
    private NavigationItem? _selectedMenuItem;
    public NavigationItem? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set
        {
            // Ignore null if we already have a selection to prevent multi-list conflicts
            if (value == null && _selectedMenuItem != null) return;

            if (SetProperty(ref _selectedMenuItem, value))
            {
                // Call the regular method instead of a partial one
                HandleNavigation(value);
            }
        }
    }

    // Audio recording state
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _elapsedTime = "00:00:00";

    // List of pages used in app (static and temporary) and there methods 
    public PageList PageList { get; } = new();


    // -- ══════ Navigation  ══════ --//

    private void HandleNavigation(NavigationItem? value)
    {
        if (value == null) return;
        CurrentPage = value.Page;
    }

    // Logic for programmatic navigation
    private void Navigate(NavigationItem page)
    {
        SelectedMenuItem = page;
    }

    public void Navigate(PageNames target)
    {
        var item = PageList.GetByTarget(target);
        if (item != null) SelectedMenuItem = item;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    // -- ══════ Recording  ══════ --//

    // Logic for Recording Trigger
    partial void OnIsRecordingChanged(bool value)
    {
        //var recordingItem = AllPages.First(p => p.Target == PageNames.Recording);

        if (value)
        {
            //if (!TemporaryItems.Contains(recordingItem))
            //    TemporaryItems.Add(recordingItem);

            // Navigate(PageNames.Recording);

            StartRecording();
        }
        else
        {
            // TemporaryItems.Remove(recordingItem);
            //Navigate(PageNames.New); // Go back after stop

            StopRecording();
        }
    }

    [RelayCommand]
    private void StartRecording()
    {
        // Проверяем, нет ли уже такой страницы в списке
        var recordingPage = PageList.GetByTarget(PageNames.Recording);

        if (recordingPage == null)
        {
            recordingPage = new NavigationItem
            {
                Label = "Live Recording",
                Icon = "Waveform",
                Target = PageNames.Recording,
                Page = new ActiveMeetingViewModel() // Создаем свежую страницу
            };
            PageList.AddTemporaryItem(recordingPage);
        }

        IsRecording = true;
        Navigate(recordingPage);
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
        _currentPage = PageList.startPage;

        NavigationItem newRecording = new NavigationItem
        {
            Label = "Meeting Recording",
            Icon = "Waveform",
            Target = PageNames.Recording,
            Description = "Meeting name",
            IsStartUp = false,
            Page = new ActiveMeetingViewModel()
        };

        PageList.AddTemporaryItem(newRecording);
        Navigate(newRecording);

    }
}