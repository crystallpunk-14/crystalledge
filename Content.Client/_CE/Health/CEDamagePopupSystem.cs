using System.Numerics;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared._CE.Health.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Client._CE.Health.CEDamagePopupOverlay;

namespace Content.Client._CE.Health;

/// <summary>
/// Pure client-side system that spawns floating damage/heal numbers.
/// Subscribes to <see cref="CEDamageChangedEvent"/> which fires from both:
/// - <c>ChangeDamage</c> (predicted melee — deduped to first prediction only), and
/// - <c>HandleState</c> (server-only ranged/environmental — fires once when state diff is detected).
/// </summary>
public sealed class CEDamagePopupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly Color HealColor = Color.FromHex("#44DD44");

    /// <summary>
    /// Maximum horizontal scatter in screen-space pixels.
    /// </summary>
    private const float HorizontalScatterPx = 30f;

    private CEDamagePopupOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new CEDamagePopupOverlay(_cache);
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<CEDamagePopupComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnDamageChanged(Entity<CEDamagePopupComponent> ent, ref CEDamageChangedEvent args)
    {
        if (!_timing.ApplyingState)
            return;

        if (args.DamageDelta == 0)
            return;

        var worldPos = _transform.GetWorldPosition(Transform(ent));

        if (args.DamageIncreased)
        {
            // Compare per-type old vs new to show colored numbers.
            foreach (var (typeId, newAmount) in args.NewDamage.Types)
            {
                args.OldDamage.Types.TryGetValue(typeId, out var oldAmount);
                var typeDelta = newAmount - oldAmount;

                if (typeDelta <= 0)
                    continue;

                var color = _proto.TryIndex(typeId, out var proto) ? proto.Color : Color.White;
                SpawnPopup(FormatDamageText(typeDelta), color, typeDelta, worldPos);
            }
        }
        else
        {
            var healAmount = -args.DamageDelta;
            SpawnPopup($"+{healAmount}", HealColor, healAmount, worldPos);
        }
    }

    private void SpawnPopup(string text, Color color, int amount, Vector2 worldPos)
    {
        var absAmount = Math.Abs(amount);

        PopupFontSize fontSize;
        fontSize = absAmount switch
        {
            <= 5 => PopupFontSize.Small,
            <= 10 => PopupFontSize.Medium,
            _ => PopupFontSize.Large,
        };

        var entry = new PopupEntry
        {
            WorldPosition = worldPos,
            Text = text,
            Color = color,
            FontSize = fontSize,
            Duration = 1.2f * _random.NextFloat(0.9f, 1.1f),
            RiseHeight = 0.5f * _random.NextFloat(0.9f, 1.1f),
            ScreenXOffset = _random.NextFloat(-HorizontalScatterPx, HorizontalScatterPx),
        };

        _overlay.Entries.Add(entry);
    }

    private static string FormatDamageText(int amount)
    {
        return amount switch
        {
            <= 5 => amount.ToString(),
            <= 10 => $"{amount}!",
            _ => $"{amount}!!!",
        };
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        for (var i = _overlay.Entries.Count - 1; i >= 0; i--)
        {
            var entry = _overlay.Entries[i];
            entry.Elapsed += frameTime;

            if (entry.Elapsed >= entry.Duration)
            {
                _overlay.Entries.RemoveSwap(i);
            }
        }
    }
}
