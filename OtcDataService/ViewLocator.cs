using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OtcDataService.ViewModels;

namespace OtcDataService;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        if (name.Contains(".ViewModels.", StringComparison.Ordinal))
        {
            name = name.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        }

        var type = Type.GetType(name);
        if (type is null && name.EndsWith("ExportSettingsView", StringComparison.Ordinal))
        {
            type = Type.GetType("OtcDataService.Views.Settings.ExportSettingsView");
        }

        if (type is null && name.EndsWith("DatabaseSettingsView", StringComparison.Ordinal))
        {
            type = Type.GetType("OtcDataService.Views.Settings.DatabaseSettingsView");
        }

        if (type is null && name.EndsWith("ActivityLogView", StringComparison.Ordinal))
        {
            type = Type.GetType("OtcDataService.Views.Home.ActivityLogView");
        }

        if (type is null && name.EndsWith("ManualView", StringComparison.Ordinal))
        {
            type = Type.GetType("OtcDataService.Views.Home.ManualView");
        }

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
