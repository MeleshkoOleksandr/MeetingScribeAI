using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace MeetingScribe.UILogic;

public class LuminaAudioMeter : Control
{
    public static readonly StyledProperty<IEnumerable<double>> WaveformProperty =
        AvaloniaProperty.Register<LuminaAudioMeter, IEnumerable<double>>(nameof(Waveform));

    public IEnumerable<double> Waveform
    {
        get => GetValue(WaveformProperty);
        set => SetValue(WaveformProperty, value);
    }

    static LuminaAudioMeter()
    {
        //in case we need to completely replace the collection
        AffectsRender<LuminaAudioMeter>(WaveformProperty);
    }

    // Subscribe to changes within the collection
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WaveformProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldList)
                oldList.CollectionChanged -= OnCollectionChanged;

            if (change.NewValue is INotifyCollectionChanged newList)
                newList.CollectionChanged += OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // We force Avalonia redraw the control for each new sample
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Waveform == null || !Waveform.Any()) return;

        var brush = new SolidColorBrush(Color.Parse("#b7e97e")); // Lumina Green

        // We store the data in a local list to avoid “collection has been modified” errors
        var data = Waveform.ToList();
        int count = data.Count;

        // --- DYNAMIC DIMENSIONING ---
        double totalWidth = Bounds.Width;
        double totalHeight = Bounds.Height;
        double spacing = 2.0; // Fixed spacing

        // We calculate the width of a single column so that they fill ALL the available space
        double barWidth = (totalWidth - (spacing * (count - 1))) / count;

        // If the columns are too narrow (less than 1px), remove the spacing
        if (barWidth < 1)
        {
            barWidth = totalWidth / count;
            spacing = 0;
        }

        double centerY = totalHeight / 2;
        double x = 0;

        foreach (var val in data)
        {
            // The height of the bar is proportional to the volume (from 2px to full height)
            double barHeight = Math.Max(2, val * totalHeight);

            var rect = new Rect(x, centerY - barHeight / 2, barWidth, barHeight);

            // Draw a rounded rectangle
            context.FillRectangle(brush, rect, 2);

            x += barWidth + spacing;
        }
    }
}