using Avalonia.Controls;
using MeetingScribe.ViewModels;
using System.Collections.Specialized;

namespace MeetingScribe.Views;

public partial class ActiveMeetingView : UserControl
{
    public ActiveMeetingView()
    {
        InitializeComponent();

        // Subscribe to DataContext changes (when the page is created)
        DataContextChanged += (s, e) =>
        {
            if (DataContext is ActiveMeetingViewModel vm)
            {
                // Listen for changes to the list of transcriptions
                vm.TranscriptLines.CollectionChanged += OnTranscriptChanged;
            }
        };
    }

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // If a new row was added (Action.Add)
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // Ask the ScrollViewer to scroll to the bottom
            TranscriptScrollViewer.ScrollToEnd();
        }
    }
}