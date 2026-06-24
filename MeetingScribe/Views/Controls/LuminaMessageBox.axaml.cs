using Material.Icons.Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Material.Icons;
using System.Threading.Tasks;
using MeetingScribe.UILogic.Enums;

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
    public static async Task<MessageBoxResult> Show(
         string title,
         string message,
         LuminaMessageBoxType type = LuminaMessageBoxType.Message,
         string confirmBtnText = "")
    {
        var dialog = new LuminaMessageBox();

        // Поиск элементов
        var titleTxt = dialog.FindControl<TextBlock>("TitleText");
        var msgTxt = dialog.FindControl<TextBlock>("MessageText");
        var iconBorder = dialog.FindControl<Border>("IconBorder");
        var mainIcon = dialog.FindControl<MaterialIcon>("MainIcon");
        var confirmIcon = dialog.FindControl<MaterialIcon>("ConfirmIcon");
        var confirmTxt = dialog.FindControl<TextBlock>("ConfirmText");
        var confirmBtn = dialog.FindControl<Button>("ConfirmBtn");
        var cancelBtn = dialog.FindControl<Button>("CancelBtn");
        var bottomBar = dialog.FindControl<ProgressBar>("BottomBar");

        titleTxt.Text = title;
        msgTxt.Text = message;

        // Setting styles based on the message box type
        switch (type)
        {
            case LuminaMessageBoxType.Danger:
                iconBorder.Background = Brush.Parse("#332020");
                mainIcon.Foreground = Brush.Parse("#ffb4ab");
                mainIcon.Kind = MaterialIconKind.DeleteForeverOutline;
                confirmBtn.Classes.Add("danger");
                confirmIcon.Kind = MaterialIconKind.DeleteOutline;
                confirmTxt.Text = string.IsNullOrEmpty(confirmBtnText) ? "Delete" : confirmBtnText;
                bottomBar.Foreground = Brush.Parse("#ffb4ab");
                break;

            case LuminaMessageBoxType.Confirm:
                iconBorder.Background = Brush.Parse("#1a2115");
                mainIcon.Foreground = Brush.Parse("#b7e97e");
                mainIcon.Kind = MaterialIconKind.HelpCircleOutline;
                confirmBtn.Classes.Add("primary");
                confirmIcon.Kind = MaterialIconKind.Check;
                confirmTxt.Text = string.IsNullOrEmpty(confirmBtnText) ? "Confirm" : confirmBtnText;
                bottomBar.Foreground = Brush.Parse("#b7e97e");
                break;

            case LuminaMessageBoxType.Message:
                iconBorder.Background = Brush.Parse("#1a2115");
                mainIcon.Foreground = Brush.Parse("#b7e97e");
                mainIcon.Kind = MaterialIconKind.InformationOutline;
                confirmBtn.Classes.Add("primary");
                confirmIcon.Kind = MaterialIconKind.Check;
                confirmTxt.Text = string.IsNullOrEmpty(confirmBtnText) ? "Got it" : confirmBtnText;
                cancelBtn.IsVisible = false; // Hide the cancel button for message type
                bottomBar.Foreground = Brush.Parse("#b7e97e");
                break;
        }

        // Owner search
        Window? owner = null;
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            owner = desktop.MainWindow;

        return await dialog.ShowDialog<MessageBoxResult>(owner!);
    }
}