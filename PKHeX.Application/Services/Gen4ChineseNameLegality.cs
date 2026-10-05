using System.Buffers.Binary;
using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>Recognizes names decoded by the selected DS translation table, without changing Core or save bytes.</summary>
internal static class Gen4ChineseNameLegality
{
    public static bool CanRelax(LegalityAnalysis analysis, PKM pk, Gen4NameEncoding encoding, bool nickname)
    {
        var enc = analysis.EncounterOriginal;
        if (pk.IsEgg || enc is IFixedTrainer or IFixedNickname or MysteryGift)
            return false;
        var raw = nickname ? pk.NicknameTrash : pk.OriginalTrainerTrash;
        var name = Gen4ChineseNames.Decode(raw, encoding);
        int max = nickname ? Legal.GetMaxLengthNickname(enc.Generation, (LanguageID)pk.Language)
            : Legal.GetMaxLengthOT(enc.Generation, (LanguageID)pk.Language);
        if (!StringConverter.HasEastAsianScriptCharacters(name) || name.Length > max
            || (name.Length + 1) * 2 > raw.Length
            || BinaryPrimitives.ReadUInt16LittleEndian(raw[(name.Length * 2)..]) != 0xFFFF)
            return false;
        foreach (char c in name)
            if (char.IsControl(c) || char.IsSurrogate(c) || c is '\uFFFD' or '\uFFFF')
                return false;
        // Official decoding can return early; validate the checks it would skip.
        return !ParseSettings.Settings.WordFilter.IsEnabled(pk.Format)
            || !(WordFilter.IsFiltered(name, pk.Context, enc.Context, out _, out _)
                || TrainerNameVerifier.ContainsTooManyNumbers(name, enc.Generation));
    }

    public static bool IsNameFinding(CheckResult result, bool nickname, bool trainer)
        => nickname && result.Identifier == CheckIdentifier.Nickname
            && result.Result is LegalityCheckResultCode.NickInvalidChar or LegalityCheckResultCode.NickMatchLanguageFail
        || trainer && result.Identifier == CheckIdentifier.Trainer
            && result.Result == LegalityCheckResultCode.WordFilterInvalidCharacter_0 && result.Value == 0xFFFF;
}
