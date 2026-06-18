using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using OtcDataService.ViewModels;

namespace OtcDataService.Views;

public partial class ExitPasswordDialog : Window
{
    private const string DefaultTitle = "Exit Confirmation";
    private const string DefaultPrompt = "Enter the exit password.";

    private static ExitPasswordDialog? _activeDialog;
    private static TaskCompletionSource<bool>? _activeTcs;

    private ExitPasswordDialogViewModel? _viewModel;

    public ExitPasswordDialog()
    {
        InitializeComponent();
        Icon = WindowIconFactory.Create();
        PasswordBox.KeyDown += OnPasswordBoxKeyDown;
    }

    public static async Task<bool> ShowAsync(string? title = null, string? prompt = null)
    {
        var resolvedTitle = title ?? DefaultTitle;
        var resolvedPrompt = prompt ?? DefaultPrompt;

        if (_activeDialog is { IsVisible: true } && _activeTcs is not null)
        {
            if (_activeDialog.DataContext is ExitPasswordDialogViewModel activeViewModel
                && activeViewModel.Title == resolvedTitle
                && activeViewModel.Prompt == resolvedPrompt)
            {
                _activeDialog.BringActiveDialogToFront();
                return await _activeTcs.Task;
            }

            _activeTcs.TrySetResult(false);
            if (_activeDialog.DataContext is ExitPasswordDialogViewModel viewModel)
            {
                viewModel.ApplyContext(resolvedTitle, resolvedPrompt);
            }

            _activeTcs = new TaskCompletionSource<bool>();
            _activeDialog.BringActiveDialogToFront();
            return await _activeTcs.Task;
        }

        var newViewModel = new ExitPasswordDialogViewModel();
        newViewModel.ApplyContext(resolvedTitle, resolvedPrompt);

        var owner = GetOwnerWindow();
        var dialog = new ExitPasswordDialog
        {
            WindowStartupLocation = owner is not null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            Topmost = true,
            Owner = owner
        };

        _activeDialog = dialog;
        _activeTcs = new TaskCompletionSource<bool>();

        newViewModel.CloseRequested += OnActiveCloseRequested;
        dialog.Closed += OnDialogClosed;
        dialog.DataContext = newViewModel;
        StartDialog(dialog, owner);

        return await _activeTcs.Task;
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    private static void StartDialog(ExitPasswordDialog dialog, Window? owner)
    {
        _ = ShowDialogAsync(dialog, owner);
    }

    private static async Task ShowDialogAsync(ExitPasswordDialog dialog, Window? owner)
    {
        if (owner is null)
        {
            _activeTcs?.TrySetResult(false);
            return;
        }

        try
        {
            var result = await dialog.ShowDialog<bool>(owner);
            _activeTcs?.TrySetResult(result);
        }
        catch
        {
            _activeTcs?.TrySetResult(false);
        }
        finally
        {
            if (ReferenceEquals(_activeDialog, dialog))
            {
                _activeDialog = null;
                _activeTcs = null;
            }
        }
    }

    private static void OnActiveCloseRequested(bool result)
    {
        _activeTcs?.TrySetResult(result);
    }

    private static void OnDialogClosed(object? sender, EventArgs e)
    {
        if (sender is ExitPasswordDialog dialog
            && dialog.DataContext is ExitPasswordDialogViewModel viewModel)
        {
            viewModel.CloseRequested -= OnActiveCloseRequested;
            dialog.Closed -= OnDialogClosed;
        }

        if (!ReferenceEquals(sender, _activeDialog))
        {
            return;
        }

        if (_activeTcs is { Task.IsCompleted: false })
        {
            _activeTcs.TrySetResult(false);
        }

        _activeDialog = null;
        _activeTcs = null;
    }

    private void BringActiveDialogToFront()
    {
        Topmost = true;
        Activate();
        FocusPasswordBox();
    }

    private void FocusPasswordBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Activate();
            PasswordBox.Focus(NavigationMethod.Pointer);
        }, DispatcherPriority.Input);
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
        FocusPasswordBox();
    }

    private void OnPasswordBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null || !_viewModel.IsPasswordValid)
        {
            return;
        }

        _viewModel.ConfirmCommand.Execute(null);
        e.Handled = true;
    }
}
