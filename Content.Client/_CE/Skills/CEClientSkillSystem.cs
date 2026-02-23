using Content.Shared._CE.Skill.Core.Components;
using Robust.Client.Player;

namespace Content.Client._CE.Skills;

public sealed partial class CEClientSkillSystem : Shared._CE.Skill.Core.CESharedSkillSystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

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
