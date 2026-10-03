using AnchorPS5.App.Services;
using AnchorPS5.Core.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AnchorPS5.App.Views;

/// <summary>Acerca de: versión, autor, licencia, enlaces, terceros, créditos y registro.</summary>
public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = App.Localization.Format("app.versionBy", App.Version, "cheyen2008");
    }

    private async void OnCheckUpdatesClick(object? sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            await AppUpdateDialogs.CheckAsync(this, interactive: true);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void OnOpenLogsClick(object? sender, RoutedEventArgs e)
    {
        if (AppLog.Directory is { } logs && Directory.Exists(logs))
            ShellOpen.Open(logs);
    }

    private void OnSevenZipLicenseClick(object? sender, RoutedEventArgs e)
    {
        var license = Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "License.txt");
        if (File.Exists(license))
            ShellOpen.Open(license);
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
            ShellOpen.Open(url);
    }
}
