using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects.TileVFX;

/// <summary>
/// Attach to a status-effect entity prototype to spawn VFX / play audio
/// on key lifecycle moments: first applied, removed, stacks gained, stacks lost.
/// </summary>
[RegisterComponent, NetworkedComponent]
[EntityCategory("Spawner")]
public sealed partial class CETileEffectVFXComponent : Component
{
    [DataField]
    public EntProtoId? OnAppliedVfx;

    [DataField]
    public SoundSpecifier? OnAppliedSound;

    [DataField]
    public EntProtoId? OnRemovedVfx;

    [DataField]
    public SoundSpecifier? OnRemovedSound;

    [DataField]
    public EntProtoId? OnStacksAddedVfx;

    [DataField]
    public SoundSpecifier? OnStacksAddedSound;

    [DataField]
    public EntProtoId? OnStacksRemovedVfx;

    [DataField]
    public SoundSpecifier? OnStacksRemovedSound;

    /// <summary>
    /// World-space radius used to group tiles into a sound sector.
    /// Only one sound of the same type will play per sector per second.
    /// </summary>
    [DataField]
    public float MergeSoundRange = 5f;
}
