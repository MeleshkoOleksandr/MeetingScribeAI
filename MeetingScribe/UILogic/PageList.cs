using MeetingScribe.UILogic.Enums;
using MeetingScribe.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace MeetingScribe.UILogic;

public class PageList
{
    // Static items (always there)
    public ObservableCollection<NavigationItem> MenuItems { get; } = new()
    {
        // Static Pages
        new NavigationItem { Label = "New Meeting", Icon = "PlusCircleOutline", Target = PageNames.New, Description = "Create new meeting",
            IsStartUp = true, Page = new NewMeetingViewModel() },
        new NavigationItem { Label = "Meeting Archive", Icon = "ArchiveOutline", Target = PageNames.Archive, Description = "View history" ,
            IsStartUp = true , Page = new ArchiveViewModel() },
        new NavigationItem { Label = "Team", Icon = "AccountMultipleOutline", Target = PageNames.Team, Description = "Participant list",
            IsStartUp = true , Page =  new TeamViewModel() },
        new NavigationItem { Label = "Settings", Icon = "CogOutline", Target = PageNames.Settings, Description = "Configuration",
            IsStartUp = true , Page = new SettingsViewModel()},
        new NavigationItem { Label = "Activity Log", Icon = "BellOutline", Target = PageNames.Logs, Description = "User activity",
            IsStartUp = true , Page = new LogsViewModel()},
    };

    // Temporary items (appear/disappear)
    public ObservableCollection<NavigationItem> TemporaryItems { get; } = new();

    // Add new page to temporary list
    public void AddTemporaryItem(NavigationItem item)
    {
        if (!TemporaryItems.Contains(item))
            TemporaryItems.Add(item);
    }
    // Remove page from temporary list
    public void RemoveTemporaryItem(NavigationItem item)
    {
        if (TemporaryItems.Contains(item))
            TemporaryItems.Remove(item);
    }

    // Get page by name (search both static and temporary)
    public NavigationItem? GetByTarget(PageNames target)
    {
        return MenuItems.FirstOrDefault(m => m.Target == target)
            ?? TemporaryItems.FirstOrDefault(m => m.Target == target);
    }

    public ViewModelBase startPage  => MenuItems[0].Page;
}
