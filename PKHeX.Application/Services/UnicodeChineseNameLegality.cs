using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>Optional name-only compatibility for translated NGC/Gen 5/6 games; no transcoding.</summary>
internal static class UnicodeChineseNameLegality
{
    public static bool Applies(PKM pk, SaveFile save, bool gen5, bool gen6, bool ngc = false)
        => (gen5 && save.Generation == 5 && pk is PK5)
            || (gen6 && save.Generation == 6 && pk is PK6)
            || (ngc && pk.IsNicknamed && ((save is SAV3Colosseum && pk is CK3) || (save is SAV3XD && pk is XK3)));

    public static bool CanRelaxNickname(LegalityAnalysis analysis, PKM pk)
    {
        var enc = analysis.EncounterOriginal;
        // A translation table alone cannot establish fixed gift/trade or egg names.
        if (pk.IsEgg || enc is IFixedTrainer or IFixedNickname or MysteryGift)
            return false;
        var name = pk.Nickname;
        if (!StringConverter.HasEastAsianScriptCharacters(name)
            || name.Length > Legal.GetMaxLengthNickname(enc.Generation, (LanguageID)pk.Language))
            return false;
        foreach (var c in name)
            if (char.IsControl(c) || char.IsSurrogate(c) || c is '\uFFFD' or '\uFFFF')
                return false;
        // Core returns early for Chinese glyphs, before length/word checks; do not
        // make those otherwise-invalid names pass by removing that first finding.
        if (ParseSettings.Settings.WordFilter.IsEnabled(pk.Format) && pk.IsNicknamed
            && (WordFilter.IsFiltered(name, pk.Context, enc.Context, out _, out _)
                || TrainerNameVerifier.ContainsTooManyNumbers(name, enc.Generation)))
            return false;
        return true;
    }

    public static bool IsNameFinding(CheckResult result, PKM pk)
        => result.Identifier == CheckIdentifier.Nickname && (pk.IsNicknamed
            ? result.Result == LegalityCheckResultCode.NickInvalidChar
            : result.Result == LegalityCheckResultCode.NickMatchLanguageFail);
}
