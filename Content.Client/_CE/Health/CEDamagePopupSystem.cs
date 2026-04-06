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
/// Subscribes to <see cref="CEDamageChangedEvent"/> raised for entities with <see cref="CEDamagePopupComponent"/>.
/// </summary>
public sealed class CEDamagePopupSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color HealColor = Color.FromHex("#44DD44");

    /// <summary>
    /// Maximum horizontal scatter in screen-space pixels.
    /// </summary>
    private const float HorizontalScatterPx = 30f;

    private const float BaseYOffset = 0.5f;

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

    /// <summary>
    /// Handles predicted damage events (from TakeDamage/Heal during client prediction).
    /// These have proper DamageDelta and may have per-type DamageSpecifier.
    /// </summary>
    private void OnDamageChanged(Entity<CEDamagePopupComponent> ent, ref CEDamageChangedEvent args)
    {
        // Mirror engine pattern (see AudioSystem): state-sync events bypass the prediction gate.
        if (!_timing.ApplyingState && !_timing.IsFirstTimePredicted)
            return;

        if (args.DamageDelta == 0)
            return;

        var worldPos = _transform.GetWorldPosition(Transform(ent));
        SpawnFromEvent(args, worldPos);
    }

    private void SpawnFromEvent(CEDamageChangedEvent args, Vector2 worldPos)
    {
        if (args.DamageIncreased)
        {
            if (args.DamageSpecifier is { } spec)
            {
                SpawnDamagePopups(spec.Types, worldPos);
            }
            else
            {
                SpawnSinglePopup(
                    FormatDamageText(args.DamageDelta),
                    Color.White,
                    args.DamageDelta,
                    worldPos);
            }
        }
        else
        {
            var healAmount = -args.DamageDelta;
            SpawnSinglePopup($"+{healAmount}", HealColor, healAmount, worldPos);
        }
    }

    private void SpawnDamagePopups(
        Dictionary<ProtoId<CEDamageTypePrototype>, int> types,
        Vector2 worldPos)
    {
        foreach (var (typeId, amount) in types)
        {
            if (amount <= 0)
                continue;

            var color = _proto.TryIndex(typeId, out var proto) ? proto.Color : Color.White;
            var text = FormatDamageText(amount);

            var spawnPos = worldPos + new Vector2(0f, BaseYOffset);
            SpawnSinglePopup(text, color, amount, spawnPos);
        }
    }

    private void SpawnSinglePopup(string text, Color color, int amount, Vector2 worldPos)
    {
        var absAmount = Math.Abs(amount);

        PopupFontSize fontSize;
        float duration;
        float riseHeight;

        if (absAmount <= 5)
        {
            fontSize = PopupFontSize.Small;
            duration = 0.6f;
            riseHeight = 0.35f;
        }
        else if (absAmount <= 10)
        {
            fontSize = PopupFontSize.Medium;
            duration = 0.8f;
            riseHeight = 0.45f;
        }
        else
        {
            fontSize = PopupFontSize.Large;
            duration = 1.0f;
            riseHeight = 0.55f;
        }

        // Randomize duration and rise height slightly so overlapping numbers don't move in lockstep.
        duration *= _random.NextFloat(0.9f, 1.1f);
        riseHeight *= _random.NextFloat(0.85f, 1.15f);

        var entry = new PopupEntry
        {
            WorldPosition = worldPos,
            Text = text,
            Color = color,
            FontSize = fontSize,
            Duration = duration,
            RiseHeight = riseHeight,
            ScreenXOffset = _random.NextFloat(-HorizontalScatterPx, HorizontalScatterPx),
        };

        _overlay.Entries.Add(entry);
    }

    private static string FormatDamageText(int amount)
    {
        if (amount <= 5)
            return amount.ToString();

        if (amount <= 10)
            return $"{amount}!";

        return $"{amount}!!!";
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
