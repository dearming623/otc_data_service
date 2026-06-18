namespace OtcDataService.Services;

public static class StartupLaunchMode
{
    public static bool IsMinimized { get; private set; }

    public static void Configure(string[] args)
    {
        IsMinimized = args.Any(arg =>
            string.Equals(arg, StartupService.MinimizedArgument, StringComparison.OrdinalIgnoreCase));
    }
}
