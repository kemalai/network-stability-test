using CommunityToolkit.Mvvm.ComponentModel;
using InternetMonitor.Infrastructure.Localization;

namespace InternetMonitor.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    // Localization shortcut
    public LocalizationService L => LocalizationService.Instance;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
}
