using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.Skill.Blessing;

public abstract partial class CESharedBlessingSystem : EntitySystem
{
    [Dependency] private readonly CESharedSkillSystem _skill = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBlessingComponent, ActivateInWorldEvent>(OnActivate);
        InitializeExamine();
    }

    private void OnActivate(Entity<CEBlessingComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!TryComp<CEBlessingReceiverComponent>(args.User, out var receiver))
            return;

        if (ent.Comp.ForPlayer is not null && ent.Comp.ForPlayer != args.User)
            return;

        if (ent.Comp.Skill is null)
            return;

        if (!_skill.TryAddSkill(args.User, ent.Comp.Skill.Value))
            return;

        // Predicted-delete all sibling blessings so they disappear instantly
        foreach (var sibling in ent.Comp.SiblingBlessings)
        {
            if (Exists(sibling))
                PredictedDel(sibling);
        }

        var ev = new CEBlessingClaimedEvent(args.User);
        RaiseLocalEvent(ent.Owner, ref ev);

        PredictedDel(ent.Owner);
    }

    /// <summary>
    ///  Checks if the player can learn the specified skill.
    /// </summary>
    //public bool CanLearnSkill(
    //    EntityUid target,
    //    CESkillPrototype skill,
    //    CESkillStorageComponent? component = null)
    //{
    //    if (!Resolve(target, ref component, false))
    //        return false;
//
    //    // Check if already learned
    //    if (_skill.HaveSkill(target, skill, component))
    //        return false;
//
    //    //Restrictions check
    //    foreach (var req in skill.Restrictions)
    //    {
    //        if (!req.Check(EntityManager, target))
    //            return false;
    //    }
//
    //    return true;
    //}
}
