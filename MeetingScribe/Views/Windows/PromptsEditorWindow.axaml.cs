using Avalonia.Controls;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System;
using System.Xml;
using MeetingScribe.ViewModels.Windows;

namespace MeetingScribe.Views.Windows;

public partial class PromptsEditorWindow : Window
{
    public PromptsEditorWindow()
    {
        InitializeComponent();
        LoadCustomHighlighting();
    }

    private void LoadCustomHighlighting()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://MeetingScribe/Assets/Manifests/Prompts.xshd"));
            using var reader = new XmlTextReader(stream);
            var customHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            Editor.SyntaxHighlighting = customHighlighting;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error loading custom highlighting: " + ex.Message);
        }
    }
}
