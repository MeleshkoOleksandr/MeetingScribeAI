using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeetingScribe.Logic.AI;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using AvaloniaEdit.Document;
using MeetingScribe.Logic.Services;

namespace MeetingScribe.ViewModels.Windows;

public partial class PromptsEditorViewModel : ViewModelBase
{
    protected static string Loc(string key) => LocalizationManager.Instance[key];

    private readonly string _promptsPath;

    [ObservableProperty]
    private TextDocument _document = new();

    public PromptsEditorViewModel()
    {
        _promptsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Manifests", "prompts.md");
        LoadFileContent();
    }

    private void LoadFileContent()
    {
        try
        {
            if (File.Exists(_promptsPath))
            {
                Document.Text = File.ReadAllText(_promptsPath);
            }
        }
        catch (Exception ex)
        {
            // Fallback or error handling
            Document.Text = string.Format(Loc("view_PromptsEditor_ErrorLoading"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await File.WriteAllTextAsync(_promptsPath, Document.Text);
            
            // Also save to source directory if we are running in debug/development mode
            try 
            {
                var sourcePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Manifests", "prompts.md"));
                if (File.Exists(sourcePath))
                {
                    await File.WriteAllTextAsync(sourcePath, Document.Text);
                }
            }
            catch { /* Ignore if not in dev env */ }

            // Reload the prompts in the application memory
            PromtHelper.LoadPrompts();
            
            CloseWindow(true);
        }
        catch (Exception ex)
        {
            // Ideally show a message box here if saving fails
            LogService.Instance.LogError($"Error saving prompts: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseWindow(false);
    }

    private void CloseWindow(bool result)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var w in desktop.Windows)
            {
                if (w is MeetingScribe.Views.Windows.PromptsEditorWindow)
                {
                    w.Close(result);
                    break;
                }
            }
        }
    }
}
