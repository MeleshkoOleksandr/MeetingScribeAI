using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.Logic.Meeting;

namespace MeetingScribe.ViewModels;

public partial class NewMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private string _meetingName = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _selectedLanguage = "English";
    [ObservableProperty] private string _meetingTopics = "";

    // We will call this from the MainViewModel to get the final object
    public MeetingSession GetSessionData() => new()
    {
        Name = MeetingName,
        Description = Description,
        Language = SelectedLanguage, 
        MeetingTopics = MeetingTopics
    };
}