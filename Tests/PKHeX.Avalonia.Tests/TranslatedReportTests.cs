using System.Reflection;
using PKHeX.Core;
using PKHeX.Application.UseCases;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public sealed class TranslatedReportTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    public void RowsAndExportsUseSelectedTableWithoutMutatingEntity(int generation, int encoding)
    {
        SaveFile save = generation switch { 1 => new SAV1(), 2 => new SAV2(), 3 => new SAV3E(), _ => new SAV4HGSS() };
        var settings = new AppSettings { Gen1NameEncoding = (Gen12NameEncoding)encoding, Gen2NameEncoding = (Gen12NameEncoding)encoding, Gen4NameEncoding = (Gen4NameEncoding)encoding };
        var pk = save.BlankPKM;
        pk.Species = 25;
        pk.Language = (int)LanguageID.English;
        pk.RefreshChecksum();
        Assert.True(TranslatedBatchNames.Process(new EntityBatchProcessor(), pk, save, settings,
            new StringInstructionSet(".Nickname=皮卡丘\n.OriginalTrainerName=小智")));
        var before = pk.Data.ToArray();
        var nick = pk.NicknameTrash.ToArray();
        var ot = pk.OriginalTrainerTrash.ToArray();
        var box = new BoxReportRow(pk, GameInfo.Strings, 0, 0, save, settings);
        var verdict = SaveLegalityCheck.Evaluate(pk, save, settings.GetNameEncoding(save.Generation), gen4Encoding: settings.Gen4NameEncoding);
        var entry = new LegalityAuditEntry(pk, verdict, false, 0, 0);
        var audit = new LegalityAuditRow(entry, GameInfo.Strings, save, settings);
        Assert.Equal("皮卡丘", box.Nickname);
        Assert.Equal("小智", box.OT);
        Assert.Equal(box.Nickname, audit.Nickname);
        Assert.Equal(box.OT, audit.OT);
        Assert.Equal(verdict.Valid, audit.Valid);
        Assert.Equal(verdict.ReportText, audit.ReportText);
        Assert.Equal("小智", BoxReportColumn.CreateDefaults().Single(c => c.Id == "OT").GetValue(box));
        var csv = Export(typeof(BoxReportViewModel), "BuildCsv", new[] { box });
        Assert.Contains("皮卡丘", csv);
        Assert.Contains("小智", csv);
        Assert.Contains("皮卡丘", Export(typeof(LegalityAuditViewModel), "BuildCsv", new[] { audit }));
        Assert.Contains("皮卡丘", Export(typeof(LegalityAuditViewModel), "BuildTextReport", new[] { audit }));
        var official = new AppSettings { Gen1NameEncoding = Gen12NameEncoding.Official, Gen2NameEncoding = Gen12NameEncoding.Official, Gen3ChineseNamesEnabled = false, Gen4NameEncoding = Gen4NameEncoding.Official };
        Assert.Equal(pk.Nickname, new BoxReportRow(pk, GameInfo.Strings, 0, 0, save, official).Nickname);
        Assert.Equal(pk.OriginalTrainerName, new LegalityAuditRow(entry, GameInfo.Strings, save, official).OT);
        Assert.Equal(before, pk.Data.ToArray());
        Assert.Equal(nick, pk.NicknameTrash.ToArray());
        Assert.Equal(ot, pk.OriginalTrainerTrash.ToArray());
    }

    private static string Export(Type type, string method, object rows)
        => (string)type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [rows])!;
}
