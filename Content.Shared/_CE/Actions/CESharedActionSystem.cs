using Content.Shared._CE.Mana.Core;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;

namespace Content.Shared._CE.Actions;

public abstract partial class CESharedActionSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly CESharedMagicEnergySystem _magicEnergy = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    private EntityQuery<ActionComponent> _actionQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actionQuery = GetEntityQuery<ActionComponent>();

        InitializePerformed();
    }
}
