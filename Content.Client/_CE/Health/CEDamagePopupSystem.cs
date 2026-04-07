using System.Numerics;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Examine;
using Robust.Client.Graphics;
using Robust.Client.Player;
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
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly Color HealColor = Color.FromHex("#44DD44");

    /// <summary>
    /// Maximum horizontal scatter in screen-space pixels.
    /// </summary>
    private const float HorizontalScatterPx = 30f;

    /// <summary>
    /// Tracks entities that recently had a predicted popup, so HandleState
    /// confirmations don't produce a duplicate.
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _predictedPopups = new();

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
        if (args.DamageDelta == 0)
            return;

        if (args.Predicted)
        {
            // Predicted (from ChangeDamage): track entity so HandleState
            // confirmations don't produce duplicates. Only show on first prediction tick.
            _predictedPopups[ent.Owner] = _timing.CurTime;

            if (!_timing.IsFirstTimePredicted)
                return;
        }
        else
        {
            // Non-predicted (from HandleState): skip if this entity has active
            // predicted damage. HandleState fires every frame during the prediction
            // window as the component flips between predicted and server values.
            if (_predictedPopups.ContainsKey(ent.Owner))
                return;
        }

        // FOV / occlusion check: don't show popups for entities behind walls.
        if (_player.LocalEntity is { } localPlayer)
        {
            var entityMapPos = _transform.GetMapCoordinates(Transform(ent));
            var playerMapPos = _transform.GetMapCoordinates(Transform(localPlayer));

            if (entityMapPos.MapId != playerMapPos.MapId)
                return;

            var dist = (entityMapPos.Position - playerMapPos.Position).Length();
            if (!_examine.InRangeUnOccluded(
                    playerMapPos,
                    entityMapPos,
                    dist,
                    e => e == ent.Owner || e == localPlayer))
            {
                return;
            }
        }

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

        var fontSize = absAmount switch
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
            Duration = 1.2f * _random.NextFloat(0.7f, 1.3f),
            RiseHeight = 1f * _random.NextFloat(0.7f, 1.3f),
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

        // Clean up stale prediction tracking entries (server never confirmed).
        if (_predictedPopups.Count > 0)
        {
            var now = _timing.CurTime;
            // Can't modify dict during foreach — collect keys to remove first.
            List<EntityUid>? toRemove = null;
            foreach (var (uid, time) in _predictedPopups)
            {
                if (now - time > TimeSpan.FromSeconds(2))
                    (toRemove ??= new List<EntityUid>()).Add(uid);
            }

            if (toRemove != null)
            {
                foreach (var uid in toRemove)
                {
                    _predictedPopups.Remove(uid);
                }
            }
        }

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
