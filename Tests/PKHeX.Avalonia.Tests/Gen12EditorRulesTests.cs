using System.Text.Json;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen12EditorRulesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DvsAndStatExperienceRoundTrip(int gen)
    {
        SaveFile save = gen == 1 ? new SAV1() : new SAV2();
        PKM pk = gen == 1 ? new PK1() : new PK2();
        pk.Species = 25;
        pk.CurrentLevel = 20;
        var (vm, _, _) = TestHelpers.CreateTestViewModel(pk, save);
        vm.IvATK = 15; vm.IvDEF = 14; vm.IvSPE = 13; vm.IvSPA = 12;
        Assert.Equal(10, vm.IvHP);
        Assert.Equal(12, vm.IvSPD);
        vm.IvHP = 0; vm.IvSPD = 1;
        Assert.Equal(10, vm.IvHP);
        Assert.Equal(12, vm.IvSPD);
        vm.EvHP = 65535; vm.EvATK = 65534; vm.EvDEF = 12345; vm.EvSPA = 54321; vm.EvSPE = 32768;
        Assert.Equal(54321, vm.EvSPD);
        vm.EvSPD = 1;
        Assert.Equal(54321, vm.EvSPD);
        var result = vm.PreparePKM();
        save.SetPartySlotAtIndex(result, 0);
        var bytes = save.Write().ToArray();
        SaveFile reload = gen == 1 ? new SAV1(bytes, LanguageID.English, save.Version) : new SAV2(bytes, LanguageID.English, save.Version);
        var actual = reload.GetPartySlotAtIndex(0);
        Assert.Equal(65535, actual.EV_HP);
        Assert.Equal(65534, actual.EV_ATK);
        Assert.Equal(12345, actual.EV_DEF);
        Assert.Equal(54321, actual.EV_SPA);
        Assert.Equal(54321, actual.EV_SPD);
        Assert.Equal(32768, actual.EV_SPE);
        Assert.Equal(10, actual.IV_HP);
        vm.SetMaxIVsCommand.Execute(null);
        Assert.Equal(15, vm.IvATK);
        Assert.Equal(15, vm.IvHP);
        Assert.Equal(15, vm.PreparePKM().IV_SPD);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(1)]
    public void GenderUsesAttackDvAndKeepsOtherDvs(int species)
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK2 { Species = (ushort)species, CurrentLevel = 20, IV_ATK = 15, IV_DEF = 7, IV_SPA = 9, IV_SPE = 11 }, new SAV2());
        vm.Gender = 1;
        Assert.Equal(1, vm.PreparePKM().Gender);
        Assert.Equal(7, vm.IvDEF); Assert.Equal(9, vm.IvSPA); Assert.Equal(11, vm.IvSPE);
        vm.Gender = 0;
        Assert.Equal(0, vm.PreparePKM().Gender);
        vm.IvATK = 0;
        Assert.Equal(1, vm.Gender);
        vm.IsShiny = true;
        Assert.True(vm.PreparePKM().IsShiny);
        vm.IvDEF = 0;
        Assert.False(vm.IsShiny);
        Assert.False(vm.PreparePKM().IsShiny);
    }

    [Theory]
    [InlineData(81, 2)]
    [InlineData(29, 1)]
    [InlineData(32, 0)]
    public void FixedGenderCannotBeChanged(int species, int gender)
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK2 { Species = (ushort)species, CurrentLevel = 20, IV_ATK = 9 }, new SAV2());
        vm.Gender = gender == 0 ? 1 : 0;
        Assert.Equal(gender, vm.Gender);
        Assert.Equal(9, vm.IvATK);
    }

    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void ModernStatsKeepIndependentFieldsAndOriginalLimits(int generation)
    {
        SaveFile save = generation switch
        {
            3 => new SAV3E(), 4 => new SAV4DP(), 5 => new SAV5BW(), 6 => new SAV6XY(),
            7 => new SAV7SM(), 8 => new SAV8SWSH(), _ => new SAV9SV(),
        };
        var original = save.BlankPKM;
        original.Species = 25;
        var (vm, _, _) = TestHelpers.CreateTestViewModel(original, save);
        Assert.False(vm.UsesLegacyStats);
        Assert.True(vm.HasModernFields);
        Assert.Equal(31, vm.MaximumIV); Assert.Equal(252, vm.MaximumEV);
        vm.IvHP = 19; vm.IvSPA = 12; vm.IvSPD = 24;
        vm.EvSPA = 252; vm.EvSPD = 100;
        var pk = vm.PreparePKM();
        Assert.Equal(19, pk.IV_HP); Assert.Equal(12, pk.IV_SPA); Assert.Equal(24, pk.IV_SPD);
        Assert.Equal(252, pk.EV_SPA); Assert.Equal(100, pk.EV_SPD);
    }

    [Theory]
    [InlineData("en", "Exeggutor")]
    [InlineData("de", "Kokowei")]
    [InlineData("zh-Hans", "椰蛋树")]
    public void ResetNicknameUsesCurrentAppDataLanguage(string language, string expected)
    {
        var previous = GameInfo.CurrentLanguage;
        var previousStrings = GameInfo.Strings;
        try
        {
            GameInfo.CurrentLanguage = language;
            GameInfo.Strings = GameInfo.GetStrings(language);
            var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK2 { Species = 103, CurrentLevel = 20 }, new SAV2());
            vm.Nickname = "ABC";
            vm.ResetNicknameCommand.Execute(null);
            Assert.Equal(expected, vm.Nickname);
            Assert.Equal(expected, Gen12ChineseNames.Decode(vm.PreparePKM().NicknameTrash));
        }
        finally
        {
            GameInfo.CurrentLanguage = previous;
            GameInfo.Strings = previousStrings;
        }
    }

    [Fact]
    public void UnownFormUpdatesDvsAndSurvivesPreparing()
    {
        var (vm, _, _) = TestHelpers.CreateTestViewModel(new PK2 { Species = 201, CurrentLevel = 20 }, new SAV2());
        vm.Form = 12;
        Assert.Equal(12, vm.PreparePKM().Form);
        Assert.Equal(vm.IvHP, vm.PreparePKM().IV_HP);
    }

    [Fact]
    public void UpdatesDefaultOffAndExplicitOptInSurvives()
    {
        Assert.False(new AppSettings().Startup.CheckForUpdatesOnStartup);
        Assert.False(JsonSerializer.Deserialize<AppSettings>("{}")!.Startup.CheckForUpdatesOnStartup);
        var settings = new AppSettings();
        settings.Startup.CheckForUpdatesOnStartup = true;
        Assert.True(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!.Startup.CheckForUpdatesOnStartup);
    }
}
