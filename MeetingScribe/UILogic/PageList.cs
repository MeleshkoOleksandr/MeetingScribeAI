using MeetingScribe.Enums;
using MeetingScribe.Logic.Services;
using MeetingScribe.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;

namespace MeetingScribe.UILogic;

public class PageList
{
    private static string Loc(string key) => LocalizationManager.Instance[key];

    // Static items (always there)
    public ObservableCollection<NavigationItem> MenuItems { get; } = new()
    {
        // Static Pages
        new NavigationItem { Label = Loc("view_PageList_NewMeeting"), Icon = "PlusCircleOutline", Target = PageNames.New, Description = Loc("view_PageList_NewMeetingDesc"),
            IsStartUp = true, Page = new NewMeetingViewModel() },
        new NavigationItem { Label = Loc("view_PageList_MeetingArchive"), Icon = "ArchiveOutline", Target = PageNames.Archive, Description = Loc("view_PageList_MeetingArchiveDesc") ,
            IsStartUp = true , Page = new ArchiveViewModel() },
        new NavigationItem { Label = Loc("view_PageList_Team"), Icon = "AccountMultipleOutline", Target = PageNames.Team, Description = Loc("view_PageList_TeamDesc"),
            IsStartUp = true , Page =  new TeamViewModel() },
        new NavigationItem { Label = Loc("view_PageList_Settings"), Icon = "CogOutline", Target = PageNames.Settings, Description = Loc("view_PageList_SettingsDesc"),
            IsStartUp = true , Page = new SettingsViewModel()},
        new NavigationItem { Label = Loc("view_PageList_ActivityLog"), Icon = "BellOutline", Target = PageNames.Logs, Description = Loc("view_PageList_ActivityLogDesc"),
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
