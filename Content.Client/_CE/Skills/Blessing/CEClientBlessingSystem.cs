using System.Numerics;
using Content.Client.Gravity;
using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.Utility;
using Robust.Shared.Animations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Skills.Blessing;

public sealed partial class CEClientBlessingSystem : CESharedBlessingSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly CESharedSkillSystem _skill = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBlessingComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<CEBlessingComponent, AnimationCompletedEvent>(OnAnimationComplete);
        SubscribeLocalEvent<CEBlessingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(HandlePlayerAttached);
    }

    private void HandlePlayerAttached(LocalPlayerAttachedEvent ev)
    {
        if (ev.Entity != _player.LocalEntity)
            return;

        var query = EntityQueryEnumerator<CEBlessingComponent>();
        while (query.MoveNext(out var ent, out var blessing))
        {
            UpdateVisuals((ent, blessing));
        }
    }

    private void OnStartup(Entity<CEBlessingComponent> ent, ref ComponentStartup args)
    {
        FloatAnimation(ent);
        UpdateVisuals(ent);
    }

    private void OnAnimationComplete(Entity<CEBlessingComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != ent.Comp.AnimationKey)
            return;
        FloatAnimation(ent);
    }

    private void OnAfterAutoHandleState(Entity<CEBlessingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<CEBlessingComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        Entity<SpriteComponent?> entity = (ent, sprite);

        if (ent.Comp.Skill == null)
        {
            _sprite.LayerSetVisible(entity, ent.Comp.MapLayer, false);
            _sprite.LayerSetVisible(entity, ent.Comp.MapVFXLayer, false);
            return;
        }

        if (!_proto.Resolve(ent.Comp.Skill.Value, out var proto))
            return;

        var icon = _skill.GetSkillIcon(ent.Comp.Skill.Value);

        if (icon is null)
            return;

        _sprite.LayerSetSprite(entity, ent.Comp.MapLayer, icon);
        _sprite.LayerSetVisible(entity, ent.Comp.MapLayer, true);

        if (proto.Vfx is not null)
        {
            _sprite.LayerSetSprite(entity,  ent.Comp.MapVFXLayer, proto.Vfx);
            _sprite.LayerSetVisible(entity, ent.Comp.MapVFXLayer, true);
        }

        _light.SetColor(entity, proto.Color);

        if (_player.LocalEntity != ent.Comp.ForPlayer)
            _sprite.SetColor(entity, Color.White.WithAlpha(0.2f));
        else
            _sprite.SetColor(entity, Color.White.WithAlpha(1f));
    }

    private void FloatAnimation(Entity<CEBlessingComponent> ent, bool stop = false)
    {
        if (stop)
        {
            _animation.Stop(ent.Owner, ent.Comp.AnimationKey);
            return;
        }

        var animation = new Robust.Client.Animations.Animation
        {
            // We multiply by the number of extra keyframes to make time for them
            Length = TimeSpan.FromSeconds(ent.Comp.AnimationTime*2),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(ent.Comp.FloatingStartOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(ent.Comp.FloatingOffset, ent.Comp.AnimationTime),
                        new AnimationTrackProperty.KeyFrame(ent.Comp.FloatingStartOffset, ent.Comp.AnimationTime),
                    }
                }
            }
        };

        if (!_animation.HasRunningAnimation(ent, ent.Comp.AnimationKey))
            _animation.Play(ent, animation, ent.Comp.AnimationKey);
    }
}
