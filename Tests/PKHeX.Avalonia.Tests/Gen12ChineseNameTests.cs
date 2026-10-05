using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Avalonia.Tests.Harness;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Moq;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen12ChineseNameTests
{
    private static AppSettings ChineseSettings() => new() { Gen12NameEncoding = Gen12NameEncoding.SimplifiedChinese };
    private static PokemonEditorViewModel Editor(PKM pk, SaveFile save, AppSettings settings)
        => new(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>(), settings: settings);

    [Fact]
    public void NewAndLegacySettingsDefaultToChineseButPreserveExplicitOfficialChoice()
    {
        Assert.Equal(Gen12NameEncoding.SimplifiedChinese, new AppSettings().Gen12NameEncoding);
        Assert.Equal(Gen12NameEncoding.SimplifiedChinese, JsonSerializer.Deserialize<AppSettings>("{}")!.Gen12NameEncoding);
        var official = new AppSettings { Gen12NameEncoding = Gen12NameEncoding.Official };
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(official))!;
        Assert.Equal(Gen12NameEncoding.Official, restored.Gen12NameEncoding);
    }

    [Fact]
    public void SuppliedTableUsesExactBytesAndSupportsMixedNames()
    {
        byte[] bytes = new byte[11];
        Assert.True(Gen12ChineseNames.TryEncode("皮卡丘", bytes));
        Assert.Equal(new byte[] { 0x0D, 0xE8, 0x0B, 0x04, 0x0E, 0xB0, 0x50, 0x50, 0x50, 0x50, 0x50 }, bytes);
        Assert.Equal("皮卡丘", Gen12ChineseNames.Decode(bytes));
        Assert.True(Gen12ChineseNames.TryEncode("A小智9", bytes));
        Assert.Equal("A小智9", Gen12ChineseNames.Decode(bytes));
        Assert.Equal("\uFFFD", Gen12ChineseNames.Decode(new byte[] { 0x2E, 0xB8, 0x50 }));
    }

    [Theory]
    [InlineData("皮卡丘皮卡丘")]
    [InlineData("皮😀")]
    [InlineData("啰")]
    [InlineData("皮\0")]
    [InlineData("<TX_PAUSE>")]
    public void InvalidInputDoesNotModifyDestination(string value)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xAB, 11).ToArray();
        var before = bytes.ToArray();
        Assert.False(Gen12ChineseNames.TryEncode(value, bytes));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void CapacityIsInBytesAndAlwaysReservesTerminator()
    {
        byte[] international = new byte[11];
        Assert.True(Gen12ChineseNames.TryEncode("皮卡丘小智", international));
        Assert.Equal(0x50, international[10]);
        Assert.False(Gen12ChineseNames.TryEncode("皮卡丘小智A", international));
        byte[] japanese = new byte[6];
        Assert.True(Gen12ChineseNames.TryEncode("小智", japanese, true));
        Assert.False(Gen12ChineseNames.TryEncode("皮卡丘", japanese, true));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void EditorReadsWritesAndSaveRoundTripsBothNameBuffers(int generation, bool japanese)
    {
        var language = japanese ? LanguageID.Japanese : LanguageID.English;
        SaveFile save = generation == 1 ? new SAV1(language) : new SAV2(language);
        PKM pk = generation == 1 ? new PK1(japanese) : new PK2(japanese);
        pk.Species = 25;
        pk.CurrentLevel = 20;
        Gen12ChineseNames.TryEncode("小智", pk.NicknameTrash, japanese);
        Gen12ChineseNames.TryEncode("赤红", pk.OriginalTrainerTrash, japanese);
        var vm = Editor(pk, save, ChineseSettings());
        Assert.Equal("小智", vm.Nickname);
        Assert.Equal("赤红", vm.OriginalTrainerName);
        vm.Nickname = "卡丘";
        vm.OriginalTrainerName = "小智";
        var prepared = vm.PreparePKM();
        Assert.Equal("卡丘", Gen12ChineseNames.Decode(prepared.NicknameTrash, japanese));
        Assert.Equal("小智", Gen12ChineseNames.Decode(prepared.OriginalTrainerTrash, japanese));
        Assert.Equal("小智", Gen12ChineseNames.Decode(pk.NicknameTrash, japanese)); // Editor owns a clone.
        save.SetBoxSlotAtIndex(prepared, 0, 0);
        save.SetPartySlotAtIndex(prepared, 0);
        var raw = save.Write().ToArray();
        SaveFile reloaded = generation == 1 ? new SAV1(raw, language, save.Version) : new SAV2(raw, language, save.Version);
        foreach (var result in new[] { reloaded.GetBoxSlotAtIndex(0, 0), reloaded.GetPartySlotAtIndex(0) })
        {
            Assert.Equal(prepared.NicknameTrash.ToArray(), result.NicknameTrash.ToArray());
            Assert.Equal(prepared.OriginalTrainerTrash.ToArray(), result.OriginalTrainerTrash.ToArray());
        }
    }

    [Fact]
    public void UnknownBytesAndTrailingDataSurviveUnrelatedEditsAndModeChanges()
    {
        var pk = new PK2 { Species = 25, CurrentLevel = 20 };
        new byte[] { 0x2E, 0xFF, 0x50, 0x01, 0x23, 0x45, 0, 0, 0, 0, 0 }.CopyTo(pk.NicknameTrash);
        Gen12ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        var nickname = pk.NicknameTrash.ToArray();
        var trainer = pk.OriginalTrainerTrash.ToArray();
        var settings = new AppSettings { Gen12NameEncoding = Gen12NameEncoding.Official };
        var vm = Editor(pk, new SAV2(), settings);
        Assert.Equal(nickname, vm.TargetPKM.NicknameTrash.ToArray());
        settings.Gen12NameEncoding = Gen12NameEncoding.SimplifiedChinese;
        vm.RefreshNameEncoding();
        Assert.Equal("小智", vm.OriginalTrainerName);
        vm.Level = 25;
        var prepared = vm.PreparePKM();
        Assert.Equal(nickname, prepared.NicknameTrash.ToArray());
        Assert.Equal(trainer, prepared.OriginalTrainerTrash.ToArray());
        settings.Gen12NameEncoding = Gen12NameEncoding.Official;
        vm.RefreshNameEncoding();
        Assert.Equal(nickname, vm.TargetPKM.NicknameTrash.ToArray());
        Assert.Equal(trainer, vm.TargetPKM.OriginalTrainerTrash.ToArray());
    }

    [Fact]
    public void InvalidEditorNameBlocksPreparationWithoutWritingEitherName()
    {
        var pk = new PK2 { Species = 25, CurrentLevel = 20 };
        Gen12ChineseNames.TryEncode("小智", pk.NicknameTrash);
        Gen12ChineseNames.TryEncode("赤红", pk.OriginalTrainerTrash);
        var vm = Editor(pk, new SAV2(), ChineseSettings());
        vm.Nickname = "皮卡丘皮卡丘";
        Assert.NotEmpty(vm.NameEncodingError);
        Assert.False(vm.CheckNameEncoding());
        Assert.Throws<InvalidOperationException>(() => vm.PreparePKM());
        Assert.Equal(pk.NicknameTrash.ToArray(), vm.TargetPKM.NicknameTrash.ToArray());
        vm.OriginalTrainerName = "小智";
        Assert.Equal(pk.OriginalTrainerTrash.ToArray(), vm.TargetPKM.OriginalTrainerTrash.ToArray());
        vm.Nickname = "皮卡丘";
        Assert.True(vm.CheckNameEncoding());
        Assert.Equal("小智", Gen12ChineseNames.Decode(vm.PreparePKM().OriginalTrainerTrash));
    }

    [Fact]
    public void ExplicitOfficialKoreanAndModernFormatsIgnoreOverride()
    {
        var pk = new PK2 { Species = 25, CurrentLevel = 20, OriginalTrainerName = "지우", Nickname = "피카츄" };
        var vm = Editor(pk, new SAV2(LanguageID.Korean, GameVersion.GS), new AppSettings { Gen12NameEncoding = Gen12NameEncoding.Official });
        Assert.Equal("지우", vm.OriginalTrainerName);
        Assert.Equal("피카츄", vm.Nickname);
        Assert.Equal(pk.OriginalTrainerTrash.ToArray(), vm.PreparePKM().OriginalTrainerTrash.ToArray());
        Assert.Equal(pk.NicknameTrash.ToArray(), vm.PreparePKM().NicknameTrash.ToArray());
        var modern = Editor(new PK9 { Species = 25, Nickname = "皮卡丘", IsNicknamed = true }, new SAV9SV(), ChineseSettings());
        Assert.False(modern.UsesChineseNames);
        Assert.False(modern.HasLegacyNames);
        Assert.Equal("皮卡丘", modern.Nickname);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SameBytesCanBeInterpretedAsKoreanOrChineseWithoutTranscoding(int generation)
    {
        PKM pk = generation == 1 ? new PK1() : new PK2();
        SaveFile save = generation == 1 ? new SAV1() : new SAV2();
        pk.Species = 25;
        pk.CurrentLevel = 20;
        Gen12ChineseNames.TryEncode("赤红", pk.OriginalTrainerTrash);
        Gen12ChineseNames.TryEncode("卡", pk.NicknameTrash);
        Assert.True(pk.Korean); // The first byte of 赤 is 06, also an official Korean prefix.
        var official = pk.OriginalTrainerName;
        Assert.NotEqual("赤红", official);
        var raw = pk.OriginalTrainerTrash.ToArray();
        var settings = new AppSettings { Gen12NameEncoding = Gen12NameEncoding.Official };
        var vm = Editor(pk, save, settings);
        Assert.Equal(official, vm.OriginalTrainerName);
        settings.Gen12NameEncoding = Gen12NameEncoding.SimplifiedChinese;
        vm.RefreshNameEncoding();
        Assert.Equal("赤红", vm.OriginalTrainerName);
        Assert.Equal(raw, vm.PreparePKM().OriginalTrainerTrash.ToArray());
        settings.Gen12NameEncoding = Gen12NameEncoding.Official;
        vm.RefreshNameEncoding();
        Assert.Equal(official, vm.OriginalTrainerName);
        Assert.Equal(raw, vm.PreparePKM().OriginalTrainerTrash.ToArray());
    }

    [Fact]
    public void EverySuppliedCharacterEncodesToItsDeclaredBytes()
    {
        using var stream = typeof(Gen12ChineseNames).Assembly.GetManifestResourceStream("PKHeX.Application.Resources.Gen12.charmap_cn.asm")!;
        using var reader = new StreamReader(stream);
        int count = 0;
        while (reader.ReadLine() is { } line)
        {
            if (!line.TrimStart().StartsWith("charmap_w ")) continue;
            var fields = line.Split('"');
            if (fields.Length < 3 || fields[1].Length != 1) continue;
            var hex = fields[2].Split(';')[0].Split(',').Skip(1).Select(x =>
                byte.Parse(x.Trim().TrimStart('$'), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
            var bytes = new byte[3];
            Assert.True(Gen12ChineseNames.TryEncode(fields[1], bytes));
            Assert.Equal(hex, bytes[..2]);
            Assert.Equal(fields[1] == "　" ? "〇" : fields[1], Gen12ChineseNames.Decode(bytes));
            count++;
        }
        Assert.Equal(7615, count);
    }

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void ComposedEditorSetsChineseNamesAndRejectsInvalidNames(int generation)
    {
        using var app = new HeadlessAppFixture();
        app.Services.GetRequiredService<AppSettings>().Gen12NameEncoding = Gen12NameEncoding.SimplifiedChinese;
        SaveFile save = generation == 1 ? new SAV1() : new SAV2();
        app.LoadSaveInstance(save);
        PKM pk = generation == 1 ? new PK1() : new PK2();
        pk.Species = 25;
        pk.CurrentLevel = 20;
        Gen12ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
        Gen12ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        var vm = app.ViewModel.CurrentPokemonEditor!;
        Assert.True(vm.LoadPKM(pk));
        Assert.Equal("皮卡丘", vm.Nickname);
        app.BoxViewer!.SetSlotCommand.Execute(app.BoxViewer.Slots[0]);
        Assert.Equal("皮卡丘", app.BoxViewer.Slots[0].Nickname);
        Assert.Equal("小智", Gen12ChineseNames.Decode(save.GetBoxSlotAtIndex(0, 0).OriginalTrainerTrash));
        var before = save.GetBoxSlotAtIndex(0, 0);
        vm.OriginalTrainerName = "小智😀";
        app.BoxViewer.SetSlotCommand.Execute(app.BoxViewer.Slots[0]);
        Assert.True(before.EqualsStored(save.GetBoxSlotAtIndex(0, 0)));
        Assert.NotEmpty(vm.NameEncodingError);
        vm.OriginalTrainerName = "赤红";
        app.ViewModel.PartyViewer!.SetSlotCommand.Execute(app.ViewModel.PartyViewer.Slots[0]);
        Assert.Equal("皮卡丘", app.ViewModel.PartyViewer.Slots[0].Nickname);
        Assert.Equal("赤红", Gen12ChineseNames.Decode(save.GetPartySlotAtIndex(0).OriginalTrainerTrash));
    }

    [AvaloniaFact]
    public void SettingsPickerPersistsExplicitChoiceOnlyOnSave()
    {
        var settings = new AppSettings();
        var store = new FakeSettingsStore();
        var vm = new SettingsViewModel(settings, store, Mock.Of<IThemeService>(), Mock.Of<IUiDensityService>(), new LanguageService(), UpdateTestDoubles.Coordinator());
        var view = new SettingsView { DataContext = vm };
        var window = new Window { Content = view, Width = 620, Height = 600 };
        window.Show();
        try
        {
            var picker = view.FindControl<ComboBox>("Gen1NameEncodingPicker")!;
            Assert.Equal(2, picker.SelectedIndex);
            picker.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, vm.Gen1NameEncoding);
            Assert.Equal(Gen12NameEncoding.SimplifiedChinese, settings.Gen12NameEncoding);
            vm.SaveCommand.Execute(null);
            var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
            Assert.Equal(Gen12NameEncoding.Official, roundTrip.Gen1NameEncoding);
            Assert.Equal(Gen12NameEncoding.SimplifiedChinese, roundTrip.Gen2NameEncoding);
            Assert.Same(settings, store.Saved);
        }
        finally { window.Close(); }
    }
}
