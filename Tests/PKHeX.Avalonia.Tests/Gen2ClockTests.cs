using System.Globalization;
using Moq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen2ClockTests
{
    [Theory]
    [InlineData(LanguageID.English, GameVersion.GS)]
    [InlineData(LanguageID.English, GameVersion.C)]
    [InlineData(LanguageID.Korean, GameVersion.GS)]
    public void PasswordMatchesCoreWithPendingInputsWithoutMutatingSave(LanguageID language, GameVersion version)
    {
        var save = new SAV2(language, version) { OT = "GOLD", TID16 = 12345, Money = 98765 };
        var before = save.Data.ToArray();
        var vm = new TrainerEditorViewModel(save);
        Assert.True(vm.HasGen2ClockPassword);
        vm.CalculateGen2ClockPasswordCommand.Execute(null);
        Assert.Equal(save.ResetKey.ToString("D5", CultureInfo.InvariantCulture), vm.Gen2ClockPassword);
        Assert.Equal(before, save.Data.ToArray());
        vm.Money = 123456;
        Assert.Empty(vm.Gen2ClockPassword);
        vm.DisplayTid = 54321;
        vm.TrainerName = "SILVER";
        var expected = new SAV2(before.ToArray(), language, version) { OT = "SILVER", TID16 = 54321, Money = 123456 };
        vm.CalculateGen2ClockPasswordCommand.Execute(null);
        Assert.Equal(expected.ResetKey.ToString("D5", CultureInfo.InvariantCulture), vm.Gen2ClockPassword);
        Assert.Equal(before, save.Data.ToArray());
        vm.SaveCommand.Execute(null);
        Assert.Equal(save.ResetKey.ToString("D5", CultureInfo.InvariantCulture), vm.Gen2ClockPassword);
    }

    [Theory]
    [InlineData(LanguageID.English, GameVersion.GS)]
    [InlineData(LanguageID.English, GameVersion.C)]
    [InlineData(LanguageID.Japanese, GameVersion.GS)]
    [InlineData(LanguageID.Japanese, GameVersion.C)]
    [InlineData(LanguageID.Korean, GameVersion.GS)]
    public async Task ResetRequiresConfirmationAndPersistsOnExport(LanguageID language, GameVersion version)
    {
        var save = new SAV2(language, version);
        var offset = new SAV2Offsets(save).RTCFlags;
        save.Data[offset] = 0x25;
        var dialogs = new Mock<IDialogService>();
        dialogs.SetupSequence(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false).ReturnsAsync(true);
        var vm = new TrainerEditorViewModel(save, dialogs.Object) { Money = 42 };
        var before = save.Data.ToArray();
        await vm.ResetGen2ClockCommand.ExecuteAsync(null);
        Assert.Equal(before, save.Data.ToArray());
        Assert.Empty(vm.Gen2ClockStatus);
        await vm.ResetGen2ClockCommand.ExecuteAsync(null);
        Assert.Equal(0xA5, save.Data[offset]);
        before[offset] = 0xA5;
        Assert.Equal(before, save.Data.ToArray()); // Do not apply pending trainer edits.
        Assert.True(save.State.Edited);
        Assert.NotEmpty(vm.Gen2ClockStatus);
        dialogs.Verify(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        var reloaded = new SAV2(save.Write().ToArray(), language, version);
        Assert.Equal(0xA5, reloaded.Data[offset]);
    }

    [Fact]
    public void JapaneseHasResetButNoPassword()
    {
        var vm = new TrainerEditorViewModel(new SAV2(LanguageID.Japanese));
        Assert.True(vm.HasGen2Clock);
        Assert.False(vm.HasGen2ClockPassword);
        Assert.False(vm.CalculateGen2ClockPasswordCommand.CanExecute(null));
        vm.CalculateGen2ClockPasswordCommand.Execute(null);
        Assert.Empty(vm.Gen2ClockPassword);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void OtherGenerationsDoNotOfferClockActions(int generation)
    {
        SaveFile save = generation switch
        {
            1 => new SAV1(), 3 => new SAV3E(), 4 => new SAV4DP(), 5 => new SAV5BW(),
            6 => new SAV6XY(), 7 => new SAV7SM(), 8 => new SAV8SWSH(), _ => new SAV9SV(),
        };
        var vm = new TrainerEditorViewModel(save);
        Assert.False(vm.HasGen2Clock);
        Assert.False(vm.ResetGen2ClockCommand.CanExecute(null));
        Assert.False(vm.HasGen2ClockPassword);
        Assert.False(vm.CalculateGen2ClockPasswordCommand.CanExecute(null));
    }

    [Fact]
    public async Task ChineseOtBytesSurviveCalculationAndClockReset()
    {
        var save = new SAV2();
        Assert.True(Gen12ChineseNames.TryEncode("小智", save.OriginalTrainerTrash));
        var raw = save.OriginalTrainerTrash.ToArray();
        var dialogs = new Mock<IDialogService>();
        dialogs.Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        var vm = new TrainerEditorViewModel(save, dialogs.Object);
        vm.CalculateGen2ClockPasswordCommand.Execute(null);
        Assert.Equal(save.ResetKey.ToString("D5", CultureInfo.InvariantCulture), vm.Gen2ClockPassword);
        Assert.Equal(raw, save.OriginalTrainerTrash.ToArray());
        await vm.ResetGen2ClockCommand.ExecuteAsync(null);
        Assert.Equal(raw, save.OriginalTrainerTrash.ToArray());
    }

    [AvaloniaTheory]
    [InlineData(1, false, false)]
    [InlineData(2, true, true)]
    [InlineData(3, false, false)]
    [InlineData(0, true, false)]
    public void TrainerViewShowsOnlyApplicableControls(int generation, bool section, bool password)
    {
        SaveFile save = generation switch { 1 => new SAV1(), 2 => new SAV2(), 0 => new SAV2(LanguageID.Japanese), _ => new SAV3E() };
        var view = new TrainerEditor { DataContext = new TrainerEditorViewModel(save) };
        var window = new Window { Content = view, Width = 900, Height = 700 };
        try
        {
            window.Show();
            Assert.Equal(section, view.FindControl<Border>("Gen2ClockSection")!.IsEffectivelyVisible);
            Assert.Equal(password, view.FindControl<Button>("CalculateGen2ClockPasswordButton")!.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }
}
