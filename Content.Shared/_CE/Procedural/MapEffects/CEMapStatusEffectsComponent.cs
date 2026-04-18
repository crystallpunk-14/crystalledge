using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Procedural.MapEffects;

/// <summary>
/// When attached to a map entity, applies the listed status effects
/// to every <see cref="Components.CEDungeonPlayerComponent"/> that enters the map
/// and removes them when they leave.
/// </summary>
[RegisterComponent]
public sealed partial class CEMapStatusEffectsComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Effects = new();
}
