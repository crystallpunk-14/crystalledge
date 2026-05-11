using Robust.Shared.GameObjects;

namespace Content.Shared._CE.NPC.Components;

/// <summary>
/// Marks an entity to be ignored by NPC collision avoidance (context steering danger map).
/// Use on entities that are physically passable for mobs (e.g. tile effects like spikes or bushes)
/// that should not be treated as steering obstacles, even though they have hard fixtures.
/// </summary>
[RegisterComponent]
public sealed partial class CENavIgnoreComponent : Component;
