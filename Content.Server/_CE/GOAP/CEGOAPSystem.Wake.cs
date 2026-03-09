
using Content.Server._CE.Health;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.NPC;

namespace Content.Server._CE.GOAP;

public sealed partial class CEGOAPSystem
{
    [Dependency] private readonly CEHealthSystem _health = default!;

    private void InitWake()
    {
        SubscribeLocalEvent<CEHealthComponent, CECheckGOAPAwakeEvent>(OnCheckAwake);
        SubscribeLocalEvent<CEGOAPComponent, CEMobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<CEGOAPComponent> ent, ref CEMobStateChangedEvent args)
    {
        UpdateAwakeStatus(ent.Owner);
    }

    private void OnCheckAwake(Entity<CEHealthComponent> ent, ref CECheckGOAPAwakeEvent args)
    {
        if (args.Handled)
            return;

        if (_health.IsAlive(ent))
            args.WakeUp();
    }

    public void UpdateAwakeStatus(Entity<CEGOAPComponent?> ent)
    {
        var ev = new CECheckGOAPAwakeEvent();
        RaiseLocalEvent(ent, ev);

        if (ev.Awake)
            Wake(ent);
        else
            Sleep(ent);
    }

    /// <summary>
    /// Activates GOAP processing for this entity.
    /// </summary>
    private void Wake(Entity<CEGOAPComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        EnsureComp<CEActiveGOAPComponent>(ent);
        EnsureComp<ActiveNPCComponent>(ent);
    }

    /// <summary>
    /// Deactivates GOAP processing for this entity.
    /// </summary>
    private void Sleep(Entity<CEGOAPComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ClearPlan((ent, ent.Comp));
        RemCompDeferred<CEActiveGOAPComponent>(ent);
        RemCompDeferred<ActiveNPCComponent>(ent);
    }
}


public sealed class CECheckGOAPAwakeEvent : HandledEntityEventArgs
{
    private bool _awake = false;
    public bool Awake => _awake;

    public void WakeUp()
    {
        _awake = true;
        Handled = true;
    }
}
