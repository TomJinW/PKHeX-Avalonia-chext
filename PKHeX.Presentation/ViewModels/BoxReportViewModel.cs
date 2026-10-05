using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// Box Data Report: a sortable spreadsheet of every Pokémon stored in the save's boxes,
/// shown in a modeless tool window. Mirrors the WinForms "Box Data Report" (ReportGrid).
/// </summary>
public partial class BoxReportViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IDialogService _dialogService;
    private readonly AppSettings _settings;
    private readonly ISettingsStore? _settingsStore;
    private BoxReportRow[] _allRows = [];
    public BoxReportColumn[] Columns { get; } = BoxReportColumn.CreateDefaults();
    private bool _resettingColumns;
    public event Action<bool>? ColumnLayoutChanged;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BoxReportRow> _rows = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateSelectedRowCommand))]
    private BoxReportRow? _selectedRow;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Raised when the user activates (double-clicks) a row.</summary>
    public event Action<BoxReportRow>? RowActivated;

    public BoxReportViewModel(SaveFile sav, IDialogService dialogService, AppSettings? settings = null, ISettingsStore? settingsStore = null)
    {
        _sav = sav;
        _dialogService = dialogService;
        _settings = settings ?? new AppSettings();
        _settingsStore = settingsStore;
        foreach (var column in Columns)
        {
            var saved = _settings.BoxReportColumns.FirstOrDefault(c => c.Id == column.Id);
            column.IsVisible = column.IsIdentity || (saved?.Visible ?? column.DefaultVisible);
            column.PropertyChanged += (_, e) => { if (!_resettingColumns && e.PropertyName == nameof(BoxReportColumn.IsVisible)) ColumnLayoutChanged?.Invoke(false); };
        }
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        var strings = GameInfo.Strings;
        var slotsPerBox = _sav.BoxSlotCount;
        var data = _sav.BoxData;

        var rows = new ObservableCollection<BoxReportRow>();
        for (int i = 0; i < data.Count; i++)
        {
            var pk = data[i];
            if (pk.Species == 0 || !pk.Valid)
                continue;
            rows.Add(new BoxReportRow(pk, strings, i / slotsPerBox, i % slotsPerBox, _sav, _settings));
        }

        _allRows = rows.ToArray();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        Rows = new ObservableCollection<BoxReportRow>(_allRows.Where(row => search.Length == 0 ||
            Columns.Any(column => column.GetValue(row)?.ToString()?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true)));
        if (SelectedRow is not null && !Rows.Contains(SelectedRow))
            SelectedRow = null;
        StatusText = LocalizedStrings.Instance.Format("BoxReport_ResultCount", Rows.Count, _allRows.Length, _sav.BoxCount);
    }

    public AppSettings.ReportColumnLayout? GetColumnLayout(string id) => _settings.BoxReportColumns.FirstOrDefault(c => c.Id == id);

    public void SaveColumnLayout(System.Collections.Generic.IEnumerable<AppSettings.ReportColumnLayout> layout)
    {
        _settings.BoxReportColumns = layout.ToList();
        _settingsStore?.Save(_settings);
    }

    [RelayCommand]
    private void ResetColumns()
    {
        _settings.BoxReportColumns.Clear();
        _resettingColumns = true;
        foreach (var column in Columns)
            column.IsVisible = column.DefaultVisible;
        _resettingColumns = false;
        ColumnLayoutChanged?.Invoke(true);
    }

    private bool HasSelectedRow => SelectedRow is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedRow))]
    private void ActivateSelectedRow()
    {
        if (SelectedRow is not null)
            RowActivated?.Invoke(SelectedRow);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ExportCsvAsync()
    {
        var path = await _dialogService.SaveFileAsync(LocalizedStrings.Instance["BoxReport_ExportTitle"], "BoxReport.csv", ["csv"]);
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.WriteAllText(path, BuildCsv(Rows), Encoding.UTF8);
            await _dialogService.ShowInformationAsync(LocalizedStrings.Instance["BoxReport_ExportCompleteTitle"], LocalizedStrings.Instance.Format("BoxReport_ExportCompleteMessage", Rows.Count, path));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(LocalizedStrings.Instance["BoxReport_ExportErrorTitle"], ex.Message);
        }
    }

    private static readonly (string Header, Func<BoxReportRow, object?> Get)[] CsvColumns =
    [
        ("Position", r => r.Position),
        ("Species", r => r.Species),
        ("Nickname", r => r.Nickname),
        ("Level", r => r.Level),
        ("Nature", r => r.Nature),
        ("Gender", r => r.Gender),
        ("ESV", r => r.ESV),
        ("HiddenPower", r => r.HP_Type),
        ("Ability", r => r.Ability),
        ("HeldItem", r => r.HeldItem),
        ("Ball", r => r.Ball),
        ("Move1", r => r.Move1),
        ("Move2", r => r.Move2),
        ("Move3", r => r.Move3),
        ("Move4", r => r.Move4),
        ("HP", r => r.HP),
        ("ATK", r => r.ATK),
        ("DEF", r => r.DEF),
        ("SPA", r => r.SPA),
        ("SPD", r => r.SPD),
        ("SPE", r => r.SPE),
        ("IVTotal", r => r.IVTotal),
        ("EVTotal", r => r.EVTotal),
        ("MetLocation", r => r.MetLoc),
        ("OriginGame", r => r.Version),
        ("OT", r => r.OT),
        ("Shiny", r => r.IsShiny),
        ("Legal", r => r.Legal),
        ("Checksum", r => r.Checksum),
    ];

    private static string BuildCsv(IEnumerable<BoxReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Array.ConvertAll(CsvColumns, c => c.Header)));
        foreach (var row in rows)
        {
            for (int i = 0; i < CsvColumns.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(EscapeCsv(CsvColumns[i].Get(row)?.ToString() ?? string.Empty));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
