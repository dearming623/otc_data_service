using Avalonia;
using OtcDataService.Services;

namespace OtcDataService;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupLaunchMode.Configure(args);

        if (!SingleInstanceGuard.TryAcquire())
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
