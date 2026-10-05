using System;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class PokemonEditorViewModel
{
    public bool UsesLegacyStats => _sav.Generation is 1 or 2 && _pk is GBPKM;
    public bool HasModernFields => !UsesLegacyStats;
    public bool HasGen2Fields => !UsesLegacyStats || _pk is PK2;
    public string ShinyToggleTooltip => LocalizedStrings.Instance[UsesLegacyStats && _pk is PK1
        ? "PokemonEditor_Gen1ShinyTooltip" : "PokemonEditor_ToggleShinyTooltip"];
    public string IvColumnLabel => UsesLegacyStats ? LocalizedStrings.Instance["PokemonEditor_ColDVs"] : LocalizedStrings.Instance["PokemonEditor_ColIVs"];
    public int MaximumIV => UsesLegacyStats ? 15 : 31;
    public int MaximumMetLevel => UsesLegacyStats ? 63 : 100;
    public int MaximumEV => UsesLegacyStats ? ushort.MaxValue : 252;

    private void NotifyLegacyFields()
    {
        foreach (var name in new[] { nameof(UsesLegacyStats), nameof(HasModernFields), nameof(HasGen2Fields),
                     nameof(ShinyToggleTooltip), nameof(IvColumnLabel), nameof(MaximumIV), nameof(MaximumEV), nameof(MaximumMetLevel) })
            OnPropertyChanged(name);
        if (!HasGen2Fields && IsMetEditorSection)
            SelectedEditorSection = 0;
    }

    [RelayCommand]
    private void ResetNickname()
    {
        if (Species <= 0 || Species >= GameInfo.Strings.specieslist.Length)
            return;
        Nickname = GameInfo.Strings.specieslist[Species];
    }

    private void ApplyStatValues()
    {
        if (UsesLegacyStats)
        {
            _pk.IV_ATK = Math.Clamp(IvATK, 0, 15);
            _pk.IV_DEF = Math.Clamp(IvDEF, 0, 15);
            _pk.IV_SPA = Math.Clamp(IvSPA, 0, 15);
            _pk.IV_SPE = Math.Clamp(IvSPE, 0, 15);
            _pk.EV_HP = Math.Clamp(EvHP, 0, ushort.MaxValue);
            _pk.EV_ATK = Math.Clamp(EvATK, 0, ushort.MaxValue);
            _pk.EV_DEF = Math.Clamp(EvDEF, 0, ushort.MaxValue);
            _pk.EV_SPA = Math.Clamp(EvSPA, 0, ushort.MaxValue);
            _pk.EV_SPE = Math.Clamp(EvSPE, 0, ushort.MaxValue);
            SyncLegacyStats();
            return;
        }
        _pk.IV_HP = IvHP;
        _pk.IV_ATK = IvATK;
        _pk.IV_DEF = IvDEF;
        _pk.IV_SPA = IvSPA;
        _pk.IV_SPD = IvSPD;
        _pk.IV_SPE = IvSPE;
        _pk.EV_HP = EvHP;
        _pk.EV_ATK = EvATK;
        _pk.EV_DEF = EvDEF;
        _pk.EV_SPA = EvSPA;
        _pk.EV_SPD = EvSPD;
        _pk.EV_SPE = EvSPE;
    }

    // GB gender, shininess, HP DV and Unown form are derived from the same four DVs.
    private void SyncLegacyStats()
    {
        var loading = _isLoading;
        var spriteChanged = Gender != _pk.Gender || IsShiny != _pk.IsShiny || Form != _pk.Form;
        _isLoading = true;
        try
        {
            IvHP = _pk.IV_HP;
            IvATK = _pk.IV_ATK;
            IvDEF = _pk.IV_DEF;
            IvSPA = _pk.IV_SPA;
            IvSPD = _pk.IV_SPD;
            IvSPE = _pk.IV_SPE;
            EvHP = _pk.EV_HP;
            EvATK = _pk.EV_ATK;
            EvDEF = _pk.EV_DEF;
            EvSPA = _pk.EV_SPA;
            EvSPD = _pk.EV_SPD;
            EvSPE = _pk.EV_SPE;
            Gender = _pk.Gender;
            IsShiny = _pk.IsShiny;
            Form = _pk.Form;
        }
        finally { _isLoading = loading; }
        if (spriteChanged && !loading)
            UpdateSprite();
    }

    private void SetLegacyShiny(bool shiny)
    {
        if (shiny)
            _pk.SetShiny();
        else if (_pk.IsShiny)
            _pk.IV_DEF ^= 1; // Break the shiny DV pattern, preserving Unown's form bits.
    }

    private void SetLegacyForm(int form)
    {
        if (_pk is not GBPKM gb || gb.Species != 201 || form is < 0 or >= 26 || gb.Form == form)
            return;

        // Gen 2 has only A-Z. Search the finite DV space instead of Core's unbounded
        // random setter. Preserve shininess when the requested form permits it (I/V),
        // and otherwise choose the smallest bit change that produces the requested form.
        var candidate = (GBPKM)gb.Clone();
        var original = gb.DV16;
        var shiny = gb.IsShiny;
        var best = original;
        var bestCost = int.MaxValue;
        for (int dv = 0; dv <= ushort.MaxValue; dv++)
        {
            candidate.DV16 = (ushort)dv;
            if (candidate.Form != form)
                continue;
            int cost = System.Numerics.BitOperations.PopCount((uint)(original ^ dv));
            if (candidate.IsShiny != shiny)
                cost += 32;
            if (cost >= bestCost)
                continue;
            bestCost = cost;
            best = (ushort)dv;
        }
        gb.DV16 = best;
    }

    private void SetLegacyGender(int gender)
    {
        // Fixed-gender species cannot change; mixed-gender species use the attack DV threshold.
        var ratio = _pk.PersonalInfo.Gender;
        if (gender is 0 or 1 && ratio is > 0 and < 254)
        {
            var threshold = ratio >> 4;
            IvATK = gender == 0 ? Math.Max(IvATK, threshold + 1) : Math.Min(IvATK, threshold);
        }
        SyncLegacyStats();
        RecalculateStats();
        UpdateSprite();
        Validate();
    }
}
