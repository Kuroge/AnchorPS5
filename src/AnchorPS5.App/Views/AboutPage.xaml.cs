using System.Diagnostics;
using AnchorPS5.Core.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnchorPS5.App.Views;

/// <summary>Acerca de: versión, autor, licencia, enlaces, terceros, créditos y registro.</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = App.Localization.Format("app.versionBy", App.Version, "cheyen2008");
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e) =>
        await AppUpdateDialogs.CheckAsync(XamlRoot, ActualTheme, interactive: true);

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        if (AppLog.Directory is { } logs)
            Open(logs);
    }

    private void OnSevenZipLicenseClick(object sender, RoutedEventArgs e) =>
        Open(Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "License.txt"));

    private static void Open(string path)
    {
        try
        {
            if (Directory.Exists(path) || File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppLog.Warn($"No se ha podido abrir {path}", ex);
        }
    }
}
