using System.Collections.ObjectModel;
using AnchorPS5.Core.Configuration;
using AnchorPS5.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Pantalla de configuración inicial: idioma + carpeta de descargas.</summary>
public sealed partial class SetupViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly FirstRunService _firstRun;
    private readonly Func<Task<string?>> _pickFolder;

    public SetupViewModel(
        LocalizationService localization,
        FirstRunService firstRun,
        string currentLanguage,
        string currentDownloadPath,
        Func<Task<string?>> pickFolder)
    {
        _localization = localization;
        _firstRun = firstRun;
        _pickFolder = pickFolder;

        Languages = new ObservableCollection<LanguageInfo>(localization.DiscoverLanguages());
        SelectedLanguage =
            Languages.FirstOrDefault(l => string.Equals(l.Code, currentLanguage, StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault();
        DownloadPath = currentDownloadPath;
    }

    /// <summary>El usuario ha elegido otro idioma: ya está cargado y la vista se redibuja.</summary>
    public event EventHandler? LanguageChanged;

    partial void OnSelectedLanguageChanged(LanguageInfo? value)
    {
        if (value is null || string.Equals(value.Code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
            return;

        _localization.Load(value.Code);
        ErrorMessage = string.Empty;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Se lanza tras guardar la configuración correctamente.</summary>
    public event EventHandler? Completed;

    public ObservableCollection<LanguageInfo> Languages { get; }

    [ObservableProperty]
    public partial LanguageInfo? SelectedLanguage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DownloadPath { get; set; }

    public bool HasError => ErrorMessage.Length > 0;

    partial void OnDownloadPathChanged(string value) => ErrorMessage = string.Empty;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var folder = await _pickFolder();
        if (!string.IsNullOrEmpty(folder))
            DownloadPath = folder;
    }

    [RelayCommand]
    private void Confirm()
    {
        if (!FirstRunService.TryNormalizeDownloadPath(DownloadPath, out var fullPath))
        {
            ErrorMessage = _localization.Get("setup.invalidPath");
            return;
        }

        try
        {
            _firstRun.SaveSettings(SelectedLanguage?.Code ?? _localization.DefaultLanguage, fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = _localization.Format("setup.folderError", fullPath);
            return;
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }
}
