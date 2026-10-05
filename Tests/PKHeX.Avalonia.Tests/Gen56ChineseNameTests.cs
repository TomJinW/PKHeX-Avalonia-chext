using System.Text.Json;
using PKHeX.Core;
using PKHeX.Avalonia.Tests.Fixtures;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen56ChineseNameTests
{
    [Theory]
    [InlineData("gen5_black.sav")]
    [InlineData("gen6_x.main")]
    public void ChineseCompatibilityOnlyRelaxesNameFindings(string filename)
    {
        var save = Assert.IsAssignableFrom<SaveFile>(SaveFileFixture.LoadSave(Path.Combine(SaveFileFixture.FindSaveFilesPath()!, filename)));
        var pk = save.GetPartySlotAtIndex(0);
        pk.Nickname = "测试中文";
        pk.IsNicknamed = true;
        var bytes = pk.Data.ToArray();
        var original = Check(false, false);
        Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.NickInvalidChar);
        var compatible = Check(save.Generation == 5, save.Generation == 6);
        Assert.DoesNotContain(compatible.Results, r => r.Result == LegalityCheckResultCode.NickInvalidChar);
        Assert.Equal(original.Results.Where(r => r.Result != LegalityCheckResultCode.NickInvalidChar), compatible.Results);
        Assert.Equal(bytes, pk.Data.ToArray());
        Assert.Equal(original.ReportText, Check(save.Generation != 5, save.Generation != 6).ReportText);

        pk.IsNicknamed = false;
        original = Check(false, false);
        Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.NickMatchLanguageFail);
        compatible = Check(true, true);
        Assert.DoesNotContain(compatible.Results, r => r.Result == LegalityCheckResultCode.NickMatchLanguageFail);
        pk.Nickname = "WRONG";
        Assert.Equal(Check(false, false).ReportText, Check(true, true).ReportText);
        pk.Nickname = "测试中文";
        pk.IsEgg = true;
        Assert.Equal(Check(false, false).ReportText, Check(true, true).ReportText);
        pk.IsEgg = false;
        pk.IsNicknamed = true;
        pk.Move1 = 999;
        Assert.False(Check(true, true).Valid);
        pk.Language = (int)LanguageID.Japanese;
        pk.Nickname = "一二三四五六七八";
        Assert.Equal(Check(false, false).ReportText, Check(true, true).ReportText);

        SaveLegalityResult Check(bool five, bool six) => SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, false, five, six);
    }

    [Fact]
    public void DefaultsAreChineseAndChoicesPersistIndependently()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}")!;
        Assert.True(settings.Gen5ChineseNamesEnabled);
        Assert.True(settings.Gen6ChineseNamesEnabled);
        settings.Gen6ChineseNamesEnabled = false;
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        Assert.True(restored.Gen5ChineseNamesEnabled);
        Assert.False(restored.Gen6ChineseNamesEnabled);
    }
}
