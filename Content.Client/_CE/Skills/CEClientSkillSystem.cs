using Content.Shared._CE.Skills;
using Content.Shared._CE.Skills.Components;
using Content.Shared._CE.Skills.Prototypes;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Skills;

public sealed partial class CEClientSkillSystem : CESharedSkillSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<EntityUid>? OnSkillUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESkillStorageComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<CESkillStorageComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent != _playerManager.LocalEntity)
            return;

        OnSkillUpdate?.Invoke(ent.Owner);
    }

    public void RequestSkillData()
    {
        var localPlayer = _playerManager.LocalEntity;

        if (!HasComp<CESkillStorageComponent>(localPlayer))
            return;

        OnSkillUpdate?.Invoke(localPlayer.Value);
    }
}
