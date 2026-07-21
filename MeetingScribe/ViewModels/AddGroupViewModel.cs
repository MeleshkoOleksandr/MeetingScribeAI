using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace MeetingScribe.ViewModels;

public partial class AddGroupViewModel : ViewModelBase
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _selectedIcon = "AccountGroup";
    [ObservableProperty] private Color _selectedColor = Color.Parse("#b7e97e");

    // Список популярных иконок для быстрого выбора
    public List<string> AvailableIcons { get; } = new()
    {
        "AccountGroup", "CodeBraces", "BullhornOutline", "PaletteOutline",
        "AccountTie", "Microscope", "HammerWrench", "ShieldCheckOutline",
        "Laptop", "School", "AccountBoxEditOutline", "AccountCash",
        "Bank", "Database", "Calculator", "Camera",
        "Briefcase", "HumanMaleBoard", "HumanMaleBoardPoll", "AccountTie"
    };
}