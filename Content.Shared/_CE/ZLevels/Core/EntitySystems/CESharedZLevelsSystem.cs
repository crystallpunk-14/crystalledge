/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    [Dependency] private readonly EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private readonly EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;
    [Dependency] private readonly EntityQuery<CEZLevelsNetworkComponent> _zNetworkQuery = default!;
    [Dependency] private readonly EntityQuery<MapGridComponent> _gridQuery = default!;

    protected EntityQuery<CEZPhysicsComponent> ZPhysicsQuery;

    protected float ZGravityForce { get; private set; }
    protected float ZImpactVelocityLimit { get; private set; }
    protected float ZVelocityLimit { get; private set; }

    public int MaxZLevelsBelowRendering { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        _configuration.OnValueChanged(CCVars.CEZLevelsPhysicsGravityForce, v => ZGravityForce = v, true);
        _configuration.OnValueChanged(CCVars.CEZLevelsPhysicsImpactVelocity, v => ZImpactVelocityLimit = v, true);
        _configuration.OnValueChanged(CCVars.CEZLevelsPhysicsVelocityLimit, v => ZVelocityLimit = v, true);
        _configuration.OnValueChanged(CCVars.CEZLevelsRenderingMaxZLevelsBelowRendering, v => MaxZLevelsBelowRendering = v, true);

        ZPhysicsQuery = GetEntityQuery<CEZPhysicsComponent>();

        InitMovement();
        InitView();
        InitializeActivation();
    }
}
