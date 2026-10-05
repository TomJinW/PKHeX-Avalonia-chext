using System;
using System.Collections.Generic;
using System.Threading;
using PKHeX.Core;
using PKHeX.Application.Services;

namespace PKHeX.Application.UseCases;

/// <summary>
/// Progress notification for <see cref="LegalityAuditUseCase"/>: slots processed so far out of the total.
/// </summary>
public readonly record struct LegalityAuditProgress(int Completed, int Total);

/// <summary>
/// One audited slot: identity/location info plus the save-scoped <see cref="SaveLegalityCheck"/> outcome,
/// so the result never diverges from what the single-Pokémon editor's legality panel would show.
/// </summary>
public sealed class LegalityAuditEntry
{
    /// <summary>The audited entity. Never a party/box slot placeholder (empty slots are skipped).</summary>
    public PKM Entity { get; }

    /// <summary>True if this slot came from the party, false if it came from a box.</summary>
    public bool IsParty { get; }

    /// <summary>Zero-based box index. Meaningless (0) when <see cref="IsParty"/> is true.</summary>
    public int Box { get; }

    /// <summary>Zero-based slot index within the box, or the party index when <see cref="IsParty"/> is true.</summary>
    public int Slot { get; }

    /// <summary>Whether the slot passed every legality check.</summary>
    public bool Valid { get; }

    /// <summary>
    /// The report from the same save-scoped check used to compute <see cref="Valid"/>.
    /// </summary>
    public string ReportText { get; }

    public LegalityAuditEntry(PKM pk, SaveLegalityResult la, bool isParty, int box, int slot)
    {
        Entity = pk;
        IsParty = isParty;
        Box = box;
        Slot = slot;
        Valid = la.Valid;
        ReportText = la.ReportText;
    }

    /// <summary>Single-line, semicolon-joined rendering of <see cref="ReportText"/> for grid/CSV display.</summary>
    public string FailureSummary => Valid ? string.Empty : ReportText.Replace(Environment.NewLine, "; ").Replace("\n", "; ");
}

/// <summary>
/// Scans every occupied party and box slot of a save file, running each through the same
/// <see cref="SaveLegalityCheck"/> code path (same save and codec) as the single-Pokémon
/// editor's legality panel, so audit verdicts can never diverge from the editor.
/// Supports cooperative cancellation and progress reporting so callers can run it off the UI thread.
/// </summary>
public sealed class LegalityAuditUseCase
{
    public IReadOnlyList<LegalityAuditEntry> Execute(SaveFile sav, IProgress<LegalityAuditProgress>? progress = null, CancellationToken cancellationToken = default, Gen12NameEncoding nameEncoding = Gen12NameEncoding.Official, bool ignoreGen3NameTrashErrors = false, bool gen5ChineseNames = false, bool gen6ChineseNames = false, Gen4NameEncoding gen4Encoding = Gen4NameEncoding.Official, bool gen3NgcChineseNames = false)
    {
        var boxData = sav.BoxData;
        var slotsPerBox = sav.BoxSlotCount;
        var partyCount = sav.PartyCount;
        var total = boxData.Count + partyCount;
        var completed = 0;

        var results = new List<LegalityAuditEntry>();

        for (int i = 0; i < boxData.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pk = boxData[i];
            if (pk.Species != 0)
            {
                var la = SaveLegalityCheck.Evaluate(pk, sav, nameEncoding, ignoreGen3NameTrashErrors, gen5ChineseNames, gen6ChineseNames, gen4Encoding, gen3NgcChineseNames);
                results.Add(new LegalityAuditEntry(pk, la, isParty: false, box: i / slotsPerBox, slot: i % slotsPerBox));
            }

            progress?.Report(new LegalityAuditProgress(++completed, total));
        }

        for (int i = 0; i < partyCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PKM pk;
            try { pk = sav.GetPartySlotAtIndex(i); }
            catch { pk = sav.BlankPKM; }

            if (pk.Species != 0)
            {
                var la = SaveLegalityCheck.Evaluate(pk, sav, nameEncoding, ignoreGen3NameTrashErrors, gen5ChineseNames, gen6ChineseNames, gen4Encoding, gen3NgcChineseNames);
                results.Add(new LegalityAuditEntry(pk, la, isParty: true, box: 0, slot: i));
            }

            progress?.Report(new LegalityAuditProgress(++completed, total));
        }

        return results;
    }
}
