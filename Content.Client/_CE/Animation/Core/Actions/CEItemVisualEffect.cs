using Content.Client.Animations;
using Content.Shared._CE.Animation.Core.Actions;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._CE.Animation.Core.Actions;

public sealed partial class CEItemVisualEffect : CESharedItemVisualEffect
{
    private const string EffectAnimationKey = "ce-item-visual-effect";
    private const string FadeAnimationKey = "ce-item-visual-fade";

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle)
    {
        var timing = IoCManager.Resolve<IGameTiming>();
        if (!timing.IsFirstTimePredicted)
            return;

        var transform = entManager.System<TransformSystem>();
        var spriteSystem = entManager.System<SpriteSystem>();
        var animationPlayer = entManager.System<AnimationPlayerSystem>();

        if (!entManager.TryGetComponent<TransformComponent>(entity, out var userXform)
            || userXform.MapID == MapId.Nullspace)
            return;

        // Spawn a client-side clone entity at the user's position
        var effectEntity = entManager.SpawnEntity("clientsideclone", userXform.Coordinates);

        if (!entManager.TryGetComponent<SpriteComponent>(effectEntity, out var effectSprite))
            return;

        // Set up the sprite: either override or copy from the used item
        if (SpriteOverride != null)
        {
            spriteSystem.LayerSetSprite((effectEntity, effectSprite), 0, SpriteOverride);
        }
        else if (used.HasValue && entManager.TryGetComponent<SpriteComponent>(used.Value, out var itemSprite))
        {
            spriteSystem.CopySprite((used.Value, itemSprite), (effectEntity, effectSprite));
        }

        spriteSystem.SetVisible((effectEntity, effectSprite), true);

        // Apply RSI override if specified
        if (AnimationRsi != null)
        {
            spriteSystem.LayerSetRsi((effectEntity, effectSprite), 0, AnimationRsi.Value);
        }

        // Set the sprite rotation to match the angle of the animation action
        spriteSystem.SetRotation((effectEntity, effectSprite), angle + Angle.FromDegrees(SpriteRotation));

        // Set up to follow the user
        if (FollowUser)
        {
            var track = entManager.EnsureComponent<TrackUserComponent>(effectEntity);
            track.User = entity;
            track.Offset = angle.ToWorldVec() * Offset;
        }
        else
        {
            // Position at the offset from the user if not following
            var worldPos = transform.GetWorldPosition(userXform) + angle.ToWorldVec() * Offset;
            transform.SetWorldPosition(effectEntity, worldPos);
        }

        // Set up timed despawn
        var despawn = entManager.EnsureComponent<TimedDespawnComponent>(effectEntity);
        despawn.Lifetime = Duration + 0.1f;

        // Play flick animation if a state is specified
        if (AnimationState != null)
        {
            var flickAnimation = new Robust.Client.Animations.Animation()
            {
                Length = TimeSpan.FromSeconds(Duration),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = 0,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(AnimationState, 0f),
                        },
                    },
                },
            };

            animationPlayer.Play(effectEntity, flickAnimation, EffectAnimationKey);
        }

        // Play fade-out animation if enabled
        if (FadeOut)
        {
            var fadeAnimation = new Robust.Client.Animations.Animation
            {
                Length = TimeSpan.FromSeconds(Duration),
                AnimationTracks =
                {
                    new AnimationTrackComponentProperty
                    {
                        ComponentType = typeof(SpriteComponent),
                        Property = nameof(SpriteComponent.Color),
                        KeyFrames =
                        {
                            new AnimationTrackProperty.KeyFrame(effectSprite.Color, Duration * 0.5f),
                            new AnimationTrackProperty.KeyFrame(effectSprite.Color.WithAlpha(0f), Duration * 0.5f),
                        },
                    },
                },
            };

            animationPlayer.Play(effectEntity, fadeAnimation, FadeAnimationKey);
        }
    }
}
