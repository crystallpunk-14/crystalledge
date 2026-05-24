using Content.Shared._CE.IdentityRecognition;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind.Components;

namespace Content.Client._CE.IdentityRecognition;

public sealed partial class CEClientIdentityRecognitionSystem : CESharedIdentityRecognitionSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindContainerComponent, CEClientTransformNameEvent>(OnTransformSpeakerName);
    }

    private void OnTransformSpeakerName(Entity<MindContainerComponent> ent, ref CEClientTransformNameEvent args)
    {
        if (args.Handled)
            return;

        var mindEntity = ent.Comp.Mind;
        if (mindEntity is null)
            return;

        TryComp<CERememberedNamesComponent>(mindEntity.Value, out var knownNames);

        var speaker = GetEntity(args.Speaker);

        if (speaker == ent.Owner)
            return;

        if (knownNames is not null && knownNames.Names.TryGetValue(args.Speaker.Id, out var name))
        {
            args.Name = name;
        }
        else
        {
            args.Name = Identity.Name(speaker, EntityManager, ent);
        }
        args.Handled = true;
    }
}

public sealed class CEClientTransformNameEvent(NetEntity speaker) : EntityEventArgs
{
    public NetEntity Speaker = speaker;

    public string Name = string.Empty;

    public bool Handled { get; set; }
}
