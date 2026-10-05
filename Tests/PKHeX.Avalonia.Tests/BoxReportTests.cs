using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Automation;
using Avalonia.Threading;
using PKHeX.Application.Services;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Views;
using PKHeX.Presentation.Localization;
using Moq;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class BoxReportTests
{
    private readonly Mock<IDialogService> _dialogServiceMock = new();

    private static SAV3E CreateSaveWithBoxMon(out PK3 pk)
    {
        var sav = new SAV3E();
        pk = new PK3
        {
            Species = (ushort)Species.Mudkip,
            CurrentLevel = 5,
        };
        sav.SetBoxSlotAtIndex(pk, 1, 3); // box 1, slot 3
        return sav;
    }

    [AvaloniaFact]
    public void Refresh_BuildsOneRowPerOccupiedSlot()
    {
        var sav = CreateSaveWithBoxMon(out _);
        var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);

        var row = Assert.Single(vm.Rows);
        Assert.Equal(1, row.Box);
        Assert.Equal(3, row.Slot);
        Assert.Equal("B02:04", row.Position);
        Assert.Equal((byte)5, row.Level);
        Assert.Contains("1 Pokémon", vm.StatusText);
    }

    [AvaloniaFact]
    public void Refresh_PicksUpBoxChanges()
    {
        var sav = CreateSaveWithBoxMon(out var pk);
        var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);
        Assert.Single(vm.Rows);

        sav.SetBoxSlotAtIndex(pk, 0, 0);
        vm.Refresh();

        Assert.Equal(2, vm.Rows.Count);
    }

    [AvaloniaFact]
    public void ActivateSelectedRow_RaisesRowActivated()
    {
        var sav = CreateSaveWithBoxMon(out _);
        var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);

        BoxReportRow? activated = null;
        vm.RowActivated += r => activated = r;

        vm.ActivateSelectedRowCommand.Execute(null); // no selection: no event
        Assert.Null(activated);

        vm.SelectedRow = vm.Rows[0];
        vm.ActivateSelectedRowCommand.Execute(null);
        Assert.Same(vm.Rows[0], activated);
    }

    [AvaloniaFact]
    public async Task ExportCsv_WritesHeaderAndRows()
    {
        var sav = CreateSaveWithBoxMon(out _);
        var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);

        var path = Path.Combine(Path.GetTempPath(), $"boxreport-test-{Guid.NewGuid():N}.csv");
        _dialogServiceMock
            .Setup(d => d.SaveFileAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]?>()))
            .ReturnsAsync(path);

        try
        {
            await vm.ExportCsvCommand.ExecuteAsync(null);

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length); // header + 1 row
            Assert.StartsWith("Position,Species,", lines[0]);
            Assert.StartsWith("B02:04,", lines[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void Search_AllFieldsAndRefresh_PreservesFilterAndClearsStaleSelection()
    {
        var sav = new SAV6XY();
        var pk = new PK6 { Species = 94, Ability = 130, Move1 = 421 };
        sav.SetBoxSlotAtIndex(pk, 1, 3);
        var before = sav.Data.ToArray();
        var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);
        vm.SelectedRow = vm.Rows[0];
        vm.SearchText = GameInfo.Strings.movelist[421];
        Assert.Single(vm.Rows);
        vm.SearchText = GameInfo.Strings.abilitylist[130];
        Assert.Single(vm.Rows);
        vm.SearchText = "missing-value";
        Assert.Empty(vm.Rows);
        Assert.Null(vm.SelectedRow);
        vm.Refresh();
        Assert.Empty(vm.Rows);
        vm.SearchText = "B02:04";
        Assert.Single(vm.Rows);
        Assert.Equal(before, sav.Data.ToArray());
    }

    [AvaloniaFact]
    public void ColumnLayout_PersistsByIdentity_AndResetsWithoutChangingSave()
    {
        var sav = CreateSaveWithBoxMon(out _);
        var settings = new AppSettings();
        var store = new Mock<ISettingsStore>();
        var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object, settings, store.Object);
        var view = new BoxReportView { DataContext = vm };
        var grid = view.FindControl<DataGrid>("ReportGrid")!;
        Assert.Equal(29, grid.Columns.Count);
        Assert.Equal(7, grid.Columns.Count(c => c.IsVisible));
        Assert.Equal(2, grid.FrozenColumnCount);
        grid.Columns[8].Width = new DataGridLength(237);
        vm.Columns[11].IsVisible = true;
        Assert.True(grid.Columns[11].IsVisible);
        grid.Columns[11].DisplayIndex = 3;
        // Closing persists final reorder geometry, including programmatic changes.
        var window = new Window { Content = view, Width = 700, Height = 400 };
        window.Show();
        window.Close();
        Assert.Contains(settings.BoxReportColumns, c => c.Id == "Ability" && c.Width == 237);
        Assert.Contains(settings.BoxReportColumns, c => c.Id == "Move1" && c.Visible && c.Order == 3);
        store.Verify(s => s.Save(settings), Times.AtLeastOnce);
        var restored = new BoxReportView { DataContext = new BoxReportViewModel(sav, _dialogServiceMock.Object, settings, store.Object) };
        var restoredGrid = restored.FindControl<DataGrid>("ReportGrid")!;
        Assert.Equal(237, restoredGrid.Columns[8].Width.Value);
        Assert.Equal(3, restoredGrid.Columns[11].DisplayIndex);
        ((BoxReportViewModel)restored.DataContext!).ResetColumnsCommand.Execute(null);
        Assert.Equal(7, restoredGrid.Columns.Count(c => c.IsVisible));
        Assert.Equal(160, restoredGrid.Columns[8].Width.Value);
        Assert.Equal(11, restoredGrid.Columns[11].DisplayIndex);
    }

    [AvaloniaFact]
    public void CorruptLayout_DoesNotHideIdentityOrApplyInvalidWidths()
    {
        var settings = new AppSettings { BoxReportColumns =
        [
            new() { Id = "Species", Width = double.NaN, Order = 99, Visible = false },
            new() { Id = "Position", Width = -1, Order = 99, Visible = false },
            new() { Id = "Move1", Width = 99999, Order = -40, Visible = true },
        ] };
        var vm = new BoxReportViewModel(CreateSaveWithBoxMon(out _), _dialogServiceMock.Object, settings);
        var view = new BoxReportView { DataContext = vm };
        var grid = view.FindControl<DataGrid>("ReportGrid")!;
        Assert.True(grid.Columns[0].IsVisible);
        Assert.True(grid.Columns[1].IsVisible);
        Assert.Equal(0, grid.Columns[0].DisplayIndex);
        Assert.Equal(1, grid.Columns[1].DisplayIndex);
        Assert.Equal(110, grid.Columns[0].Width.Value);
        Assert.Equal(160, grid.Columns[1].Width.Value);
        Assert.Equal(160, grid.Columns[11].Width.Value);
    }

    [AvaloniaTheory]
    [InlineData("en", 1100, 600)]
    [InlineData("de", 700, 400)]
    [InlineData("fr", 480, 320)]
    public void ReportLayout_LongValues_HaveFullTooltipAndAccessibleDescription(string language, int width, int height)
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            var sav = CreateSaveWithBoxMon(out var pk);
            pk.Ability = 130;
            pk.Move1 = 421;
            sav.SetBoxSlotAtIndex(pk, 1, 3);
            var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);
            vm.Columns[11].IsVisible = true;
            var view = new BoxReportView { DataContext = vm };
            window = new Window { Content = view, Width = width, Height = height };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var grid = view.FindControl<DataGrid>("ReportGrid")!;
            Assert.True(grid.Bounds.Height > 80);
            Assert.True(grid.Bounds.Width <= width);
            foreach (var index in new[] { 0, 1, 8, 11 })
            {
                var descriptor = vm.Columns[index];
                var column = Assert.IsType<DataGridTemplateColumn>(grid.Columns[index]);
                var header = Assert.IsType<TextBlock>(column.Header);
                Assert.Equal(descriptor.Header, ToolTip.GetTip(header));
                Assert.Equal(descriptor.Header, AutomationProperties.GetHelpText(header));
                var cell = Assert.IsType<TextBlock>(column.CellTemplate!.Build(vm.Rows[0]));
                Assert.Equal(descriptor.GetValue(vm.Rows[0])?.ToString(), ToolTip.GetTip(cell));
                Assert.Equal(cell.Text, AutomationProperties.GetHelpText(cell));
                Assert.True(column.CanUserSort);
                Assert.Equal(descriptor.Id, column.SortMemberPath);
            }
        }
        finally
        {
            window?.Close();
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }

    [AvaloniaFact]
    public void ColumnSort_UsesNumericStats_AndLanguageChangeRefreshesHeaders()
    {
        var previous = LocalizedStrings.Instance.CurrentLanguage;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage("en");
            var sav = new SAV6XY();
            sav.SetBoxSlotAtIndex(new PK6 { Species = 25, CurrentLevel = 50 }, 0);
            sav.SetBoxSlotAtIndex(new PK6 { Species = 25, CurrentLevel = 5 }, 1);
            var vm = new BoxReportViewModel(sav, _dialogServiceMock.Object);
            var view = new BoxReportView { DataContext = vm };
            window = new Window { Content = view, Width = 700, Height = 400 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var grid = view.FindControl<DataGrid>("ReportGrid")!;
            Assert.Equal("NumericHP", grid.Columns[15].SortMemberPath);
            grid.Columns[15].Sort(System.ComponentModel.ListSortDirection.Ascending);
            Dispatcher.UIThread.RunJobs();
            var dataView = Assert.IsAssignableFrom<global::Avalonia.Collections.IDataGridCollectionView>(grid.CollectionView);
            var rows = dataView.Cast<BoxReportRow>().ToArray();
            Assert.Equal((byte)5, rows[0].Level);
            Assert.Equal((byte)50, rows[1].Level);
            var before = ((TextBlock)grid.Columns[1].Header).Text;
            LocalizedStrings.Instance.SetLanguage("de");
            Assert.NotEqual(before, ((TextBlock)grid.Columns[1].Header).Text);
        }
        finally
        {
            window?.Close();
            LocalizedStrings.Instance.SetLanguage(previous);
        }
    }
}
