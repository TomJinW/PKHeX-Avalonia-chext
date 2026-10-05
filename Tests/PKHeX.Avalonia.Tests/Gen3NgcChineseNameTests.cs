using System.Text.Json;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen3NgcChineseNameTests
{
    [Theory]
    [InlineData("General/Ribbons/Legal (Missing Winning Ribbon).ck3", true)]
    [InlineData("../Illegal/Misc/UltraBallTeddiursa.xk3", true)]
    [InlineData("Generation 3/Misc/250 - HO-OH - 2241C625EB05 Italian longOT.ck3", false)]
    [InlineData("Generation 3/XD Fateful Gifts/213 - SHUCKLE - 1904411103A3 NoFateful.xk3", false)]
    public void ChineseNicknamesOnlyRelaxCharacterFinding(string fixture, bool relax)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Tests/PKHeX.Core.Tests/Legality/Legal"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var pk = Assert.IsAssignableFrom<PKM>(FileUtil.GetSupportedFile(Path.Combine(dir.FullName, "Tests/PKHeX.Core.Tests/Legality/Legal", fixture)));
        SaveFile save = pk is CK3 ? new SAV3Colosseum(new byte[0x60000], decrypt: false) : new SAV3XD();
        pk.Nickname = "测试中文";
        Assert.True(pk.IsNicknamed);
        Assert.Equal("测试中文", pk.Nickname);
        Assert.Equal(new byte[] { 0x6D, 0x4B }, pk.NicknameTrash[..2].ToArray()); // UTF-16 BE
        var before = pk.Data.ToArray();
        var official = Check(false);
        Assert.Contains(official.Results, r => r.Result == LegalityCheckResultCode.NickInvalidChar);
        var compatible = Check(true);
        if (!relax)
        {
            Assert.Equal(official.Results, compatible.Results);
            Assert.Equal(before, pk.Data.ToArray());
            return;
        }
        Assert.DoesNotContain(compatible.Results, r => r.Result == LegalityCheckResultCode.NickInvalidChar);
        Assert.Equal(official.Results.Where(r => r.Result != LegalityCheckResultCode.NickInvalidChar), compatible.Results);
        Assert.Equal(before, pk.Data.ToArray());
        Assert.Equal(official.Results, SaveLegalityCheck.Evaluate(pk, new SAV3E(), Gen12NameEncoding.Official, gen3NgcChineseNames: true).Results);
        pk.Move1 = 999;
        Assert.False(Check(true).Valid);
        pk.Language = (int)LanguageID.Japanese;
        pk.Nickname = "一二三四五六七";
        Assert.Equal(Check(false).Results, Check(true).Results);
        ((IGCRegion)pk).CurrentRegion = GCRegion.NoRegion;
        pk.Nickname = "中\u0001";
        Assert.Contains('\u0001', pk.Nickname);
        Assert.Equal(Check(false).Results, Check(true).Results);
        SaveLegalityResult Check(bool enabled) => SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official, gen3NgcChineseNames: enabled);
    }

    [Fact]
    public void DefaultAndPersistenceAreIndependentOfGba()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}")!;
        Assert.True(settings.Gen3NgcChineseNamesEnabled);
        settings.Gen3NgcChineseNamesEnabled = false;
        settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        Assert.False(settings.Gen3NgcChineseNamesEnabled);
        Assert.True(settings.Gen3ChineseNamesEnabled);
    }
}
