/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = null!;
    [Dependency] private readonly IConfigurationManager _configuration = null!;

    [Dependency] private readonly SharedTransformSystem _transform = null!;
    [Dependency] private readonly SharedAudioSystem _audio = null!;
    [Dependency] private readonly ActionBlockerSystem _blocker = null!;
    [Dependency] private readonly EntityLookupSystem _lookup = null!;
    [Dependency] private readonly SharedMapSystem _map = null!;
    [Dependency] private readonly SharedPopupSystem _popup = null!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;
    private EntityQuery<CEZLevelsNetworkComponent> _zNetworkQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    protected EntityQuery<CEZPhysicsComponent> ZPhysicsQuery;

    protected float ZGravityForce { get; private set; }
    protected float ZImpactVelocityLimit { get; private set; }
    protected float ZVelocityLimit { get; private set; }

    protected float MaxZLevelsBelowRendering { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        _configuration.OnValueChanged(CCVars.CEZLevelsPhysicsGravityForce, v => ZGravityForce = v, true);
        _configuration.OnValueChanged(CCVars.CEZLevelsPhysicsImpactVelocity, v => ZImpactVelocityLimit = v, true);
        _configuration.OnValueChanged(CCVars.CEZLevelsPhysicsVelocityLimit, v => ZVelocityLimit = v, true);
        _configuration.OnValueChanged(CCVars.CEZLevelsRenderingMaxZLevelsBelowRendering, v => MaxZLevelsBelowRendering = v, true);

        _mapQuery = GetEntityQuery<MapComponent>();
        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();
        _zNetworkQuery = GetEntityQuery<CEZLevelsNetworkComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        ZPhysicsQuery = GetEntityQuery<CEZPhysicsComponent>();

        InitMovement();
        InitView();
        InitializeActivation();
    }
}
