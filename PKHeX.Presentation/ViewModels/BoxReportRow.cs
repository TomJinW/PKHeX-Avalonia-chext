using PKHeX.Core;

namespace PKHeX.Presentation.ViewModels;

/// <summary>
/// One row of the Box Data Report: an <see cref="EntitySummary"/> that knows which
/// box/slot it came from so the report can navigate back to the source Pokémon.
/// </summary>
public sealed class BoxReportRow : EntitySummary
{
    public new string Nickname { get; }
    public new string OT { get; }

    /// <summary>Zero-based box index of the source slot.</summary>
    public int Box { get; }

    /// <summary>Zero-based slot index within the box.</summary>
    public int Slot { get; }

    public override string Position => $"B{Box + 1:00}:{Slot + 1:00}";

    public int IVTotal => IV_HP + IV_ATK + IV_DEF + IV_SPA + IV_SPD + IV_SPE;
    public int EVTotal => EV_HP + EV_ATK + EV_DEF + EV_SPA + EV_SPD + EV_SPE;
    // Core's summary exposes formatted stat strings; the report must sort by numeric value.
    public int NumericHP => int.Parse(HP);
    public int NumericATK => int.Parse(ATK);
    public int NumericDEF => int.Parse(DEF);
    public int NumericSPA => int.Parse(SPA);
    public int NumericSPD => int.Parse(SPD);
    public int NumericSPE => int.Parse(SPE);

    public BoxReportRow(PKM pk, GameStrings strings, int box, int slot, SaveFile? save = null, AppSettings? settings = null) : base(pk, strings)
    {
        var nameSettings = settings ?? new AppSettings();
        Nickname = save is null ? base.Nickname : Gen4ChineseNames.Nickname(pk, save, nameSettings);
        OT = save is null ? base.OT : Gen4ChineseNames.OriginalTrainer(pk, save, nameSettings);
        Box = box;
        Slot = slot;
    }
}
