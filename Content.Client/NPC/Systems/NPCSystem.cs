using Content.Client.NPC.HTN;
using Content.Shared._CE.GOAP;
using Content.Shared.NPC.Systems;

namespace Content.Client.NPC.Systems;

public sealed class NPCSystem : SharedNPCSystem
{
    public override bool IsNpc(EntityUid uid)
    {
        // CrystallEdge - also recognize CE GOAP mobs as NPCs
        return HasComp<HTNComponent>(uid) || HasComp<CEGOAPComponent>(uid);
    }
}
