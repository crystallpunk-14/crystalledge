using Content.Shared._CE.Achievements.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Achievements.Components;

[RegisterComponent]
public sealed partial class CEAchievementOnDestructComponent : Component
{
    [DataField(required: true)]
    public ProtoId<CEAchievementPrototype> Achievement;
}
