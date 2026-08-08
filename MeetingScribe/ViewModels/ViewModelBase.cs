using CommunityToolkit.Mvvm.ComponentModel;
using MeetingScribe.Logic.Services;

namespace MeetingScribe.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        protected static string Loc(string key) => LocalizationManager.Instance[key];
    }
}
