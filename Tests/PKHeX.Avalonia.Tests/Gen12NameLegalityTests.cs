using Moq;
using PKHeX.Application.UseCases;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen12NameLegalityTests
{
    private const Gen12NameEncoding Chinese = Gen12NameEncoding.SimplifiedChinese;

    private static PKM ChinesePokemon(int generation, bool japanese = false)
    {
        PKM pk = generation == 1 ? new PK1(japanese) : new PK2(japanese);
        pk.Species = 103;
        pk.CurrentLevel = 30;
        pk.Move1 = 250; // Deliberately invalid: fixing names must not make this Pokémon legal.
        Assert.True(Gen12ChineseNames.TryEncode("你好", pk.NicknameTrash, japanese));
        Assert.True(Gen12ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash, japanese));
        return pk;
    }

    private static PKM LoadLegalFixture(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Tests/PKHeX.Core.Tests/Legality/Legal"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Assert.IsAssignableFrom<PKM>(FileUtil.GetSupportedFile(Path.Combine(dir.FullName,
            "Tests/PKHeX.Core.Tests/Legality/Legal", relativePath)));
    }

    [Theory]
    [InlineData(1, "General/004 - CHARMANDER - D5EF.pk1")]
    [InlineData(2, "Generation 2/025 - PIKACHU - 2129.pk2")]
    public void OtherwiseLegalPokemonBecomesLegalAndSlotFlagsAgree(int generation, string fixture)
    {
        var pk = LoadLegalFixture(fixture);
        SaveFile save = generation == 1 ? new SAV1() : new SAV2();
        Assert.True(new LegalityAnalysis(pk, save.Personal).Valid);
        Gen12ChineseNames.TryEncode("你好", pk.NicknameTrash);
        Gen12ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        pk = pk.Clone();
        Assert.False(new LegalityAnalysis(pk, save.Personal).Valid);
        var result = SaveLegalityCheck.Evaluate(pk, save, Chinese);
        Assert.True(result.Valid, result.ReportText);
        Assert.True(SaveLegalityCheck.IsSlotValid(pk, save, Chinese));
        Assert.Equal(LegalityLocalizationContext.Create(new LegalityAnalysis(pk)).Settings.Lines.Legal, result.ReportText);
        save.SetPartySlotAtIndex(pk, 0);
        save.SetBoxSlotAtIndex(pk, 0, 0);
        Assert.True(new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>()).Slots[0].IsLegal);
        Assert.True(new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>()).Slots[0].IsLegal);
        Assert.All(new LegalityAuditUseCase().Execute(save, nameEncoding: Chinese), entry => Assert.True(entry.Valid, entry.ReportText));
        var settings = new AppSettings();
        var vm = new PokemonEditorViewModel(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>(), settings: settings);
        Assert.True(vm.IsLegal, vm.LegalityReport);
        settings.Gen12NameEncoding = Gen12NameEncoding.Official;
        vm.RefreshNameEncoding();
        Assert.False(vm.IsLegal);
        settings.Gen12NameEncoding = Chinese;
        vm.RefreshNameEncoding();
        Assert.True(vm.IsLegal, vm.LegalityReport);
    }

    [Fact]
    public void ChineseDecodingDoesNotExcuseAnArbitraryEggNickname()
    {
        var pk = LoadLegalFixture("General/235 - 알 - 10DB.pk2");
        Assert.True(pk.IsEgg);
        Gen12ChineseNames.TryEncode("你好", pk.NicknameTrash);
        Gen12ChineseNames.TryEncode("小智", pk.OriginalTrainerTrash);
        var result = SaveLegalityCheck.Evaluate(pk, new SAV2(), Chinese);
        Assert.False(result.Valid);
        Assert.Contains(result.Results, r => r.Result == LegalityCheckResultCode.NickLengthShort);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(1, true)] [InlineData(2, true)]
    public void FixesOnlyFalseEmptyNamesAndPreservesRawDataAndOtherFindings(int generation, bool japanese)
    {
        var pk = ChinesePokemon(generation, japanese);
        var language = japanese ? LanguageID.Japanese : LanguageID.English;
        SaveFile save = generation == 1 ? new SAV1(language) : new SAV2(language);
        var before = pk.Clone();
        var original = new LegalityAnalysis(pk, save.Personal);
        if (!japanese)
        {
            Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.OTShort);
            Assert.Contains(original.Results, r => r.Result == LegalityCheckResultCode.NickLengthShort);
        }
        var adapted = SaveLegalityCheck.Evaluate(pk, save, Chinese);
        Assert.DoesNotContain(adapted.Results, r => r.Result is LegalityCheckResultCode.OTShort or LegalityCheckResultCode.NickLengthShort);
        Assert.Equal(original.Results.Where(r => r.Result is not (LegalityCheckResultCode.OTShort or LegalityCheckResultCode.NickLengthShort)), adapted.Results);
        Assert.False(adapted.Valid);
        var localized = LegalityLocalizationContext.Create(original);
        var moveLines = new List<string>();
        LegalityFormatting.AddMoves(localized, original.Info.Moves, moveLines, pk.Context, false);
        Assert.NotEmpty(moveLines);
        Assert.All(moveLines, line => Assert.Contains(line, adapted.ReportText));
        Assert.True(before.EqualsStored(pk));
        if (!japanese) Assert.True(original.HasResult(LegalityCheckResultCode.OTShort)); // Core analysis stays unchanged.
        else Assert.Equal(original.Report(), adapted.ReportText); // These bytes already decode nonempty in Japanese.
    }

    [Theory]
    [InlineData("50")]
    [InlineData("00")]
    [InlineData("12")]
    [InlineData("1250")]
    [InlineData("2EB850")] // Removed 啰 mapping.
    [InlineData("120D2EFF50")] // Known character followed by unknown pair.
    [InlineData("120D18BD")] // Missing terminator.
    public void InvalidOrEmptyNamesAreNeverExcused(string hex)
    {
        var raw = Convert.FromHexString(hex);
        Assert.False(Gen12ChineseNames.IsWellFormedChineseName(raw));
        var pk = ChinesePokemon(1);
        pk.OriginalTrainerTrash.Fill(0x12); // Unterminated tail, not a manufactured terminator.
        raw.CopyTo(pk.OriginalTrainerTrash);
        pk.NicknameTrash.Fill(0x50);
        var save = new SAV1();
        var original = new LegalityAnalysis(pk, save.Personal);
        var adapted = SaveLegalityCheck.Evaluate(pk, save, Chinese);
        Assert.Equal(original.Results, adapted.Results);
        Assert.Equal(original.Report(), adapted.ReportText);
        Assert.Equal(original.Valid, adapted.Valid);
    }

    [Fact]
    public void OfficialKoreanAndChineseByteCollisionsKeepOfficialChecksWhenSelected()
    {
        var pk = ChinesePokemon(2);
        Assert.True(Gen12ChineseNames.TryEncode("赤红", pk.OriginalTrainerTrash));
        Assert.True(pk.Korean);
        var save = new SAV2(LanguageID.Korean, GameVersion.GS);
        var expected = new LegalityAnalysis(pk, save.Personal);
        var actual = SaveLegalityCheck.Evaluate(pk, save, Gen12NameEncoding.Official);
        Assert.Equal(expected.Report(), actual.ReportText);
        Assert.Equal(expected.Valid, actual.Valid);
        Assert.Equal(expected.Results, actual.Results);
    }

    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void EveryLaterSaveGenerationHasExactlyTheOriginalVerdictAndReport(int generation)
    {
        SaveFile save = generation switch
        {
            3 => new SAV3E(), 4 => new SAV4DP(), 5 => new SAV5BW(), 6 => new SAV6XY(),
            7 => new SAV7SM(), 8 => new SAV8SWSH(), _ => new SAV9SV(),
        };
        var pk = save.BlankPKM;
        pk.Species = 1;
        pk.CurrentLevel = 20;
        pk.OriginalTrainerName = string.Empty;
        var before = pk.Clone();
        foreach (var encoding in new[] { Chinese, Gen12NameEncoding.Official })
        {
            var expected = new LegalityAnalysis(pk, save.Personal);
            var actual = SaveLegalityCheck.Evaluate(pk, save, encoding);
            Assert.Equal(expected.Report(), actual.ReportText);
            Assert.Equal(expected.Valid, actual.Valid);
            Assert.Equal(expected.Results, actual.Results);
            Assert.Equal(new LegalityAnalysis(pk).Valid, SaveLegalityCheck.IsSlotValid(pk, save, encoding));
        }
        Assert.True(before.EqualsStored(pk));
        // Even a legacy entity inspected against a later save must not enable the override.
        var legacy = ChinesePokemon(1);
        var baseline = new LegalityAnalysis(legacy, save.Personal);
        var result = SaveLegalityCheck.Evaluate(legacy, save, Chinese);
        Assert.Equal(baseline.Report(), result.ReportText);
        Assert.Equal(baseline.Valid, result.Valid);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void EditorSlotsAndAuditUseSameVerdictAndSettingsSwitchRefreshesReport(int generation)
    {
        SaveFile save = generation == 1 ? new SAV1() : new SAV2();
        var pk = ChinesePokemon(generation);
        var settings = new AppSettings();
        var vm = new PokemonEditorViewModel(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>(), settings: settings);
        var prepared = vm.PreparePKM();
        var expected = SaveLegalityCheck.Evaluate(prepared, save, Chinese);
        Assert.Equal(expected.Valid, vm.IsLegal);
        Assert.Equal(expected.ReportText, vm.LegalityReport);
        save.SetPartySlotAtIndex(prepared, 0);
        save.SetBoxSlotAtIndex(prepared, 0, 0);
        var party = new PartyViewerViewModel(save, Mock.Of<ISpriteRenderer>(), settings: settings);
        var boxes = new BoxViewerViewModel(save, Mock.Of<ISpriteRenderer>(), settings: settings);
        Assert.Equal(expected.Valid, party.Slots[0].IsLegal);
        Assert.Equal(expected.Valid, boxes.Slots[0].IsLegal);
        var audit = new LegalityAuditUseCase().Execute(save, nameEncoding: Chinese);
        Assert.Equal(2, audit.Count);
        Assert.All(audit, entry => Assert.Equal(expected.ReportText, entry.ReportText));
        var raw = vm.TargetPKM.Clone();
        settings.Gen12NameEncoding = Gen12NameEncoding.Official;
        vm.RefreshNameEncoding();
        Assert.Equal(new LegalityAnalysis(vm.TargetPKM, save.Personal).Report(), vm.LegalityReport);
        Assert.True(raw.EqualsStored(vm.TargetPKM));
        settings.Gen12NameEncoding = Chinese;
        vm.RefreshNameEncoding();
        Assert.Equal(expected.ReportText, vm.LegalityReport);
        Assert.True(raw.EqualsStored(vm.TargetPKM));
    }
}
