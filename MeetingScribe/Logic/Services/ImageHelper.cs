using SkiaSharp;
using System;
using System.IO;

namespace MeetingScribe.Logic.Services;

public static class ImageHelper
{
    public static void ResizeAndSavePhoto(string sourcePath, string destPath, int targetSize = 512)
    {
        using var input = File.OpenRead(sourcePath);
        using var original = SKBitmap.Decode(input);

        if (original == null) throw new Exception("Could not decode image file.");

        // Calculating the square for cropping (Crop to Square)
        int width = original.Width;
        int height = original.Height;
        int minSide = Math.Min(width, height);

        var cropRect = new SKRectI(
            (width - minSide) / 2,
            (height - minSide) / 2,
            (width + minSide) / 2,
            (height + minSide) / 2
        );

        using var cropped = new SKBitmap(minSide, minSide);
        original.ExtractSubset(cropped, cropRect);

        // Changing the size (Resize)
        using var resized = new SKBitmap(targetSize, targetSize);
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.Transparent);
            var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
            var destRect = new SKRect(0, 0, targetSize, targetSize);
            canvas.DrawBitmap(cropped, destRect, sampling);
        }

        // Save as JPG
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        using var output = File.OpenWrite(destPath);
        data.SaveTo(output);
    }
}