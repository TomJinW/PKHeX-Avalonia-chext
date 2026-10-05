using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using PKHeX.Avalonia.Tests.Fixtures;

namespace PKHeX.Avalonia.Tests;

public sealed class TranslatedNameEntryPointTests
{
    [Fact]
    public async Task BatchViewModelWritesAndUndoesSelectedEncoding()
    {
        var save = new SAV2();
        var settings = new AppSettings { Gen2NameEncoding = Gen12NameEncoding.KoreanChinese };
        var pk = new PK2 { Species = 25, CurrentLevel = 5 };
        Gen12ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash, encoding: settings.Gen2NameEncoding);
        save.SetBoxSlotAtIndex(pk, 0, 0);
        var original = save.GetBoxSlotAtIndex(0, 0).NicknameTrash.ToArray();
        using var vm = new BatchEditorViewModel(save, Moq.Mock.Of<IDialogService>(), settings: settings);
        vm.Instructions = ".Nickname=卡丘\n.OriginalTrainerName=小智";
        for (int i = 0; i < 100 && vm.AffectedCount != 1; i++) await Task.Delay(20);
        Assert.Equal(1, vm.AffectedCount);
        await vm.RunBatchCommand.ExecuteAsync(null);
        Assert.Equal("卡丘", Gen12ChineseNames.Nickname(save.GetBoxSlotAtIndex(0, 0), settings.Gen2NameEncoding));
        vm.ResetBatchCommand.Execute(null);
        Assert.Equal(original, save.GetBoxSlotAtIndex(0, 0).NicknameTrash.ToArray());
        vm.Instructions = ".Nickname=😀";
        for (int i = 0; i < 100 && vm.AffectedCount != 0; i++) await Task.Delay(20);
        Assert.Equal(0, vm.AffectedCount);
        Assert.Equal(original, save.GetBoxSlotAtIndex(0, 0).NicknameTrash.ToArray());
    }

    [Theory]
    [InlineData(1, Gen12NameEncoding.CknGb2312)]
    [InlineData(1, Gen12NameEncoding.SimplifiedChinese)]
    [InlineData(2, Gen12NameEncoding.SimplifiedChinese)]
    [InlineData(2, Gen12NameEncoding.KoreanChinese)]
    public void PlayerNameRoundTripAndInvalidEdit(int generation, Gen12NameEncoding encoding)
    {
        SaveFile save = generation == 1 ? new SAV1() : new SAV2(encoding == Gen12NameEncoding.KoreanChinese ? LanguageID.Korean : LanguageID.English, GameVersion.GS);
        var settings = new AppSettings { Gen1NameEncoding = encoding, Gen2NameEncoding = encoding };
        Assert.True(TranslatedSaveNames.TrySetTrainer(save, settings, "小智"));
        var vm = new TrainerEditorViewModel(save, settings: settings);
        Assert.Equal("小智", vm.TrainerName);
        var original = TranslatedSaveNames.TrainerBytes(save).ToArray();
        vm.SaveCommand.Execute(null);
        Assert.Equal(original, TranslatedSaveNames.TrainerBytes(save).ToArray());
        vm.TrainerName = "赤红";
        Assert.True(vm.IsTrainerNameEncodingValid);
        vm.SaveCommand.Execute(null);
        Assert.Equal("赤红", TranslatedSaveNames.TrainerName(save, settings));
        vm.TrainerName = "😀";
        Assert.False(vm.IsTrainerNameEncodingValid);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Equal("赤红", TranslatedSaveNames.TrainerName(save, settings));
    }

    [Theory]
    [InlineData("gen4_diamond.sav")]
    [InlineData("gen4_platinum.sav")]
    [InlineData("gen4_heartgold.sav")]
    [InlineData("gen2")]
    [InlineData("gen2-korean")]
    public void BoxEditingSwapAndCancelPreserveRawNames(string fixture)
    {
        SaveFile save = fixture.StartsWith("gen2") ? new SAV2(fixture.EndsWith("korean") ? LanguageID.Korean : LanguageID.English, GameVersion.GS)
            : Assert.IsAssignableFrom<SaveFile>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, fixture)));
        var settings = new AppSettings { Gen2NameEncoding = fixture.EndsWith("korean") ? Gen12NameEncoding.KoreanChinese : Gen12NameEncoding.SimplifiedChinese };
        Assert.True(TranslatedSaveNames.TrySetBoxName(save, 0, "皮卡丘", settings));
        Assert.True(TranslatedSaveNames.TrySetBoxName(save, 1, "小智", settings));
        var raw = TranslatedSaveNames.BoxBytes(save, 0);
        raw[^1] = 0x42; // Trailing data must survive no-op and swapping.
        TranslatedSaveNames.SetBoxBytes(save, 0, raw);
        var originalData = save.Data.ToArray();
        var edited = save.State.Edited;
        var vm = new BoxLayoutEditorViewModel(save, settings);
        Assert.Equal("皮卡丘", vm.Boxes[0].Name);
        Assert.Equal(originalData, save.Data.ToArray());
        Assert.Equal(edited, save.State.Edited);
        vm.Boxes[0].Name = "卡丘";
        vm.CancelCommand.Execute(null);
        Assert.Equal(raw, TranslatedSaveNames.BoxBytes(save, 0));
        vm = new BoxLayoutEditorViewModel(save, settings);
        vm.MoveDownCommand.Execute(vm.Boxes[0]);
        vm.SaveCommand.Execute(null);
        Assert.Equal(raw, TranslatedSaveNames.BoxBytes(save, 1));
        Assert.Equal("小智", TranslatedSaveNames.BoxName(save, 0, settings));
        vm = new BoxLayoutEditorViewModel(save, settings);
        vm.Boxes[0].Name = "😀";
        Assert.True(vm.HasNameError);
        Assert.False(vm.SaveCommand.CanExecute(null));
        vm.Boxes[0].Name = "卡丘";
        Assert.False(vm.HasNameError);
        vm.SaveCommand.Execute(null);
        Assert.Equal("卡丘", TranslatedSaveNames.BoxName(save, 0, settings));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void BatchNamesFiltersOrderAndFailureAreAtomic(int generation)
    {
        SaveFile save = generation switch { 1 => new SAV1(), 2 => new SAV2(), 3 => new SAV3E(), _ => new SAV4HGSS() };
        var pk = save.BlankPKM;
        pk.Species = 25;
        pk.Language = (int)LanguageID.English;
        pk.RefreshChecksum();
        var settings = new AppSettings();
        var processor = new EntityBatchProcessor();
        Assert.True(TranslatedBatchNames.Process(processor, pk, save, settings,
            new StringInstructionSet(".Nickname=皮卡丘\n.OriginalTrainerName=小智\n.CurrentLevel=20")));
        Assert.Equal("皮卡丘", Gen4ChineseNames.Nickname(pk, save, settings));
        Assert.Equal(20, pk.CurrentLevel);
        Assert.True(TranslatedBatchNames.Process(processor, pk, save, settings,
            new StringInstructionSet("=Nickname=皮卡丘\n.Nickname=卡丘\n.OriginalTrainerName=*Nickname")));
        var bytes = pk.Data.ToArray();
        var nick = pk.NicknameTrash.ToArray();
        var ot = pk.OriginalTrainerTrash.ToArray();
        Assert.False(TranslatedBatchNames.Process(processor, pk, save, settings,
            new StringInstructionSet("=Nickname=错误\n.Nickname=皮卡丘")));
        Assert.Throws<TranslatedBatchNameException>(() => TranslatedBatchNames.Process(processor, pk, save, settings,
            new StringInstructionSet(".CurrentLevel=50\n.Nickname=😀")));
        Assert.Equal(bytes, pk.Data.ToArray());
        Assert.Equal(nick, pk.NicknameTrash.ToArray());
        Assert.Equal(ot, pk.OriginalTrainerTrash.ToArray());
    }
}
