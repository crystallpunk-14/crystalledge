using Content.Server.Chat.Systems;
using Content.Server.Instruments;
using Content.Shared._CE.Actions;
using Content.Shared._CE.Actions.Components;
using Content.Shared._CE.Actions2;
using Content.Shared.Actions.Events;
using Content.Shared.Instruments;

namespace Content.Server._CE.Actions;

public sealed partial class CEAction2System : CESharedAction2System
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEActionRequiredMusicToolComponent, ActionAttemptEvent>(OnActionMusicAttempt);
    }

    private void OnActionMusicAttempt(Entity<CEActionRequiredMusicToolComponent> ent, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var passed = false;
        var query = EntityQueryEnumerator<ActiveInstrumentComponent, InstrumentComponent>();
        while (query.MoveNext(out var uid, out var active, out var instrument))
        {
            if (!instrument.Playing)
                continue;

            if (Transform(uid).ParentUid != args.User)
                continue;

            passed = true;
            break;
        }

        if (passed)
            return;

        Popup.PopupClient(Loc.GetString("ce-magic-music-aspect"), args.User, args.User);
        args.Cancelled = true;
    }
}
