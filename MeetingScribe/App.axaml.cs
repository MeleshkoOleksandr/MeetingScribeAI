using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MeetingScribe.Logic.Services;
using MeetingScribe.UILogic.Enums;
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