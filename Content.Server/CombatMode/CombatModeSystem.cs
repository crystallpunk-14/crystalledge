using Content.Server.NPC.HTN;
using Content.Shared._CE.GOAP;
using Content.Shared.CombatMode;

namespace Content.Server.CombatMode;

public sealed class CombatModeSystem : SharedCombatModeSystem
{
    protected override bool IsNpc(EntityUid uid)
    {
        // CrystallEdge - also recognize CE GOAP mobs as NPCs
        return HasComp<HTNComponent>(uid) || HasComp<CEGOAPComponent>(uid);
    }
}
