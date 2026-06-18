using System;
using Robust.Shared.GameObjects;

namespace Content.Shared._CE.MetaChest;

/// <summary>
/// Marker for a personal shadow storage entity spawned by CEMetaChestSystem.
/// The entity is invisible, has no physics, and lives parented to the real meta chest (or admin mob).
/// </summary>
[RegisterComponent]
public sealed partial class CEMetaChestPersonalComponent : Component
{
    /// <summary>UserId of the player whose items are stored here.</summary>
    public Guid TargetUserId;

    /// <summary>UserId of the player or admin who opened this shadow (controls UI access).</summary>
    public Guid ActorUserId;

    public int ChestSlot;

    /// <summary>True when opened by an admin for another player's items.</summary>
    public bool IsAdminSession;
}
