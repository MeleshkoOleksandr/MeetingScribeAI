using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MeetingScribe.Views;

public partial class ReviewMeetingView : UserControl
{
    public ReviewMeetingView()
    {
        InitializeComponent();
    }

    private void TranscriptListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModels.ReviewMeetingViewModel vm && vm.AutoScrollTranscript)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                listBox.ScrollIntoView(listBox.SelectedItem);
            }
        }
    }
}