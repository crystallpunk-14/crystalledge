using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CEAnimationActionEntry
{
    public abstract void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, TimeSpan frame);
}
