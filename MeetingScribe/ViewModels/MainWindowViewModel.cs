using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Services;

namespace MeetingScribe.ViewModels
{
    // MUST BE PARTIAL for Source Generators to work
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty] private string _meetingName = "Weekly Sync";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private string _transcriptText = "";
        [ObservableProperty] private string _statusText = "Idle";
        [ObservableProperty] private bool _isRecording;

        private readonly TranscriptionService _transcriptionService;

        public MainWindowViewModel()
        {
            _transcriptionService = new TranscriptionService();

            // Use the generated property 'StatusText'
            _transcriptionService.StatusChanged += (status) => StatusText = status;

            _transcriptionService.TranscriptionUpdated += (text) =>
            {
                TranscriptText += $" {text}";
            };
        }

        [RelayCommand]
        private async Task ToggleRecording()
        {
            if (!IsRecording)
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    // Ensure your models are in Assets folder and set to "Copy to Output Directory"
                    string whisperPath = Path.Combine(baseDir, "Assets", "ggml-large-v3-turbo-q8_0.bin");
                    string vadPath = Path.Combine(baseDir, "Assets", "silero_vad.onnx");

                    await _transcriptionService.InitializeAsync(whisperPath, vadPath);
                    _transcriptionService.Start();
                    IsRecording = true; // Use Capitalized property
                }
                catch (Exception ex)
                {
                    StatusText = $"Error: {ex.Message}";
                }
            }
            else
            {
                _transcriptionService.Stop();
                IsRecording = false;
            }
        }
    }
}