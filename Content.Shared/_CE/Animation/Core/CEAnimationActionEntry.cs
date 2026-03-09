using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CEAnimationActionEntry
{
    public abstract void Play(EntityManager entManager, EntityUid user, EntityUid? used, Angle angle, float speed, TimeSpan frame, EntityUid? target, EntityCoordinates? position);
}
