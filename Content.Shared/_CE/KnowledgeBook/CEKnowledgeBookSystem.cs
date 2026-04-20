using System.Text;
using Content.Shared._CE.Workbench.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.KnowledgeBook;

public abstract class CESharedKnowledgeBookSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier KnowledgeLearnedSound =
        new SoundPathSpecifier("/Audio/_CE/Effects/knowledge_learned.ogg");

    private readonly EntProtoId _knowledgeVfx = "CEEffectKnowledgeSparks";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEKnowledgeBookComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CEKnowledgeBookComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<CEKnowledgeBookComponent, CEReadKnowledgeBookDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CEKnowledgeBookComponent, ExaminedEvent>(OnExamined);
    }

    private void OnUseInHand(Entity<CEKnowledgeBookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        TryStartDoAfter(ent, args.User, ent.Comp.UseDelay);
        args.Handled = true;
    }

    private void OnGetAltVerbs(Entity<CEKnowledgeBookComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryStartDoAfter(ent, user, ent.Comp.UseDelay),
            Text = Loc.GetString("ce-knowledgebook-verb-read"),
            Priority = 2,
        });
    }

    private bool TryStartDoAfter(Entity<CEKnowledgeBookComponent> ent, EntityUid user, TimeSpan delay)
    {
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            delay,
            new CEReadKnowledgeBookDoAfterEvent(),
            ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    protected virtual void OnDoAfter(Entity<CEKnowledgeBookComponent> ent, ref CEReadKnowledgeBookDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        _audio.PlayPredicted(ent.Comp.UseSound, Transform(ent).Coordinates, args.User);

        if (!_timing.IsFirstTimePredicted)
            return;

        // Play knowledge learned sound globally to this player only
        _audio.PlayGlobal(KnowledgeLearnedSound, args.User);

        // Spawn visual effect on client
        if (_net.IsClient)
            Spawn(_knowledgeVfx, Transform(args.User).Coordinates);
    }

    private void OnExamined(Entity<CEKnowledgeBookComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Recipes.Count == 0)
            return;

        var sb = new StringBuilder();

        sb.Append(Loc.GetString("ce-knowledgebook-examine-header"));
        sb.Append("\n");

        foreach (var recipe in ent.Comp.Recipes)
        {
            sb.Append($"[color=yellow]- {GetRecipeName(recipe)}[/color]\n");
        }

        args.PushMarkup(sb.ToString());
    }

    protected string GetRecipeName(ProtoId<CEWorkbenchRecipePrototype> recipeId)
    {
        if (!_proto.TryIndex(recipeId, out var recipe))
            return recipeId.Id;

        // Get name from result entity
        if (_proto.TryIndex(recipe.Result, out var resultProto))
            return resultProto.Name;

        return recipeId.Id;
    }
}

[Serializable, NetSerializable]
public sealed partial class CEReadKnowledgeBookDoAfterEvent : SimpleDoAfterEvent;
