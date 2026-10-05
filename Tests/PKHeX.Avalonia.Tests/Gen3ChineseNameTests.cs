using System.Globalization;
using System.Text.Json;
using Moq;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen3ChineseNameTests
{
    [Fact]
    public void ChineseBoxNamesEditSwapAndExportWithoutTranscoding()
    {
        var save = Assert.IsAssignableFrom<SAV3>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen3_emerald.sav")));
        Assert.True(Gen3ChineseNames.TrySetBoxName(save, 0, "精灵盒子"));
        Assert.True(Gen3ChineseNames.TrySetBoxName(save, 1, "水系"));
        var raw = Gen3ChineseNames.BoxNameBytes(save, 0).ToArray();
        Assert.Equal(0xFF, raw[8]);
        Assert.Equal("精灵盒子", Gen3ChineseNames.BoxName(save, 0, true));
        Assert.Equal(save.GetBoxName(0), Gen3ChineseNames.BoxName(save, 0, false));
        Assert.False(Gen3ChineseNames.TrySetBoxName(save, 0, "一二三四五"));
        Assert.Equal(raw, Gen3ChineseNames.BoxNameBytes(save, 0).ToArray());
        var vm = new BoxLayoutEditorViewModel(save, Settings());
        Assert.Equal("精灵盒子", vm.Boxes[0].Name);
        vm.Boxes[0].Name = "一二三四五";
        Assert.False(vm.SaveCommand.CanExecute(null));
        vm.Boxes[0].Name = "精灵盒子";
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.MoveDownCommand.Execute(vm.Boxes[0]);
        vm.SaveCommand.Execute(null);
        var restored = new SAV3E(save.Write());
        Assert.Equal("水系", Gen3ChineseNames.BoxName(restored, 0, true));
        Assert.Equal(raw, Gen3ChineseNames.BoxNameBytes(restored, 1).ToArray());
        Assert.True(restored.ChecksumsValid);
    }

    [Fact]
    public void TrainerWorkspaceUsesChineseCodecAndPreservesUneditedBytes()
    {
        var save = Assert.IsAssignableFrom<SAV3>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen3_emerald.sav")));
        byte[] original = [0x0F, 0x81, 0x0C, 0x3C, 0xFF, 0x02, 0xE2, 0xFF];
        original.CopyTo(save.SmallBlock.OriginalTrainerTrash);
        var settings = Settings();
        var vm = new TrainerEditorViewModel(save, settings: settings);
        Assert.Equal("佑树", vm.TrainerName);
        vm.SaveCommand.Execute(null);
        Assert.Equal(original, save.SmallBlock.OriginalTrainerTrash.ToArray());
        settings.Gen3ChineseNamesEnabled = false;
        vm.RefreshNameEncoding();
        Assert.Equal(save.OT, vm.TrainerName);
        settings.Gen3ChineseNamesEnabled = true;
        vm.RefreshNameEncoding();
        Assert.Equal("佑树", vm.TrainerName);
        vm.TrainerName = "皮卡丘水";
        Assert.False(vm.SaveCommand.CanExecute(null));
        vm.SaveCommand.Execute(null);
        Assert.Equal(original, save.SmallBlock.OriginalTrainerTrash.ToArray());
        vm.TrainerName = "小智";
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.SaveCommand.Execute(null);
        var reloaded = new SAV3E(save.Write());
        Assert.Equal("小智", Gen3ChineseNames.Decode(reloaded.SmallBlock.OriginalTrainerTrash[..7]));
        Assert.True(reloaded.ChecksumsValid);
    }

    private static AppSettings Settings(bool enabled = true) => new() { Gen3ChineseNamesEnabled = enabled };
    private static PK3 Pokemon() => new() { Species = 25, CurrentLevel = 20, Language = (int)LanguageID.English, Version = GameVersion.E };

    [Fact]
    public void EveryChineseEntryMatchesSuppliedTable()
    {
        using var stream = typeof(Gen3ChineseNames).Assembly.GetManifestResourceStream("PKHeX.Application.Resources.Gen3.PMRSEFRLG_charmap.txt")!;
        using var reader = new StreamReader(stream);
        int count = 0;
        while (reader.ReadLine() is { } line)
        {
            int split = line.IndexOf('=');
            if (split != 4 || line.Length != 6) continue;
            var code = ushort.Parse(line.AsSpan(0, 4), NumberStyles.HexNumber);
            var bytes = new byte[3];
            Assert.True(Gen3ChineseNames.TryEncode(line[5..], bytes));
            Assert.Equal(new byte[] { (byte)(code >> 8), (byte)code, 0xFF }, bytes);
            Assert.Equal(line[5..], Gen3ChineseNames.Decode(bytes));
            count++;
        }
        Assert.Equal(6763, count);
    }

    [Fact]
    public void MixedNamesCapacityAndZeroLowBytesUseGbaRules()
    {
        var bytes = new byte[10];
        Assert.True(Gen3ChineseNames.TryEncode("皮卡丘", bytes));
        Assert.Equal(new byte[] { 0x0A, 0x5F, 0x07, 0xB6, 0x0B, 0x12, 0xFF, 0xFF, 0xFF, 0xFF }, bytes);
        Assert.True(Gen3ChineseNames.TryEncode("啊啊啊啊啊", bytes));
        Assert.Equal("啊啊啊啊啊", Gen3ChineseNames.Decode(bytes));
        Assert.DoesNotContain((byte)0xFF, bytes);
        var ot = new byte[7];
        Assert.True(Gen3ChineseNames.TryEncode("赤红蓝A", ot));
        Assert.Equal("赤红蓝A", Gen3ChineseNames.Decode(ot));
        var before = ot.ToArray();
        Assert.False(Gen3ChineseNames.TryEncode("赤红蓝绿", ot));
        Assert.Equal(before, ot);
        Assert.True(Gen3ChineseNames.TryEncode("ÉéA！", bytes)); // 06 / 1B remain single-byte.
        Assert.Equal("ÉéA！", Gen3ChineseNames.Decode(bytes));
        Assert.False(Gen3ChineseNames.TryEncode("ÀA", bytes)); // Would decode as a Chinese pair.
        Assert.False(Gen3ChineseNames.TryEncode("😀", bytes));
        Assert.False(Gen3ChineseNames.TryEncode("A\nB", bytes));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NamesRoundTripThroughEditorAndGbaSave(int game)
    {
        var filename = game switch { 0 => "gen3_ruby.sav", 1 => "gen3_firered.sav", _ => "gen3_emerald.sav" };
        var save = Assert.IsAssignableFrom<SAV3>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, filename)));
        save.OT = "RED";
        var pk = Pokemon();
        Gen3ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
        Gen3ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        var settings = Settings();
        var vm = TestHelpers.CreateTestViewModel(pk, save, settings: settings).VM;
        Assert.True(vm.UsesChineseNames);
        Assert.Equal("皮卡丘", vm.Nickname);
        Assert.Equal("小智", vm.OriginalTrainerName);
        vm.Nickname = "啊啊啊啊啊";
        vm.OriginalTrainerName = "赤红蓝A";
        var result = vm.PreparePKM();
        Assert.Equal((int)LanguageID.English, result.Language);
        save.SetBoxSlotAtIndex(result, 0, 0);
        save.SetPartySlotAtIndex(result, 0);
        var bytes = save.Write().ToArray();
        SAV3 restored = game switch { 0 => new SAV3RS(bytes), 1 => new SAV3FRLG(bytes), _ => new SAV3E(bytes) };
        foreach (var reloaded in new[] { restored.GetBoxSlotAtIndex(0, 0), restored.GetPartySlotAtIndex(0) })
        {
            Assert.Equal("啊啊啊啊啊", Gen3ChineseNames.Decode(reloaded.NicknameTrash));
            Assert.Equal("赤红蓝A", Gen3ChineseNames.Decode(reloaded.OriginalTrainerTrash));
            Assert.True(reloaded.ChecksumValid);
        }
        var box = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>(), settings: settings);
        var party = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>(), settings: settings);
        box.CurrentBox = 0;
        box.RefreshCurrentBox();
        Assert.Equal("啊啊啊啊啊", box.Slots[0].Nickname);
        Assert.Equal("啊啊啊啊啊", party.Slots[0].Nickname);
        vm.OriginalTrainerName = "赤红蓝绿";
        Assert.False(vm.CheckNameEncoding());
        Assert.Throws<InvalidOperationException>(() => vm.PreparePKM());
    }

    [Fact]
    public void ToggleReinterpretsBytesWithoutTranscodingOrDamagingUnknownTrash()
    {
        var pk = Pokemon();
        Gen3ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
        Gen3ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        pk.NicknameTrash[9] = 0x99;
        var nickname = pk.NicknameTrash.ToArray();
        var ot = pk.OriginalTrainerTrash.ToArray();
        var settings = Settings(false);
        var vm = TestHelpers.CreateTestViewModel(pk, new SAV3E(), settings: settings).VM;
        Assert.Equal(pk.Nickname, vm.Nickname);
        foreach (bool enabled in new[] { true, false, true })
        {
            settings.Gen3ChineseNamesEnabled = enabled;
            vm.RefreshNameEncoding();
            Assert.Equal(enabled ? "皮卡丘" : pk.Nickname, vm.Nickname);
            Assert.Equal(nickname, vm.PreparePKM().NicknameTrash.ToArray());
            Assert.Equal(ot, vm.PreparePKM().OriginalTrainerTrash.ToArray());
        }
        pk.NicknameTrash[0] = 0x1E; pk.NicknameTrash[1] = 0xF6; // unmapped pair
        vm = TestHelpers.CreateTestViewModel(pk, new SAV3E(), settings: settings).VM;
        Assert.Contains('\uFFFD', vm.Nickname);
        Assert.Equal(pk.NicknameTrash.ToArray(), vm.PreparePKM().NicknameTrash.ToArray());
    }

    [Fact]
    public void ChineseEggFlagDoesNotForceJapaneseEncodingOrOverwriteNames()
    {
        var pk = Pokemon();
        Gen3ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
        Gen3ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        var vm = TestHelpers.CreateTestViewModel(pk, new SAV3E(), settings: Settings()).VM;
        vm.IsEgg = true;
        var result = vm.PreparePKM();
        Assert.True(result.IsEgg);
        Assert.Equal((int)LanguageID.English, result.Language);
        Assert.Equal("皮卡丘", Gen3ChineseNames.Decode(result.NicknameTrash));
        Assert.Equal("小智", Gen3ChineseNames.Decode(result.OriginalTrainerTrash));
    }

    [Fact]
    public void OfficialModeStillAppliesCoreEggNaming()
    {
        var pk = Pokemon();
        pk.Nickname = "PIKACHU";
        var expected = (PK3)pk.Clone();
        expected.IsEgg = true;
        var vm = TestHelpers.CreateTestViewModel(pk, new SAV3E(), settings: Settings(false)).VM;
        vm.IsEgg = true;
        Assert.Equal(expected.NicknameTrash.ToArray(), vm.PreparePKM().NicknameTrash.ToArray());
    }

    [Fact]
    public void SettingDefaultsOnAndDoesNotOverrideOtherCodecsOrLegality()
    {
        Assert.True(new AppSettings().Gen3ChineseNamesEnabled);
        Assert.True(JsonSerializer.Deserialize<AppSettings>("{}")!.Gen3ChineseNamesEnabled);
        Assert.False(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Settings(false)))!.Gen3ChineseNamesEnabled);
        var settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Settings()))!;
        Assert.True(settings.Gen3ChineseNamesEnabled);
        Assert.Equal(Gen12NameEncoding.SimplifiedChinese, settings.Gen12NameEncoding);
        Assert.False(Gen3ChineseNames.Applies(new PK1(), new SAV1(), true));
        Assert.False(Gen3ChineseNames.Applies(new PK2(), new SAV2(), true));
        Assert.False(Gen3ChineseNames.Applies(new PK9(), new SAV9SV(), true));
        Assert.False(Gen3ChineseNames.Applies(Pokemon(), new SAV9SV(), true));
        Assert.False(Gen3ChineseNames.Applies(Pokemon(), new SAV3E(true), true));
        var japanese = Pokemon(); japanese.Language = (int)LanguageID.Japanese;
        Assert.False(Gen3ChineseNames.Applies(japanese, new SAV3E(), true));
        var pk = Pokemon();
        Gen3ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash);
        var save = new SAV3E();
        var expected = new LegalityAnalysis(pk, save.Personal);
        Assert.Equal(expected.Report(), SaveLegalityCheck.Evaluate(pk, save, settings.Gen12NameEncoding).ReportText);
    }
}
