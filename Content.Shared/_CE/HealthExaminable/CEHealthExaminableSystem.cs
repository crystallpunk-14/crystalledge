using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._CE.HealthExaminable;

public sealed partial class CEHealthExaminableSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private CESharedDamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEHealthExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, CEHealthExaminableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        // as to not show health examine verbs if target is not damageable
        if (!TryComp<CEDamageableComponent>(uid, out var _))
            return;

        var detailsRange = _examineSystem.IsInDetailsRange(args.User, uid);

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var markup = CreateMarkup(uid, component);
                _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
            },
            Text = Loc.GetString("health-examinable-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("health-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    public FormattedMessage CreateMarkup(EntityUid uid, CEHealthExaminableComponent component)
    {
        var msg = new FormattedMessage();
        var baseLocStr = $"health-examinable-{component.LocPrefix}";

        var healthInfo = _damageable.GetHealthInfo(uid);

        // REVIEW: Will this component be applied to structures/destructibles too? If so, then this entire method will need a few changes.
        if (!healthInfo.HasMobState)
            return msg;

        var closest = FixedPoint2.MaxValue;
        var chosenLocStr = string.Empty;
        var ratio = healthInfo.Ratio * 100;

        baseLocStr = $"{baseLocStr}-{(healthInfo.Critical ? "critical-" : "")}";

        foreach (var threshold in component.Thresholds)
        {
            var str = $"{baseLocStr}{threshold}";
            var tempLocStr = Loc.GetString(str, ("target", Identity.Entity(uid, EntityManager)));

            // string doesn't exist in localization (loc.getstring returns the used messageId; in this case, str)
            if (tempLocStr == str)
                continue;

            if (ratio <= threshold && threshold <= closest)
            {
                chosenLocStr = tempLocStr;
                closest = threshold;
            }
        }
        msg.AddMarkupOrThrow(chosenLocStr);

        if (msg.IsEmpty)
            msg.AddMarkupOrThrow(Loc.GetString($"health-examinable-{component.LocPrefix}-100")); // use 100% health in case somehow the message came up empty

        // Anything else want to add on to this?
        RaiseLocalEvent(uid, new CEHealthBeingExaminedEvent(msg), true);

        return msg;
    }
}

/// <summary>
///     A class raised on an entity whose health is being examined
///     in order to add special text that is not handled by the
///     damage thresholds.
/// </summary>
public sealed class CEHealthBeingExaminedEvent(FormattedMessage message)
{
    public FormattedMessage Message = message;
}
