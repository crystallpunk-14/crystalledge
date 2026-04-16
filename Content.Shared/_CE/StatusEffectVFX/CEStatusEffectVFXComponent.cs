using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffectVFX;

/// <summary>
/// Attach to a status-effect entity prototype to spawn VFX / play audio
/// on key lifecycle moments: first applied, removed, stacks gained, stacks lost.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEStatusEffectVFXComponent : Component
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
}
