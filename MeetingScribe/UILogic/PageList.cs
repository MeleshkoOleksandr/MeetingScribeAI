using System.ComponentModel;
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
    public ObservableCollection<NavigationItem> MenuItems { get; } = new();

    // Temporary items (appear/disappear)
    public ObservableCollection<NavigationItem> TemporaryItems { get; } = new();

    public PageList()
    {
        InitializeStaticPages();
        LocalizationManager.Instance.PropertyChanged += OnLanguageChanged;
    }

    private void InitializeStaticPages()
    {
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_NewMeeting"), Icon = "PlusCircleOutline", Target = PageNames.New, Description = Loc("view_PageList_NewMeetingDesc"), IsStartUp = true, Page = new NewMeetingViewModel() });
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_MeetingArchive"), Icon = "ArchiveOutline", Target = PageNames.Archive, Description = Loc("view_PageList_MeetingArchiveDesc") , IsStartUp = true , Page = new ArchiveViewModel() });
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_Team"), Icon = "AccountMultipleOutline", Target = PageNames.Team, Description = Loc("view_PageList_TeamDesc"), IsStartUp = true , Page =  new TeamViewModel() });
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_Settings"), Icon = "CogOutline", Target = PageNames.Settings, Description = Loc("view_PageList_SettingsDesc"), IsStartUp = true , Page = new SettingsViewModel()});
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_ActivityLog"), Icon = "BellOutline", Target = PageNames.Logs, Description = Loc("view_PageList_ActivityLogDesc"), IsStartUp = true , Page = new LogsViewModel()});
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "CurrentLanguage" || e.PropertyName == "Item")
        {
            RebuildStaticPages();
            RebuildTemporaryPages();
        }
    }

    private void RebuildStaticPages()
    {
        var tempPages = MenuItems.Select(m => m.Page).ToList();
        if (tempPages.Count < 5) return;

        MenuItems.Clear();
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_NewMeeting"), Icon = "PlusCircleOutline", Target = PageNames.New, Description = Loc("view_PageList_NewMeetingDesc"), IsStartUp = true, Page = tempPages[0] });
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_MeetingArchive"), Icon = "ArchiveOutline", Target = PageNames.Archive, Description = Loc("view_PageList_MeetingArchiveDesc") , IsStartUp = true , Page = tempPages[1] });
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_Team"), Icon = "AccountMultipleOutline", Target = PageNames.Team, Description = Loc("view_PageList_TeamDesc"), IsStartUp = true , Page =  tempPages[2] });
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_Settings"), Icon = "CogOutline", Target = PageNames.Settings, Description = Loc("view_PageList_SettingsDesc"), IsStartUp = true , Page = tempPages[3]});
        MenuItems.Add(new NavigationItem { Label = Loc("view_PageList_ActivityLog"), Icon = "BellOutline", Target = PageNames.Logs, Description = Loc("view_PageList_ActivityLogDesc"), IsStartUp = true , Page = tempPages[4]});
    }

    private void RebuildTemporaryPages()
    {
        var tempItemsCopy = TemporaryItems.ToList();
        TemporaryItems.Clear();
        foreach (var item in tempItemsCopy)
        {
            if (item.Target == PageNames.Recording)
            {
                item.Label = Loc("view_Main_MeetingRecording");
            }
            else if (item.Target == PageNames.Review)
            {
                item.Label = Loc("view_Main_Review");
            }
            TemporaryItems.Add(item);
        }
    }



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
