using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>A save-scoped verdict and report; Core's analysis and the entity remain unchanged.</summary>
public sealed record SaveLegalityResult(bool Valid, string ReportText, IReadOnlyList<CheckResult> Results);

public static class SaveLegalityCheck
{
    /// <summary>Retains the original slot-viewer Core call outside the Gen 1/2 override.</summary>
    public static bool IsSlotValid(PKM pk, SaveFile save, Gen12NameEncoding encoding, bool ignoreGen3NameTrashErrors = false, bool gen5ChineseNames = false, bool gen6ChineseNames = false, Gen4NameEncoding gen4Encoding = Gen4NameEncoding.Official, bool gen3NgcChineseNames = false)
        => (save.Generation is 1 or 2 && Gen12ChineseNames.Applies(pk, encoding))
            || (ignoreGen3NameTrashErrors && save.Generation == 3 && pk is PK3)
            || Gen4ChineseNames.Applies(pk, save, gen4Encoding)
            || UnicodeChineseNameLegality.Applies(pk, save, gen5ChineseNames, gen6ChineseNames, gen3NgcChineseNames)
            ? Evaluate(pk, save, encoding, ignoreGen3NameTrashErrors, gen5ChineseNames, gen6ChineseNames, gen4Encoding, gen3NgcChineseNames).Valid
            : new LegalityAnalysis(pk).Valid;

    public static SaveLegalityResult Evaluate(PKM pk, SaveFile save, Gen12NameEncoding encoding, bool ignoreGen3NameTrashErrors = false, bool gen5ChineseNames = false, bool gen6ChineseNames = false, Gen4NameEncoding gen4Encoding = Gen4NameEncoding.Official, bool gen3NgcChineseNames = false)
    {
        var analysis = new LegalityAnalysis(pk, save.Personal);
        bool chineseGb = save.Generation is 1 or 2 && Gen12ChineseNames.Applies(pk, encoding);
        bool ignoreGbaTrash = ignoreGen3NameTrashErrors && save.Generation == 3 && pk is PK3;
        bool chineseUnicode = UnicodeChineseNameLegality.Applies(pk, save, gen5ChineseNames, gen6ChineseNames, gen3NgcChineseNames);
        bool chineseDs = Gen4ChineseNames.Applies(pk, save, gen4Encoding);
        if (!analysis.Parsed || (!chineseGb && !ignoreGbaTrash && !chineseUnicode && !chineseDs))
            return Original(analysis);

        bool hasTrainer = chineseGb && Gen12ChineseNames.IsWellFormedChineseName(pk.OriginalTrainerTrash, pk.Japanese, encoding);
        // Eggs and fixed encounter names need game-specific translated labels that this
        // character table does not supply. Never infer their nickname legality from decoding alone.
        bool hasNickname = chineseGb && !pk.IsEgg && analysis.EncounterOriginal is not IFixedNickname { IsFixedNickname: true }
            && Gen12ChineseNames.IsWellFormedChineseName(pk.NicknameTrash, pk.Japanese, encoding);
        bool allowUnicodeName = chineseUnicode && UnicodeChineseNameLegality.CanRelaxNickname(analysis, pk);
        bool dsNickname = chineseDs && Gen4ChineseNameLegality.CanRelax(analysis, pk, gen4Encoding, true);
        bool dsTrainer = chineseDs && Gen4ChineseNameLegality.CanRelax(analysis, pk, gen4Encoding, false);
        var results = analysis.Results.Where(result => !IsFalseEmptyName(result, hasTrainer, hasNickname)
            && !Gen4ChineseNameLegality.IsNameFinding(result, dsNickname, dsTrainer)
            && !(allowUnicodeName && UnicodeChineseNameLegality.IsNameFinding(result, pk))
            && !(ignoreGbaTrash && result.Result is LegalityCheckResultCode.TrashBytesMissingTerminatorFinal or LegalityCheckResultCode.TrashBytesMismatchInitial)).ToArray();
        if (results.Length == analysis.Results.Count)
            return Original(analysis);

        // Only explicitly selected findings are removed. In particular, do not rerun
        // encounter matching with dummy English names or discard any move/encounter checks.
        bool valid = results.All(result => result.Valid)
            && MoveResult.AllValid(analysis.Info.Moves) && MoveResult.AllValid(analysis.Info.Relearn);
        var localized = LegalityLocalizationContext.Create(analysis);
        if (valid)
            return new(true, localized.Settings.Lines.Legal, results);

        var lines = new List<string>();
        LegalityFormatting.AddMoves(localized, analysis.Info.Moves, lines, pk.Context, false);
        LegalityFormatting.AddSecondaryChecksInvalid(localized, results, lines);
        return new(false, string.Join(Environment.NewLine, lines), results);
    }

    private static bool IsFalseEmptyName(CheckResult result, bool hasTrainer, bool hasNickname)
        => result is { Identifier: CheckIdentifier.Trainer, Result: LegalityCheckResultCode.OTShort } && hasTrainer
        || result is { Identifier: CheckIdentifier.Nickname, Result: LegalityCheckResultCode.NickLengthShort } && hasNickname;

    private static SaveLegalityResult Original(LegalityAnalysis analysis)
        => new(analysis.Valid, analysis.Report(), analysis.Results);
}
