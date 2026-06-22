using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MeetingScribe.Logic.Meeting;
using MeetingScribe.UILogic.ManifestReaders;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;


namespace MeetingScribe.ViewModels;

public partial class NewMeetingViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<LanguageManifest> _languages = new();
    [ObservableProperty] private LanguageManifest? _selectedLanguage;

    [ObservableProperty] private string _meetingName = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _meetingTopics = "";
    [ObservableProperty] private string _participantsText = "";

    public NewMeetingViewModel()
    {
        // Default name with date
        MeetingName = DateTime.Now.ToString("yy-MM-dd HH_mm") + " - New Meeting";
        LoadLanguages();
    }

    private void LoadLanguages()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "languages_manifest.json");
        if (File.Exists(path))
        {
            var data = JsonSerializer.Deserialize<List<LanguageManifest>>(File.ReadAllText(path));
            Languages = new ObservableCollection<LanguageManifest>(data ?? new());
            if (Languages.Any()) SelectedLanguage = Languages.First(); // Select the first language by default
        }
    }

    public MeetingSession GetSessionData() => new()
    {
        Name = MeetingName,
        Description = Description,
        MeetingTopics = MeetingTopics,
        Language = SelectedLanguage?.Code ?? "auto" // Save the CODE (ru, en)
    };


    [RelayCommand]
    private async Task ImportAgenda()
    {
        // 1. Получаем доступ к файловой системе через окно
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        var storage = desktop.MainWindow?.StorageProvider;
        if (storage == null) return;

        // 2. Выбор только .docx файлов
        var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Meeting Agenda",
            FileTypeFilter = new[] { new FilePickerFileType("Word Documents") { Patterns = new[] { "*.docx" } } },
            AllowMultiple = false
        });

        if (result.Count == 0) return;

        try
        {
            // 3. Вызов вашего парсера
            string filePath = result[0].Path.LocalPath;
            var (participants, topics) = AgendaParser.ParseMeetingAgenda(filePath);

            // 4. Заполняем поля на UI
            MeetingTopics = topics;
            ParticipantsText = participants;

            // Опционально: Добавить в описание, что импорт прошел успешно
            if (string.IsNullOrEmpty(Description))
                Description = $"Imported from agenda: {DateTime.Now:d}";
        }
        catch (Exception ex)
        {
            // Тут можно вывести ошибку пользователю
            System.Diagnostics.Debug.WriteLine($"Parsing error: {ex.Message}");
        }
    }
}