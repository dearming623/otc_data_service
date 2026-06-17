using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OtcDataService.ViewModels;

namespace OtcDataService.Views;

public partial class ExitPasswordDialog : Window
{
    private ExitPasswordDialogViewModel? _viewModel;

    public ExitPasswordDialog()
    {
        InitializeComponent();
        Icon = ExitWindowIconFactory.Create();
        KeyDown += OnKeyDown;
    }

    public static async Task<bool> ShowAsync()
    {
        var viewModel = new ExitPasswordDialogViewModel();
        var dialog = new ExitPasswordDialog
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };

        var tcs = new TaskCompletionSource<bool>();

        viewModel.CloseRequested += OnCloseRequested;
        dialog.Closed += OnClosed;
        dialog.DataContext = viewModel;
        dialog.Show();
        dialog.Activate();

        return await tcs.Task;

        void OnCloseRequested(bool result)
        {
            tcs.TrySetResult(result);
        }

        void OnClosed(object? sender, EventArgs e)
        {
            viewModel.CloseRequested -= OnCloseRequested;
            dialog.Closed -= OnClosed;
            if (!tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(false);
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as ExitPasswordDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(bool result)
    {
        Close(result);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(() => PasswordBox.Focus());
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null || !_viewModel.IsPasswordValid)
        {
            return;
        }

        _viewModel.ConfirmCommand.Execute(null);
        e.Handled = true;
    }
}
