using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.UILogic;
using System.Collections.ObjectModel;

namespace MeetingScribe.ViewModels;

public partial class ActiveMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private string _meetingName = "Weekly Sync";
    [ObservableProperty] private string _elapsedTime = "00:15:24";

    public ObservableCollection<TranscriptLine> TranscriptLines { get; } = new();

    public ActiveMeetingViewModel()
    {
        // Заглушки для теста
        TranscriptLines.Add(new TranscriptLine { Timestamp = "[00:14:02]", Text = "And if we look at the Q3 roadmap..." });
        TranscriptLines.Add(new TranscriptLine { Timestamp = "[00:15:24]", Text = "The AI is currently analyzing...", IsAiAnalyzing = true });
    }

}