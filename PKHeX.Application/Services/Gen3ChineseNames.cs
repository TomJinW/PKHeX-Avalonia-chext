using System.Globalization;
using System.Text;
using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>GBA Chinese font patch codec; separate from both official Gen 3 and GB encodings.</summary>
public static class Gen3ChineseNames
{
    private static readonly Dictionary<ushort, char> DecodeMap = new();
    private static readonly Dictionary<char, ushort> EncodeMap = new();

    static Gen3ChineseNames()
    {
        using var stream = typeof(Gen3ChineseNames).Assembly.GetManifestResourceStream(
            "PKHeX.Application.Resources.Gen3.PMRSEFRLG_charmap.txt")!;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            int split = line.IndexOf('=');
            if (split < 0 || line.Length != split + 2 ||
                !ushort.TryParse(line.AsSpan(0, split), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                continue; // Control tokens and multi-character abbreviations are not name glyphs.
            DecodeMap[code] = line[^1];
        }
        foreach (var (code, glyph) in DecodeMap.OrderBy(pair => pair.Key))
            EncodeMap.TryAdd(glyph, code);
    }

    public static bool Applies(PKM pk, SaveFile save, bool enabled)
        => enabled && save is SAV3 { Japanese: false } && pk is PK3 { Japanese: false };

    public static string TrainerName(SaveFile save, bool enabled)
        => enabled && save is SAV3 { Japanese: false } gba
            ? Decode(gba.SmallBlock.OriginalTrainerTrash[..7]) : save.OT;

    // GBA storage has 14 nine-byte box-name fields after the Pokémon slots.
    public static Span<byte> BoxNameBytes(SAV3 save, int box)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(box);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(box, save.BoxCount);
        return save.Storage.Slice(save.GetBoxOffset(save.BoxCount) + box * 9, 9);
    }

    public static string BoxName(SaveFile save, int box, bool enabled)
        => enabled && save is SAV3 { Japanese: false } gba ? Decode(BoxNameBytes(gba, box))
            : save is IBoxDetailNameRead reader ? reader.GetBoxName(box)
            : BoxDetailNameExtensions.GetDefaultBoxName(box);

    public static bool TrySetBoxName(SAV3 save, int box, string name)
    {
        Span<byte> encoded = stackalloc byte[9];
        encoded.Fill(0xFF);
        if (!TryEncode(name, encoded[..8])) return false;
        encoded.CopyTo(BoxNameBytes(save, box));
        return true;
    }

    private static bool IsLead(byte value) => value is >= 1 and <= 0x1E and not (6 or 0x1B);

    public static string Decode(ReadOnlySpan<byte> data)
    {
        var result = new StringBuilder(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            byte value = data[i];
            if (value == 0xFF) break;
            ushort code = value;
            // Match the patch renderer: 06/1B stay western; a low byte < F7
            // enables a Chinese pair. Zero is a valid low byte, not a terminator.
            if (IsLead(value) && i + 1 < data.Length && data[i + 1] < 0xF7)
                code = (ushort)((value << 8) | data[++i]);
            result.Append(DecodeMap.GetValueOrDefault(code, '\uFFFD'));
        }
        return result.ToString();
    }

    public static bool TryEncode(string value, Span<byte> destination)
    {
        var bytes = new List<byte>();
        foreach (var character in value)
        {
            if (!EncodeMap.TryGetValue(character, out var code)) return false;
            if (code > byte.MaxValue) bytes.Add((byte)(code >> 8));
            bytes.Add((byte)code);
        }
        // GBA name fields may fill their entire 10/7-byte capacity without a terminator.
        if (bytes.Count > destination.Length) return false;
        var candidate = new byte[destination.Length];
        candidate.AsSpan().Fill(0xFF);
        bytes.CopyTo(candidate);
        // Reject western combinations that the patch would instead interpret as Chinese.
        if (Decode(candidate) != value) return false;
        candidate.CopyTo(destination);
        return true;
    }

    public static string Nickname(PKM pk, SaveFile save, AppSettings settings)
        => Applies(pk, save, settings.Gen3ChineseNamesEnabled) ? Decode(pk.NicknameTrash)
            : Gen12ChineseNames.Nickname(pk, settings.GetNameEncoding(save.Generation));
}
