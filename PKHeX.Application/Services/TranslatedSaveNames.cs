using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>Save-owned names use the same selected codecs as Pokémon names.</summary>
public static class TranslatedSaveNames
{
    public static bool UsesGb(SaveFile save, AppSettings settings) => save is SAV1 or SAV2
        && settings.GetNameEncoding(save.Generation) != Gen12NameEncoding.Official;
    public static Span<byte> TrainerBytes(SaveFile save) => save switch
    {
        SAV1 gb => gb.OriginalTrainerTrash,
        SAV2 gb => gb.OriginalTrainerTrash,
        _ => throw new ArgumentException(nameof(save)),
    };
    private static bool Japanese(SaveFile save) => save is SAV1 { Japanese: true } or SAV2 { Japanese: true };
    public static string TrainerName(SaveFile save, AppSettings settings) => UsesGb(save, settings)
        ? Gen12ChineseNames.Decode(TrainerBytes(save), Japanese(save), settings.GetNameEncoding(save.Generation))
        : Gen4ChineseNames.TrainerName(save, settings);
    public static bool TrySetTrainer(SaveFile save, AppSettings settings, string name)
        => Gen12ChineseNames.TryEncode(name, TrainerBytes(save), Japanese(save), settings.GetNameEncoding(save.Generation));

    public static bool UsesBoxes(SaveFile save, AppSettings settings) => save is SAV2 && UsesGb(save, settings)
        || save is SAV3 { Japanese: false } && settings.Gen3ChineseNamesEnabled
        || save is SAV4 && Gen4ChineseNames.Enabled(settings.Gen4NameEncoding);

    public static Span<byte> Gen2BoxBytes(SAV2 save, int box)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(box);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(box, save.BoxCount);
        int length = save.Korean ? 17 : 9;
        return save.Data.Slice(new SAV2Offsets(save).BoxNames + box * length, length);
    }

    public static string BoxName(SaveFile save, int box, AppSettings settings) => save is SAV2 gb && UsesGb(gb, settings)
        ? Gen12ChineseNames.Decode(Gen2BoxBytes(gb, box), gb.Japanese, settings.GetNameEncoding(2))
        : Gen4ChineseNames.BoxName(save, box, settings);

    public static byte[] BoxBytes(SaveFile save, int box) => save switch
    {
        SAV2 gb => Gen2BoxBytes(gb, box).ToArray(),
        SAV3 gba => Gen3ChineseNames.BoxNameBytes(gba, box).ToArray(),
        SAV4 ds => Gen4ChineseNames.GetBoxBytes(ds, box),
        _ => throw new ArgumentException(nameof(save)),
    };

    public static void SetBoxBytes(SaveFile save, int box, ReadOnlySpan<byte> bytes)
    {
        if (save is SAV2 gb) bytes.CopyTo(Gen2BoxBytes(gb, box));
        else if (save is SAV3 gba) bytes.CopyTo(Gen3ChineseNames.BoxNameBytes(gba, box));
        else if (save is SAV4 ds) Gen4ChineseNames.SetBoxBytes(ds, box, bytes);
        else throw new ArgumentException(nameof(save));
    }

    public static bool TrySetBoxName(SaveFile save, int box, string name, AppSettings settings)
    {
        if (name == BoxName(save, box, settings)) return true;
        if (save is SAV3 gba) return Gen3ChineseNames.TrySetBoxName(gba, box, name);
        var bytes = BoxBytes(save, box);
        bool valid = save is SAV2 gb
            ? Gen12ChineseNames.TryEncode(name, bytes, gb.Japanese, settings.GetNameEncoding(2))
            : Gen4ChineseNames.TryEncode(name, bytes, settings.Gen4NameEncoding, 8);
        if (!valid) return false;
        SetBoxBytes(save, box, bytes);
        return true;
    }
}
