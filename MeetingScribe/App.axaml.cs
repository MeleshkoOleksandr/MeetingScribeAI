using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MeetingScribe.Enums;
using MeetingScribe.Logic.Services;
using MeetingScribe.ViewModels;
using MeetingScribe.Views;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace MeetingScribe
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            // Register handler before the window is created
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (s, e) =>
            {
                LogService.Instance.Log("Unhandled UI Exception", LogLevel.Critical, e.Exception.ToString());
                e.Handled = true; 
            };

            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            // Handling Raw Errors in Threads
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogService.Instance.Log("Unhandled System Exception", LogLevel.Critical, ex?.ToString());
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogService.Instance.Log("Unhandled Task Exception", LogLevel.Critical, e.Exception.ToString());
                e.SetObserved();
            };

            base.OnFrameworkInitializationCompleted();
        }
    }
}