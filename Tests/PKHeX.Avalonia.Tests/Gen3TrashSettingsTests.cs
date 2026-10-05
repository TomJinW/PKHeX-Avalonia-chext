using System.Text.Json;
using PKHeX.Core;
using PKHeX.Avalonia.Tests.Fixtures;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen3TrashSettingsTests
{
    [Fact]
    public void DefaultIsOffAndExplicitChoicePersists()
    {
        Assert.False(new AppSettings().IgnoreGen3NameTrashErrors);
        Assert.False(JsonSerializer.Deserialize<AppSettings>("{}")!.IgnoreGen3NameTrashErrors);
        var settings = new AppSettings { IgnoreGen3NameTrashErrors = true };
        Assert.True(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!.IgnoreGen3NameTrashErrors);
    }

    [Fact]
    public void OnlyTheTwoGen3FindingsAreRemovedWithoutChangingBytes()
    {
        var save = Assert.IsAssignableFrom<SAV3>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, "gen3_emerald.sav")));
        var pk = (PK3)save.GetPartySlotAtIndex(0);
        pk.Species = 255;
        pk.Language = (int)LanguageID.English;
        byte[] nick = [0x05, 0x7D, 0x10, 0x9A, 0x05, 0x91, 0xFF, 0x08, 0x01, 0x00];
        byte[] ot = [0x08, 0x64, 0x03, 0x09, 0xFF, 0x0F, 0x81];
        nick.CopyTo(pk.NicknameTrash);
        ot.CopyTo(pk.OriginalTrainerTrash);
        var before = pk.Data.ToArray();
        var original = SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official);
        Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.TrashBytesMissingTerminatorFinal);
        Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.TrashBytesMismatchInitial);
        var filtered = SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, true);
        Assert.Equal(original.Results.Where(r => r.Result is not (LegalityCheckResultCode.TrashBytesMissingTerminatorFinal or LegalityCheckResultCode.TrashBytesMismatchInitial)), filtered.Results);
        Assert.Equal(before, pk.Data.ToArray());
        var mismatch = SaveLegalityCheck.Evaluate(pk, new SAV9SV(), Gen12NameEncoding.Official, true);
        var mismatchDefault = SaveLegalityCheck.Evaluate(pk, new SAV9SV(), Gen12NameEncoding.Official);
        Assert.Equal(mismatchDefault.ReportText, mismatch.ReportText);
        pk.Move1 = 999;
        Assert.False(SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, true).Valid);

        var settings = new AppSettings();
        var vm = TestHelpers.CreateTestViewModel(pk, save, settings: settings).VM;
        settings.IgnoreGen3NameTrashErrors = true;
        vm.RefreshNameEncoding();
        Assert.Equal(SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, true).ReportText, vm.LegalityReport);
    }
}
