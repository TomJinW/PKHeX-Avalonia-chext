using System.Text.Json;
using PKHeX.Core;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen4ChineseNameTests
{
    [Theory]
    [InlineData(Gen4NameEncoding.Xzonn)]
    [InlineData(Gen4NameEncoding.YYJOY)]
    public void LegalityUsesSelectedTableWithoutChangingOtherFindings(Gen4NameEncoding encoding)
    {
        var save = Assert.IsAssignableFrom<SAV4>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen4_heartgold.sav")));
        var pk = save.GetPartySlotAtIndex(0);
        pk.Language = 1;
        pk.IsNicknamed = false;
        Assert.True(Gen4ChineseNames.TryEncode("小锯鳄", pk.NicknameTrash, encoding, 5));
        Assert.True(Gen4ChineseNames.TryEncode("雪绢", pk.OriginalTrainerTrash, encoding, 5));
        var bytes = pk.Data.ToArray();
        var original = new LegalityAnalysis(pk, save.Personal);
        Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.NickInvalidChar);
        var result = SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, gen4Encoding: encoding);
        Assert.DoesNotContain(result.Results, r => r.Result is LegalityCheckResultCode.NickInvalidChar or LegalityCheckResultCode.WordFilterInvalidCharacter_0);
        Assert.Equal(original.Results.Where(r => r.Result is not (LegalityCheckResultCode.NickInvalidChar or LegalityCheckResultCode.WordFilterInvalidCharacter_0)), result.Results);
        Assert.Equal(bytes, pk.Data.ToArray());
        Assert.Equal(original.Results, SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official).Results);
        Assert.Equal(original.Results, SaveLegalityCheck.Evaluate(pk, new SAV5B2W2(), Gen12NameEncoding.Official, gen4Encoding: encoding).Results);
        Assert.True(Gen4ChineseNames.TryEncode("小锯鳄小锯鳄", pk.NicknameTrash, encoding, 10));
        Assert.Contains(SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, gen4Encoding: encoding).Results,
            r => r.Result == LegalityCheckResultCode.NickInvalidChar);
        pk.NicknameTrash.Fill(0xFF);
        pk.NicknameTrash[0] = 0xFE;
        Assert.Contains(SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, gen4Encoding: encoding).Results,
            r => r.Result == LegalityCheckResultCode.NickInvalidChar);
    }

    [Theory]
    [InlineData("gen4_diamond.sav")]
    [InlineData("gen4_platinum.sav")]
    [InlineData("gen4_heartgold.sav")]
    public void BoxNameReadsDoNotMutateSave(string filename)
    {
        var save = Assert.IsAssignableFrom<SAV4>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, filename)));
        var before = save.Data.ToArray();
        var edited = save.State.Edited;
        var settings = new AppSettings();
        for (int box = 0; box < save.BoxCount; box++)
        {
            Assert.DoesNotContain('\uFFFF', Gen4ChineseNames.BoxName(save, box, settings));
            settings.Gen4NameEncoding = Gen4NameEncoding.Official;
            Assert.Equal(((IBoxDetailNameRead)save).GetBoxName(box).Replace('\uFFFF', '\uFFFD'), Gen4ChineseNames.BoxName(save, box, settings));
            settings.Gen4NameEncoding = Gen4NameEncoding.Xzonn;
        }
        Assert.Equal(before, save.Data.ToArray());
        Assert.Equal(edited, save.State.Edited);
    }

    [Theory]
    [InlineData(Gen4NameEncoding.YYJOY)]
    [InlineData(Gen4NameEncoding.Xzonn)]
    public void EmptyBuffersAndSentinelsAreSafeForDisplay(Gen4NameEncoding encoding)
    {
        Assert.Equal(string.Empty, Gen4ChineseNames.Decode(new byte[22], encoding));
        Assert.Equal(string.Empty, Gen4ChineseNames.Decode([0xFF, 0xFF], encoding));
        var bytes = new byte[12];
        Assert.True(Gen4ChineseNames.TryEncode("皮", bytes, encoding, 5));
        bytes[2] = bytes[3] = 0;
        Assert.Equal("皮", Gen4ChineseNames.Decode(bytes, encoding));
        // Reserved international entries use U+FFFF internally, never as display text.
        for (ushort code = 1; code <= 0x1EC; code++)
            Assert.DoesNotContain('\uFFFF', Gen4ChineseNames.Decode([(byte)code, (byte)(code >> 8), 0xFF, 0xFF], encoding));
    }

    [Theory]
    [InlineData(Gen4NameEncoding.YYJOY, "180A8A07C20AFFFF")]
    [InlineData(Gen4NameEncoding.Xzonn, "730965030D02FFFF")]
    public void KnownMappingsAndCapacity(Gen4NameEncoding encoding, string expected)
    {
        var bytes = new byte[8];
        Assert.True(Gen4ChineseNames.TryEncode("皮卡丘", bytes, encoding, 3));
        Assert.Equal(expected, Convert.ToHexString(bytes));
        Assert.Equal("皮卡丘", Gen4ChineseNames.Decode(bytes, encoding));
        var original = bytes.ToArray();
        Assert.False(Gen4ChineseNames.TryEncode("皮卡丘皮", bytes, encoding, 3));
        Assert.False(Gen4ChineseNames.TryEncode("😀", bytes, encoding, 3));
        Assert.Equal(original, bytes);
        Assert.Equal("\uFFFD", Gen4ChineseNames.Decode([0x9B, 0x1C, 0xFF, 0xFF], Gen4NameEncoding.Xzonn));
    }

    [Theory]
    [InlineData("gen4_diamond.sav", Gen4NameEncoding.YYJOY)]
    [InlineData("gen4_platinum.sav", Gen4NameEncoding.Xzonn)]
    [InlineData("gen4_heartgold.sav", Gen4NameEncoding.Xzonn)]
    public void EditorAndTrainerRoundTrip(string filename, Gen4NameEncoding encoding)
    {
        var save = Assert.IsAssignableFrom<SAV4>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, filename)));
        var settings = new AppSettings { Gen4NameEncoding = encoding };
        var pk = (PK4)save.GetPartySlotAtIndex(0);
        Assert.True(Gen4ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash, encoding, 5));
        Assert.True(Gen4ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash, encoding, 5));
        var vm = TestHelpers.CreateTestViewModel(pk, save, settings: settings).VM;
        Assert.Equal("皮卡丘", vm.Nickname);
        Assert.Equal("小智", vm.OriginalTrainerName);
        vm.Nickname = "波加曼";
        vm.IsNicknamed = true;
        var result = vm.PreparePKM();
        Assert.True(result.IsNicknamed);
        Assert.Equal("波加曼", Gen4ChineseNames.Decode(result.NicknameTrash, encoding));
        save.SetPartySlotAtIndex(result, 0);
        var trainer = new TrainerEditorViewModel(save, settings: settings);
        trainer.TrainerName = "小光";
        trainer.SaveCommand.Execute(null);
        var written = save.Write();
        SAV4 restored = save switch { SAV4DP => new SAV4DP(written), SAV4Pt => new SAV4Pt(written), _ => new SAV4HGSS(written) };
        Assert.Equal("波加曼", Gen4ChineseNames.Decode(restored.GetPartySlotAtIndex(0).NicknameTrash, encoding));
        Assert.Equal("小光", Gen4ChineseNames.TrainerName(restored, settings));
        Assert.True(restored.ChecksumsValid);
        var before = result.NicknameTrash.ToArray();
        settings.Gen4NameEncoding = Gen4NameEncoding.Official;
        vm.RefreshNameEncoding();
        Assert.Equal(before, vm.PreparePKM().NicknameTrash.ToArray());
    }

    [Fact]
    public void DefaultsAndScope()
    {
        Assert.Equal(Gen4NameEncoding.Xzonn, new AppSettings().Gen4NameEncoding);
        Assert.Equal(Gen4NameEncoding.Xzonn, JsonSerializer.Deserialize<AppSettings>("{}")!.Gen4NameEncoding);
        var settings = new AppSettings { Gen4NameEncoding = Gen4NameEncoding.Official };
        Assert.Equal(Gen4NameEncoding.Official, JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!.Gen4NameEncoding);
        Assert.False(Gen4ChineseNames.Applies(new PK5(), new SAV5B2W2(), Gen4NameEncoding.Xzonn));
        Assert.False(Gen4ChineseNames.Applies(new PK4(), new SAV5B2W2(), Gen4NameEncoding.Xzonn));
        Assert.False(Gen4ChineseNames.Applies(new PK4(), new SAV4DP(), Gen4NameEncoding.Official));
    }
}
