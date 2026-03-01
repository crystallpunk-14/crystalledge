using System.Linq;
using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Skills.Blessing;

public sealed partial class CEBlessingSystem : CESharedBlessingSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CERandomBlessingComponent, MapInitEvent>(OnRandomInit);
    }

    private void OnRandomInit(Entity<CERandomBlessingComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<CEBlessingComponent>(ent, out var blessing))
            return;

        if (blessing.Skill is not null)
            return;

        var skills = _proto.EnumeratePrototypes<CESkillPrototype>().ToList();
        if (skills.Count == 0)
            return;

        var skill = _random.Pick(skills);
        blessing.Skill = skill;
        Dirty(ent.Owner, blessing);
    }
}
