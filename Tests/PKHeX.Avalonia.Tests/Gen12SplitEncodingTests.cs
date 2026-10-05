using System.Text.Json;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public sealed class Gen12SplitEncodingTests
{
    [Theory]
    [InlineData(Gen12NameEncoding.CknGb2312, "皮卡丘", "C6A4BFA8C7F0")]
    [InlineData(Gen12NameEncoding.CknGb2312, "あアαА", "A4A2A5A2A6C1A7A1")]
    [InlineData(Gen12NameEncoding.CknGb2312, "·—0", "A1A4A1AAA3B0")]
    [InlineData(Gen12NameEncoding.KoreanChinese, "皮卡丘", "012D010F012E")]
    [InlineData(Gen12NameEncoding.KoreanChinese, "茸炭虽琪眩", "02000400060008000A00")]
    public void ExactBytesAndTermination(Gen12NameEncoding encoding, string name, string hex)
    {
        var bytes = new byte[11];
        Assert.True(Gen12ChineseNames.TryEncode(name, bytes, encoding: encoding));
        Assert.Equal(hex, Convert.ToHexString(bytes.AsSpan(0, name.Length * 2)));
        Assert.Equal(name, Gen12ChineseNames.Decode(bytes, encoding: encoding));
        Assert.True(Gen12ChineseNames.IsWellFormedChineseName(bytes, encoding: encoding));
        Assert.False(Gen12ChineseNames.IsWellFormedChineseName(bytes.AsSpan(0, name.Length * 2), encoding: encoding));
        var before = bytes.ToArray();
        Assert.False(Gen12ChineseNames.TryEncode(name + name + name, bytes, encoding: encoding));
        Assert.Equal(before, bytes);
    }

    [Fact]
    public void CknEnglishCannotCollideWithHanziLeads()
    {
        var bytes = new byte[11];
        Assert.True(Gen12ChineseNames.TryEncode("A皮a", bytes, encoding: Gen12NameEncoding.CknGb2312));
        Assert.Equal("A皮a", Gen12ChineseNames.Decode(bytes, encoding: Gen12NameEncoding.CknGb2312));
        // q is B0 in the English table, which is a GB2312 Hanzi lead byte.
        var before = bytes.ToArray();
        Assert.False(Gen12ChineseNames.TryEncode("q", bytes, encoding: Gen12NameEncoding.CknGb2312));
        Assert.False(Gen12ChineseNames.TryEncode("b", bytes, encoding: Gen12NameEncoding.CknGb2312));
        Assert.Equal(before, bytes);
        Assert.Equal("\uFFFD", Gen12ChineseNames.Decode([0xB0], encoding: Gen12NameEncoding.CknGb2312));
    }

    [Fact]
    public void IndependentSettingsMigrateAndRoundTrip()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"Gen12NameEncoding\":0}")!;
        Assert.Equal(Gen12NameEncoding.Official, settings.GetNameEncoding(1));
        Assert.Equal(Gen12NameEncoding.Official, settings.GetNameEncoding(2));
        settings.Gen1NameEncoding = Gen12NameEncoding.CknGb2312;
        settings.Gen2NameEncoding = Gen12NameEncoding.KoreanChinese;
        settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(Gen12NameEncoding.CknGb2312, settings.GetNameEncoding(1));
        Assert.Equal(Gen12NameEncoding.KoreanChinese, settings.GetNameEncoding(2));
        Assert.Equal(Gen12NameEncoding.Official, settings.GetNameEncoding(3));
        Assert.False(Gen12ChineseNames.Applies(new PK2(), settings.GetNameEncoding(1)));
        Assert.False(Gen12ChineseNames.Applies(new PK1(), settings.GetNameEncoding(2)));
        Assert.Equal(Gen12NameEncoding.SimplifiedChinese, new AppSettings().GetNameEncoding(1));
        Assert.Equal(Gen12NameEncoding.SimplifiedChinese, new AppSettings().GetNameEncoding(2));
    }

    [Theory]
    [InlineData(1, Gen12NameEncoding.CknGb2312)]
    [InlineData(2, Gen12NameEncoding.KoreanChinese)]
    public void EditorUsesOnlyItsGenerationSetting(int generation, Gen12NameEncoding encoding)
    {
        PKM pk = generation == 1 ? new PK1() : new PK2();
        SaveFile save = generation == 1 ? new SAV1() : new SAV2();
        var settings = new AppSettings { Gen1NameEncoding = Gen12NameEncoding.CknGb2312, Gen2NameEncoding = Gen12NameEncoding.KoreanChinese };
        Assert.True(Gen12ChineseNames.TryEncode("皮卡丘", pk.NicknameTrash, encoding: encoding));
        Assert.True(Gen12ChineseNames.TryEncode("皮卡", pk.OriginalTrainerTrash, encoding: encoding));
        var vm = TestHelpers.CreateTestViewModel(pk, save, settings: settings).VM;
        Assert.Equal("皮卡丘", vm.Nickname);
        if (generation == 1) settings.Gen2NameEncoding = Gen12NameEncoding.Official;
        else settings.Gen1NameEncoding = Gen12NameEncoding.Official;
        vm.RefreshNameEncoding();
        Assert.Equal("皮卡丘", vm.Nickname);
        vm.Nickname = "卡丘";
        Assert.True(vm.CheckNameEncoding());
        Assert.Equal("卡丘", Gen12ChineseNames.Decode(vm.PreparePKM().NicknameTrash, encoding: encoding));
    }
}
