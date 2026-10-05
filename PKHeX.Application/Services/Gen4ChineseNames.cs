using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using PKHeX.Core;

namespace PKHeX.Application.Services;

public enum Gen4NameEncoding { Official, YYJOY, Xzonn }

public static class Gen4ChineseNames
{
    private static readonly Dictionary<ushort, char> YYJOY = Load("YYJOY");
    private static readonly Dictionary<ushort, char> Xzonn = Load("Xzonn");
    private static Dictionary<ushort, char> Load(string table)
    {
        using var stream = typeof(Gen4ChineseNames).Assembly.GetManifestResourceStream($"PKHeX.Application.Resources.Gen4.{table}.txt")!;
        using var reader = new StreamReader(stream);
        var result = new Dictionary<ushort, char>();
        while (reader.ReadLine() is { Length: 6 } line)
            result.Add(ushort.Parse(line.AsSpan(0, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture), line[5]);
        return result;
    }

    public static bool Enabled(Gen4NameEncoding encoding) => encoding is Gen4NameEncoding.YYJOY or Gen4NameEncoding.Xzonn;
    public static bool Applies(PKM pk, SaveFile save, Gen4NameEncoding encoding)
        => Enabled(encoding) && save is SAV4 && pk is PK4;

    public static string Decode(ReadOnlySpan<byte> bytes, Gen4NameEncoding encoding)
    {
        if (!Enabled(encoding)) return StringConverter4.GetString(bytes);
        var table = encoding == Gen4NameEncoding.YYJOY ? YYJOY : Xzonn;
        var text = new StringBuilder();
        for (int i = 0; i + 1 < bytes.Length; i += 2)
        {
            ushort code = BinaryPrimitives.ReadUInt16LittleEndian(bytes[i..]);
            // Empty PK4 buffers are zero-filled; both markers terminate Gen4 text.
            // Never pass the table's U+FFFF sentinel to the font fallback system.
            if (code is 0 or 0xFFFF) break;
            char glyph = code <= 0x1EC ? StringConverter4Util.TableINT[code]
                : table.GetValueOrDefault(code, '\uFFFD');
            text.Append(glyph == '\uFFFF' ? '\uFFFD' : StringConverter4Util.NormalizeGenderSymbol(glyph));
        }
        return text.ToString();
    }

    public static bool TryEncode(string name, Span<byte> destination, Gen4NameEncoding encoding, int maxLength)
    {
        if (!Enabled(encoding) || name.Length > maxLength || (name.Length + 1) * 2 > destination.Length) return false;
        var table = encoding == Gen4NameEncoding.YYJOY ? YYJOY : Xzonn;
        var candidate = new byte[destination.Length];
        candidate.AsSpan().Fill(0xFF);
        for (int i = 0; i < name.Length; i++)
        {
            char glyph = name[i];
            int index = StringConverter4Util.TableINT.IndexOf(glyph);
            ushort code;
            if (index >= 0 && glyph != '\uFFFF') code = (ushort)index;
            else
            {
                var entry = table.FirstOrDefault(pair => pair.Value == glyph);
                if (entry.Key == 0) return false;
                code = entry.Key;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(candidate.AsSpan(i * 2), code);
        }
        if (Decode(candidate, encoding) != name) return false;
        candidate.CopyTo(destination);
        return true;
    }

    public static string Nickname(PKM pk, SaveFile save, AppSettings settings)
        => Applies(pk, save, settings.Gen4NameEncoding) ? Decode(pk.NicknameTrash, settings.Gen4NameEncoding)
            : Gen3ChineseNames.Nickname(pk, save, settings);

    public static string OriginalTrainer(PKM pk, SaveFile save, AppSettings settings)
        => Applies(pk, save, settings.Gen4NameEncoding) ? Decode(pk.OriginalTrainerTrash, settings.Gen4NameEncoding)
            : Gen3ChineseNames.Applies(pk, save, settings.Gen3ChineseNamesEnabled) ? Gen3ChineseNames.Decode(pk.OriginalTrainerTrash)
            : Gen12ChineseNames.OriginalTrainer(pk, settings.GetNameEncoding(save.Generation));

    public static string TrainerName(SaveFile save, AppSettings settings)
        => save is SAV4 ds && Enabled(settings.Gen4NameEncoding) ? Decode(ds.OriginalTrainerTrash, settings.Gen4NameEncoding)
            : Gen3ChineseNames.TrainerName(save, settings.Gen3ChineseNamesEnabled);

    public static string BoxName(SaveFile save, int box, AppSettings settings)
    {
        if (save is not SAV4 ds || !Enabled(settings.Gen4NameEncoding))
            return Gen3ChineseNames.BoxName(save, box, settings.Gen3ChineseNamesEnabled).Replace('\uFFFF', '\uFFFD');
        return Decode(GetBoxBytes(ds, box), settings.Gen4NameEncoding);
    }

    private static (byte[] Data, int Offset) BoxSnapshot(SAV4 ds, int box)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(box);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(box, ds.BoxCount);
        // Core does not expose the storage buffer. Export an independent clone so
        // rendering never updates checksums or flags in the loaded save.
        var edited = ds.State.Edited;
        SaveFile clone;
        try { clone = ds.Clone(); }
        finally { ds.State.Edited = edited; } // Core's CloneInternal calls SetData on the source instance.
        var snapshot = clone.Write().ToArray();
        var (start, size, names) = ds switch
        {
            SAV4DP => (0xC100, 0x121E0, 0x11EE4),
            SAV4Pt => (0xCF2C, 0x121E4, 0x11EE4),
            SAV4HGSS => (0xF700, 0x12310, 0x12008),
            _ => throw new ArgumentOutOfRangeException(nameof(ds)),
        };
        int footer = start + size - 0x14;
        int active = SAV4BlockDetection.CompareFooters(snapshot, footer, footer + 0x40000);
        int offset = start + (active == 0 ? 0 : 0x40000) + names + box * 40;
        return (snapshot, offset);
    }

    public static byte[] GetBoxBytes(SAV4 save, int box)
    {
        var (data, offset) = BoxSnapshot(save, box);
        return data.AsSpan(offset, 40).ToArray();
    }

    public static void SetBoxBytes(SAV4 save, int box, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 40) throw new ArgumentException(nameof(bytes));
        var (data, offset) = BoxSnapshot(save, box);
        bytes.CopyTo(data.AsSpan(offset, 40));
        SAV4 updated = save switch
        {
            SAV4DP => new SAV4DP(data),
            SAV4Pt => new SAV4Pt(data),
            SAV4HGSS => new SAV4HGSS(data),
            _ => throw new ArgumentException(nameof(save)),
        };
        save.CopyChangesFrom(updated);
    }
}
