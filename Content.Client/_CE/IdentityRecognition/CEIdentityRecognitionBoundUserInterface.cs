using Content.Shared._CE.IdentityRecognition;
using Content.Shared.Labels.Components;
using Content.Shared.Mind.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._CE.IdentityRecognition;

public sealed class CEIdentityRecognitionBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    [ViewVariables]
    private CERememberNameWindow? _window;

    private NetEntity? _rememberedTarget;

    public CEIdentityRecognitionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CERememberNameWindow>();

        if (_entManager.TryGetComponent(Owner, out HandLabelerComponent? labeler))
        {
            _window.SetMaxLabelLength(labeler!.MaxLabelChars);
        }

        _window.OnRememberedNameChanged += OnLabelChanged;
        Reload();
    }

    private void OnLabelChanged(string newLabel)
    {
        if (_rememberedTarget is null)
            return;

        // Focus moment
        var currentName = CurrentName();

        if (currentName is not null && currentName.Equals(newLabel))
            return;

        SendPredictedMessage(new CERememberedNameChangedMessage(newLabel, _rememberedTarget.Value));
    }

    public void Reload()
    {
        if (_window is null)
            return;

        var currentName = CurrentName();

        if (currentName is null)
            return;

        _window.SetCurrentLabel(currentName);
    }

    private string? CurrentName()
    {
        if (_rememberedTarget is null)
            return null;
        if (!_entManager.TryGetComponent<MindContainerComponent>(_player.LocalEntity, out var mindContainer))
            return null;
        if (!_entManager.TryGetComponent<CERememberedNamesComponent>(mindContainer.Mind, out var knownNames))
            return null;

        var netId = _rememberedTarget.Value.Id;
        return knownNames.Names.GetValueOrDefault(netId);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window is null)
            return;

        switch (state)
        {
            case CERememberNameUiState rememberNameUiState:
                _rememberedTarget = rememberNameUiState.Target;

                var currentName = CurrentName();
                if (currentName is not null)
                    _window.SetCurrentLabel(currentName);
                break;
        }
    }
}
