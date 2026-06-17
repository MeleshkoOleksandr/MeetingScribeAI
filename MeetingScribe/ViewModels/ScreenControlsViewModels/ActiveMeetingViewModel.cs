using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.Meeting;
using System.Collections.ObjectModel;

namespace MeetingScribe.ViewModels;

public partial class ActiveMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private string _meetingName;
    public ObservableCollection<TranscriptLine> TranscriptLines { get; } = new();

    public ActiveMeetingViewModel(string name)
    {
        MeetingName = name;
    }

    public void AddLine(string time, string text)
    {
        // Update UI on the main thread
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            TranscriptLines.Add(new TranscriptLine { Timestamp = $"[{time}]", Text = text });
        });
    }
}