using CommunityToolkit.Mvvm.ComponentModel;

namespace MeetingScribe.UILogic;

public partial class AppSettings : ObservableObject
{
    // Speech recognition settings
    [ObservableProperty] private float _speechThreshold = 0.2f;
    [ObservableProperty] private int _silenceTimeoutMs = 600;
    [ObservableProperty] private float _audioGain = 3.0f;
    [ObservableProperty] private string _selectedModel = "ggml-large-v3-turbo.bin";
    [ObservableProperty] private string _transcriptionLanguage = "it";

}