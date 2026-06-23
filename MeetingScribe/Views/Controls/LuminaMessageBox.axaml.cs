using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace MeetingScribe.Views;


public partial class LuminaMessageBox : Window
{
    public enum MessageBoxResult { Confirm, Cancel }

    public LuminaMessageBox()
    {
        InitializeComponent();
        // Bind the button click events to close the dialog with the appropriate result
        this.FindControl<Button>("ConfirmBtn").Click += (s, e) => Close(MessageBoxResult.Confirm);
        this.FindControl<Button>("CancelBtn").Click += (s, e) => Close(MessageBoxResult.Cancel);
    }

    /// <summary>
    /// Opens a message box dialog with the specified title and message.
    /// </summary>
    public static async Task<MessageBoxResult> Show(string title, string message, string confirmText = "Confirm", Window? owner = null)
    {
        if (owner == null)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                owner = desktop.MainWindow;
        }

        var dialog = new LuminaMessageBox();
        dialog.FindControl<TextBlock>("TitleText").Text = title;
        dialog.FindControl<TextBlock>("MessageText").Text = message;

        var confirmBtn = dialog.FindControl<Button>("ConfirmBtn");
        // Chache the original content of the confirm button
        if (confirmBtn.Content is StackPanel sp)
        {
            var txt = sp.Children[1] as TextBlock;
            if (txt != null) txt.Text = confirmText;
        }

        return await dialog.ShowDialog<MessageBoxResult>(owner!);
    }
}