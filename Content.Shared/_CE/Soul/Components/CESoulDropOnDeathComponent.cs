using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Soul.Components;

/// <summary>
/// Drops a configurable number of soul shards when the entity is destroyed by
/// <see cref="Content.Shared._CE.Health.CEDestructibleSystem"/>. Shards spawn at
/// the death position and receive a random physics impulse, matching the smooth
/// scatter used by CEDestructible loot drops.
/// Drop logic lives in <see cref="CESharedSoulSystem"/> on the server.
/// </summary>
[RegisterComponent]
public sealed partial class CESoulDropOnDeathComponent : Component
{
    /// <summary>
    /// Number of soul shards to drop on death.
    /// </summary>
    [DataField]
    public int Souls;

    /// <summary>
    /// Prototype to spawn for each soul. Defaults to the standard soul shard.
    /// </summary>
    [DataField]
    public EntProtoId Prototype = "CESoulShard";

    /// <summary>
    /// Maximum linear speed (tiles/second) for the scatter impulse. Each shard
    /// receives a random direction and a random speed in <c>[0.5, ScatterSpeed]</c>,
    /// matching the smooth physics-driven scatter used by CEDestructible loot drops.
    /// </summary>
    [DataField]
    public float ScatterSpeed = 15.0f;
}
