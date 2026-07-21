using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MeetingScribe.Logic.Meeting;
using MeetingScribe.ViewModels;

namespace MeetingScribe.Views;

public partial class AddGroupWindow : Window
{
    public AddGroupWindow()
    {
        InitializeComponent();

        this.FindControl<Button>("CancelBtn").Click += (s, e) => Close(null);


        this.FindControl<Button>("CreateBtn").Click += (s, e) =>
        {
            var vm = (AddGroupViewModel)DataContext!;
            var newGroup = new TeamGroup
            {
                Name = vm.Name,
                Icon = vm.SelectedIcon,
                Color = vm.SelectedColor.ToString() // Save as HEX 
            };

            Close(newGroup);
        };
    }
}