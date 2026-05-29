/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Mapping;

public sealed class CEZLevelMappingSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelMapComponent, CEMapAddedIntoZNetworkEvent>(OnAddedIntoZNetwork);
    }

    private void OnAddedIntoZNetwork(Entity<CEZLevelMapComponent> ent, ref CEMapAddedIntoZNetworkEvent args)
    {
        var hasInitializedMaps = false;
        foreach (var existingMapUid in args.Network.Comp.ZLevels.Values)
        {
            if (existingMapUid.HasValue && _map.IsInitialized(existingMapUid.Value))
            {
                hasInitializedMaps = true;
                break;
            }
        }

        if (hasInitializedMaps)
            _map.InitializeMap(ent.Owner);
    }
}
