using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core;
using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Skills.Blessing;

public sealed partial class CEBlessingSystem : CESharedBlessingSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CESharedSkillSystem _skill = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    public override void Initialize()
    {
        base.Initialize();

        InitializeLinking();
        InitializeTrigger();

        SubscribeLocalEvent<CERandomBlessingComponent, MapInitEvent>(OnRandomInit);
    }

    private void OnRandomInit(Entity<CERandomBlessingComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<CEBlessingComponent>(ent, out var blessing))
            return;

        if (blessing.Skill is not null)
            return;

        List<ProtoId<CESkillPrototype>> skills = new();
        foreach (var s in _proto.EnumeratePrototypes<CESkillPrototype>())
        {
            skills.Add(s);
        }
        if (skills.Count == 0)
            return;

        var skill = _random.Pick(skills);
        blessing.Skill = skill;
        Dirty(ent.Owner, blessing);
    }
}
