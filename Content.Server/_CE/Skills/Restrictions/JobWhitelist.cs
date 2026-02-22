using Content.Shared._CE.Skills.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skills.Restrictions;

public sealed partial class JobWhitelist : CESkillRestriction
{
    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();
    public override bool Check(IEntityManager entManager, EntityUid target)
    {

    }
}
