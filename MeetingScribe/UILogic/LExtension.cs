using Avalonia.Data;
using Avalonia.Markup.Xaml;
using MeetingScribe.Logic.Services;
using System;

namespace MeetingScribe.UILogic;

public class LExtension : MarkupExtension
{
    public string Key { get; set; }

    public LExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Create a binding to the manager instance and its indexer
        var binding = new Binding
        {
            Source = LocalizationManager.Instance,
            Path = $"[{Key}]", // We link to the indexer
            Mode = BindingMode.OneWay
        };

        return binding;
    }
}