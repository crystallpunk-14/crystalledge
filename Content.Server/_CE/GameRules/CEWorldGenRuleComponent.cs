using Content.Shared._CE.WorldGen.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.GameRules;

[RegisterComponent]
public sealed partial class CEWorldGenRuleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CEWorldConfigPrototype> Config;
}
