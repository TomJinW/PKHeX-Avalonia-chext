using PKHeX.Core;

namespace PKHeX.Application.Services;

public sealed class TranslatedBatchNameException : Exception;

/// <summary>Runs ordinary batch instructions on a clone, intercepting translated name fields.</summary>
public static class TranslatedBatchNames
{
    private static bool IsName(StringInstruction command) => command.PropertyName is nameof(PKM.Nickname) or nameof(PKM.OriginalTrainerName);
    private static bool Applies(PKM pk, SaveFile save, AppSettings settings)
        => Gen12ChineseNames.Applies(pk, settings.GetNameEncoding(save.Generation))
            || Gen3ChineseNames.Applies(pk, save, settings.Gen3ChineseNamesEnabled)
            || Gen4ChineseNames.Applies(pk, save, settings.Gen4NameEncoding);

    private static string Decode(PKM pk, SaveFile save, AppSettings settings, bool nickname)
    {
        var raw = nickname ? pk.NicknameTrash : pk.OriginalTrainerTrash;
        if (Gen4ChineseNames.Applies(pk, save, settings.Gen4NameEncoding)) return Gen4ChineseNames.Decode(raw, settings.Gen4NameEncoding);
        if (Gen3ChineseNames.Applies(pk, save, settings.Gen3ChineseNamesEnabled)) return Gen3ChineseNames.Decode(raw);
        return Gen12ChineseNames.Decode(raw, pk.Japanese, settings.GetNameEncoding(save.Generation));
    }

    private static bool Encode(PKM pk, SaveFile save, AppSettings settings, bool nickname, string value)
    {
        var raw = nickname ? pk.NicknameTrash : pk.OriginalTrainerTrash;
        if (value == Decode(pk, save, settings, nickname)) return true;
        if (Gen4ChineseNames.Applies(pk, save, settings.Gen4NameEncoding))
            return Gen4ChineseNames.TryEncode(value, raw, settings.Gen4NameEncoding, nickname
                ? Legal.GetMaxLengthNickname(4, (LanguageID)pk.Language) : Legal.GetMaxLengthOT(4, (LanguageID)pk.Language));
        if (Gen3ChineseNames.Applies(pk, save, settings.Gen3ChineseNamesEnabled)) return Gen3ChineseNames.TryEncode(value, raw);
        return Gen12ChineseNames.TryEncode(value, raw, pk.Japanese, settings.GetNameEncoding(save.Generation));
    }

    private static string ResolveValue(string value, PKM pk, SaveFile save, AppSettings settings)
    {
        if (value == "*Nickname") return Decode(pk, save, settings, true);
        if (value == "*OriginalTrainerName") return Decode(pk, save, settings, false);
        if (value.StartsWith(BatchEditingUtil.PointerToken)) throw new TranslatedBatchNameException();
        return value;
    }

    public static bool Process(EntityBatchProcessor processor, PKM pk, SaveFile save, AppSettings settings, StringInstructionSet set)
    {
        if (!Applies(pk, save, settings) || (!set.Filters.Any(IsName) && !set.Instructions.Any(IsName))) return processor.Process(pk, set.Filters, set.Instructions);
        foreach (var filter in set.Filters.Where(IsName))
        {
            int comparison = string.CompareOrdinal(Decode(pk, save, settings, filter.PropertyName == nameof(PKM.Nickname)), ResolveValue(filter.PropertyValue, pk, save, settings));
            if (!filter.Comparer.IsCompareOperator(comparison)) { processor.AddSkipped(); return false; }
        }
        var otherFilters = set.Filters.Where(command => !IsName(command)).ToArray();
        if (!pk.Valid || !EntityBatchEditor.Instance.IsFilterMatch(otherFilters, pk)) return false;

        var candidate = pk.Clone();
        foreach (var command in set.Instructions)
        {
            if (IsName(command))
            {
                // Special expressions are not literal names. Reject rather than silently
                // treating a Core suggestion/property expression as Chinese text.
                if (command.Operation != InstructionOperation.Set || command.PropertyValue.StartsWith('$')
                    || !Encode(candidate, save, settings, command.PropertyName == nameof(PKM.Nickname), ResolveValue(command.PropertyValue, candidate, save, settings)))
                    throw new TranslatedBatchNameException();
            }
            else
            {
                var result = EntityBatchEditor.Instance.TryModify(candidate, [], [command]);
                if (result.HasFlag(ModifyResult.Error)) throw new TranslatedBatchNameException();
            }
            candidate.RefreshChecksum();
        }
        return processor.Process(pk, [], [], target =>
        {
            candidate.Data.CopyTo(target.Data);
            candidate.NicknameTrash.CopyTo(target.NicknameTrash);
            candidate.OriginalTrainerTrash.CopyTo(target.OriginalTrainerTrash);
            return true;
        });
    }
}
