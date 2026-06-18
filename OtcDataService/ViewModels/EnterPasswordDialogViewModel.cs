using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OtcDataService.ViewModels;

public partial class EnterPasswordDialogViewModel : ViewModelBase
{
    public event Action<bool>? CloseRequested;

    [ObservableProperty]
    private string _title = "Exit Confirmation";

    [ObservableProperty]
    private string _prompt = "Enter the exit password.";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordValid;

    public void ApplyContext(string title, string prompt)
    {
        Title = title;
        Prompt = prompt;
        Password = string.Empty;
    }

    partial void OnPasswordChanged(string value)
    {
        IsPasswordValid = value == DateTime.Now.ToString("yyyyMMddHH");
    }

    [RelayCommand]
    private void ClearPassword()
    {
        Password = string.Empty;
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    [RelayCommand]
    private void Confirm()
    {
        if (!IsPasswordValid)
        {
            return;
        }

        CloseRequested?.Invoke(true);
    }
}
