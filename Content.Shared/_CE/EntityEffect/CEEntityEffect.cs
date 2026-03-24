using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._CE.EntityEffect;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CEEntityEffect
{
    public abstract void Effect(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position);
}
