using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using OtcDataService.Services;
using OtcDataService.ViewModels;
using OtcDataService.Views;

namespace OtcDataService;

public partial class App : Application
{
    public TrayApplicationViewModel TrayViewModel { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SingleInstanceGuard.RegisterActivateCallback(() =>
            {
                Dispatcher.UIThread.Post(TrayViewModel.ShowMainWindow);
            });

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var mainWindow = new MainWindow
            {
                DataContext = TrayViewModel.MainWindowViewModel
            };
            TrayViewModel.ConfigureMainWindow(mainWindow);
            desktop.MainWindow = mainWindow;

            var enableMenuItem = new NativeMenuItem
            {
                Header = "Enable"
            };
            enableMenuItem.Click += (_, _) => TrayViewModel.ToggleEnableCommand.Execute(null);

            var settingMenuItem = new NativeMenuItem
            {
                Header = "Setting"
            };
            settingMenuItem.Click += (_, _) => TrayViewModel.OpenSettingsCommand.Execute(null);

            var exitMenuItem = new NativeMenuItem
            {
                Header = "Exit"
            };
            exitMenuItem.Click += (_, _) => TrayViewModel.ExitCommand.Execute(null);

            var trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://OtcDataService/Assets/app-icon.ico"))),
                ToolTipText = AppInfo.WindowTitle,
                Menu = new NativeMenu
                {
                    Items =
                    {
                        enableMenuItem,
                        new NativeMenuItemSeparator(),
                        settingMenuItem,
                        new NativeMenuItemSeparator(),
                        exitMenuItem
                    }
                },
                IsVisible = true
            };

            TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
            TrayViewModel.AttachTray(trayIcon, enableMenuItem, settingMenuItem);
            trayIcon.Clicked += (_, _) => TrayViewModel.ShowMainWindow();

            AppServices.Log.Info("OTC Data Service started.");
            AppServices.Log.Info($"Process architecture: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")} (ODBC requires matching DSN bitness).");
            AppServices.Log.Info("Single-instance guard active (local mutex and LAN TCP lock).");
            TrayViewModel.ShowMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
