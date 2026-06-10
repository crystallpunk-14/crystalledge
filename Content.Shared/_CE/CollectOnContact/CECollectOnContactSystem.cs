using System.Linq;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._CE.CollectOnContact;

public sealed partial class CECollectOnContactSystem : EntitySystem
{
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TagSystem _tag = default!;

    [Dependency] private EntityQuery<ContainerManagerComponent> _containerQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<StorageComponent> _storageQuery = default!;
    [Dependency] private EntityQuery<CECollectOnContactTargetComponent> _targetQuery = default!;

    private readonly Stack<ContainerManagerComponent> _containerStack = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CECollectOnContactComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<CECollectOnContactComponent> coin, ref StartCollideEvent args)
    {
        if (!_physicsQuery.TryGetComponent(coin, out var physics)
            || physics.BodyStatus != BodyStatus.OnGround)
            return;

        if (!_containerQuery.TryGetComponent(args.OtherEntity, out var containerManager))
            return;

        TryCollectInto(coin, containerManager);
    }

    private void TryCollectInto(
        Entity<CECollectOnContactComponent> coin,
        ContainerManagerComponent rootManager)
    {
        _containerStack.Clear();
        _containerStack.Push(rootManager);

        do
        {
            var current = _containerStack.Pop();

            foreach (var container in current.Containers.Values)
            {
                // ToArray: ContainedEntities may change during iteration if Insert succeeds
                foreach (var item in container.ContainedEntities.ToArray())
                {
                    if (_targetQuery.HasComponent(item)
                        && _tag.HasTag(item, coin.Comp.StorageTag)
                        && _storageQuery.TryGetComponent(item, out var storage))
                    {
                        var initialCoords = Transform(coin).Coordinates;
                        var finalCoords   = Transform(item).Coordinates;
                        var initialRot    = Transform(coin).LocalRotation;

                        if (!_storage.Insert(item, coin, out var stacked, out _, storageComp: storage))
                            continue;

                        if (_timing.IsFirstTimePredicted && _net.IsClient)
                        {
                            var animTarget = stacked ?? coin.Owner;
                            _storage.PlayPickupAnimation(animTarget, initialCoords, finalCoords, initialRot);
                        }

                        return;
                    }

                    if (_containerQuery.TryGetComponent(item, out var nested))
                        _containerStack.Push(nested);
                }
            }
        } while (_containerStack.Count > 0);
    }
}
