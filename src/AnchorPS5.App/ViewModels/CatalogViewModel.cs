using System.Collections.ObjectModel;
using AnchorPS5.Core.Catalog;
using AnchorPS5.Core.Localization;
using AnchorPS5.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnchorPS5.App.ViewModels;

/// <summary>Grid del catálogo: carga de fuentes, búsqueda y avisos.</summary>
public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly SourceLoader _loader;
    private readonly IReadOnlyList<Source> _sources;
    private readonly LocalizationService _localization;
    private List<CatalogItemViewModel> _all = [];

    public CatalogViewModel(SourceLoader loader, IReadOnlyList<Source> sources, LocalizationService localization)
    {
        _loader = loader;
        _sources = sources;
        _localization = localization;
    }

    public ObservableCollection<CatalogItemViewModel> Items { get; } = [];

    public bool IsLoaded { get; private set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceErrors))]
    public partial string SourceErrors { get; set; } = string.Empty;

    public bool HasStatus => StatusMessage.Length > 0;

    public bool HasSourceErrors => SourceErrors.Length > 0;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        StatusMessage = _localization.Get("catalog.loading");
        SourceErrors = string.Empty;
        Items.Clear();

        var result = await _loader.LoadAllAsync(_sources);

        _all = result.Entries.Select(e => new CatalogItemViewModel(e, _localization)).ToList();
        SourceErrors = string.Join(Environment.NewLine, result.FailedSources.Select(f =>
            _localization.Format("catalog.sourceError", f.Source.Name, _localization.Get(ErrorKey(f.Error)))));
        IsLoaded = true;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (!IsLoaded)
            return;

        var visible = CatalogSearch.Filter(_all.Select(i => i.Entry), SearchText).ToHashSet();
        Items.Clear();
        foreach (var item in _all.Where(i => visible.Contains(i.Entry)))
            Items.Add(item);

        StatusMessage = _all.Count == 0 ? _localization.Get("catalog.empty")
            : Items.Count == 0 ? _localization.Format("catalog.noResults", SearchText.Trim())
            : string.Empty;
    }

    private static string ErrorKey(SourceErrorKind error) => error switch
    {
        SourceErrorKind.NotFound => "catalog.error.notFound",
        SourceErrorKind.InvalidJson => "catalog.error.invalidJson",
        SourceErrorKind.Network => "catalog.error.network",
        SourceErrorKind.TooLarge => "catalog.error.tooLarge",
        _ => "catalog.error.invalidSource",
    };
}
