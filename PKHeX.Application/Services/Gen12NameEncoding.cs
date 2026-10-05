using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PKHeX.Core;

namespace PKHeX.Application.Services;

public enum Gen12NameEncoding
{
    Official,
    SimplifiedChinese,
    CknGb2312,
    KoreanChinese,
}

/// <summary>
/// Opt-in name codec for the supplied Gen 1/2 translation table. Does not change Core's
/// language detection, save layout, or official Korean codec.
/// </summary>
public static class Gen12ChineseNames
{
    private static readonly Dictionary<ushort, char> DecodeMap = new();
    private static readonly Dictionary<char, ushort> EncodeMap = new();

    static Gen12ChineseNames()
    {
        using var stream = typeof(Gen12ChineseNames).Assembly.GetManifestResourceStream(
            "PKHeX.Application.Resources.Gen12.charmap_cn.asm")!;
        using var reader = new StreamReader(stream);
        var pattern = new Regex("^\\s*charmap_w \"(.)\",\\s*\\$([0-9A-Fa-f]+),\\s*\\$([0-9A-Fa-f]+)");
        while (reader.ReadLine() is { } line)
        {
            var match = pattern.Match(line);
            if (!match.Success) continue; // ASM control tokens are not name characters.
            var high = byte.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var low = byte.Parse(match.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var code = (ushort)((high << 8) | low);
            var character = match.Groups[1].Value[0];
            DecodeMap.TryAdd(code, character); // First alias is canonical (〇 / ideographic space).
            EncodeMap.Add(character, code);
        }
    }

    private static readonly Dictionary<ushort, char> CknMap = LoadTable("CknGb2312");
    private static readonly Dictionary<ushort, char> KoreanMap = LoadTable("KoreanChinese");
    private static Dictionary<ushort, char> LoadTable(string name)
    {
        using var stream = typeof(Gen12ChineseNames).Assembly.GetManifestResourceStream($"PKHeX.Application.Resources.Gen12.{name}.txt")!;
        using var reader = new StreamReader(stream);
        var map = new Dictionary<ushort, char>();
        while (reader.ReadLine() is { } line)
            if (line.Length == 6)
                map.Add(ushort.Parse(line.AsSpan(0, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture), line[5]);
        return map;
    }
    private static Dictionary<ushort, char> Map(Gen12NameEncoding encoding) => encoding switch
    {
        Gen12NameEncoding.CknGb2312 => CknMap,
        Gen12NameEncoding.KoreanChinese => KoreanMap,
        _ => DecodeMap,
    };
    private static bool IsLead(byte value, Gen12NameEncoding encoding) => encoding switch
    {
        Gen12NameEncoding.CknGb2312 => value is >= 0xA1 and <= 0xF7,
        Gen12NameEncoding.KoreanChinese => value is >= 1 and <= 0x0B,
        _ => value is >= 1 and <= 0x2E,
    };

    public static bool Applies(PKM pk, Gen12NameEncoding encoding)
        => pk is GBPKML && (encoding == Gen12NameEncoding.SimplifiedChinese
            || encoding == Gen12NameEncoding.CknGb2312 && pk is PK1
            || encoding == Gen12NameEncoding.KoreanChinese && pk is PK2);

    public static string Nickname(PKM pk, Gen12NameEncoding encoding)
        => Applies(pk, encoding) ? Decode(pk.NicknameTrash, pk.Japanese, encoding) : pk.Nickname;

    public static string OriginalTrainer(PKM pk, Gen12NameEncoding encoding)
        => Applies(pk, encoding) ? Decode(pk.OriginalTrainerTrash, pk.Japanese, encoding) : pk.OriginalTrainerName;

    /// <summary>Requires a nonempty, terminated name, at least one mapped double-byte glyph,
    /// and no unknown or incomplete sequence. Trailing trash after the terminator is ignored.</summary>
    public static bool IsWellFormedChineseName(ReadOnlySpan<byte> data, bool japanese = false, Gen12NameEncoding encoding = Gen12NameEncoding.SimplifiedChinese)
    {
        bool hasChinese = false;
        var singleByte = japanese ? StringConverter1.TableJP : StringConverter1.TableEN;
        for (int i = 0; i < data.Length; i++)
        {
            byte high = data[i];
            if (high is 0 or 0x50) return i > 0 && hasChinese;
            if (IsLead(high, encoding))
            {
                if (++i >= data.Length || !Map(encoding).ContainsKey((ushort)((high << 8) | data[i]))) return false;
                hasChinese = true;
            }
            else if (high != StringConverter1.TradeOTCode && singleByte[high] == '\0')
                return false;
        }
        return false; // No room for a terminator: never excuse an overfull name.
    }

    public static string Decode(ReadOnlySpan<byte> data, bool japanese = false, Gen12NameEncoding encoding = Gen12NameEncoding.SimplifiedChinese)
    {
        var result = new StringBuilder(data.Length);
        var singleByte = japanese ? StringConverter1.TableJP : StringConverter1.TableEN;
        for (int i = 0; i < data.Length; i++)
        {
            byte high = data[i];
            if (high is 0 or 0x50) break;
            if (IsLead(high, encoding))
            {
                if (i + 1 >= data.Length)
                {
                    result.Append('\uFFFD');
                    break;
                }
                var code = (ushort)((high << 8) | data[++i]);
                result.Append(Map(encoding).GetValueOrDefault(code, '\uFFFD'));
            }
            else
            {
                char character = high == StringConverter1.TradeOTCode ? StringConverter1.TradeOT : singleByte[high];
                result.Append(character == '\0' ? '\uFFFD' : character);
            }
        }
        return result.ToString();
    }

    /// <summary>Validates the entire name before writing; never truncates or partially writes.</summary>
    public static bool TryEncode(string value, Span<byte> destination, bool japanese = false, Gen12NameEncoding encoding = Gen12NameEncoding.SimplifiedChinese)
    {
        var bytes = new List<byte>();
        var singleByte = japanese ? StringConverter1.TableJP : StringConverter1.TableEN;
        foreach (char character in value)
        {
            ushort pair = encoding == Gen12NameEncoding.SimplifiedChinese ? EncodeMap.GetValueOrDefault(character)
                : Map(encoding).FirstOrDefault(entry => entry.Value == character).Key;
            if (pair != 0)
            {
                bytes.Add((byte)(pair >> 8));
                bytes.Add((byte)pair);
            }
            else
            {
                int index = character == StringConverter1.TradeOT ? StringConverter1.TradeOTCode : singleByte.IndexOf(character);
                if (character == '\0' || index <= 0 || index == 0x50 || IsLead((byte)index, encoding)) return false;
                bytes.Add((byte)index);
            }
        }
        if (bytes.Count >= destination.Length) return false; // Reserve the terminator.
        destination.Fill(0x50);
        for (int i = 0; i < bytes.Count; i++) destination[i] = bytes[i];
        return true;
    }
}
