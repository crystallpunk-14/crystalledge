using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._CE.IdentityRecognition;

public abstract class CESharedIdentityRecognitionSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEUnknownIdentityComponent, GetVerbsEvent<Verb>>(OnUnknownIdentityVerb);
        SubscribeLocalEvent<CEUnknownIdentityComponent, ExaminedEvent>(OnExaminedEvent);

        SubscribeLocalEvent<MindContainerComponent, CERememberedNameChangedMessage>(OnRememberedNameChanged);

        SubscribeLocalEvent<CERememberedNamesComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CERememberedNamesComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<MindComponent>(ent, out var mind))
            return;

        if (mind.OwnedEntity is null)
            return;

        if (mind.CharacterName is null)
            return;

        RememberCharacter(ent, GetNetEntity(mind.OwnedEntity.Value), mind.CharacterName);
    }

    private void OnUnknownIdentityVerb(Entity<CEUnknownIdentityComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (HasComp<GhostComponent>(args.User))
            return;

        if(!_mind.TryGetMind(args.User, out var mindId, out var mind))
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        if (args.User == ent.Owner)
            return;

        EnsureComp<CERememberedNamesComponent>(mindId);

        var seeAttemptEv = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(ent.Owner, seeAttemptEv);

        var _args = args;
        var verb = new Verb
        {
            Priority = 2,
            Icon =  new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
            Text = Loc.GetString("ce-remember-name-verb"),
            Disabled = seeAttemptEv.Cancelled,
            Act = () =>
            {
                _uiSystem.SetUiState(_args.User, CERememberNameUiKey.Key, new CERememberNameUiState(GetNetEntity(ent)));
                _uiSystem.TryToggleUi(_args.User, CERememberNameUiKey.Key, actor.PlayerSession);
            },
        };
        args.Verbs.Add(verb);
    }

    private void OnExaminedEvent(Entity<CEUnknownIdentityComponent> ent, ref ExaminedEvent args)
    {
        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(ent.Owner, ev);

        if (ev.Cancelled)
            return;

        if (!_mind.TryGetMind(args.Examiner, out var mindId, out var mind))
            return;

        if (!TryComp<CERememberedNamesComponent>(mindId, out var knownNames))
            return;

        if (knownNames.Names.TryGetValue(GetNetEntity(ent).Id, out var name))
        {
            args.PushMarkup(Loc.GetString("ce-remember-name-examine", ("name", name)), priority: -1);
        }
    }

    private void OnRememberedNameChanged(Entity<MindContainerComponent> ent, ref CERememberedNameChangedMessage args)
    {
        var mindEntity = ent.Comp.Mind;

        if (mindEntity is null)
            return;

        RememberCharacter(mindEntity.Value, args.Target, args.Name);
    }

    private void RememberCharacter(EntityUid mindEntity, NetEntity targetId, string name)
    {
        var knownNames = EnsureComp<CERememberedNamesComponent>(mindEntity);

        knownNames.Names[targetId.Id] = name;
        Dirty(mindEntity, knownNames);
    }
}

[Serializable, NetSerializable]
public sealed class CERememberedNameChangedMessage(string name, NetEntity target) : BoundUserInterfaceMessage
{
    public string Name { get; } = name;
    public NetEntity Target { get; } = target;
}

[Serializable, NetSerializable]
public enum CERememberNameUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CERememberNameUiState(NetEntity target) : BoundUserInterfaceState
{
    public NetEntity Target = target;
}
