using Content.Shared.Doors;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._CE.Lock;

/// <summary>
/// Prevents entities with a locked <see cref="LockComponent"/> from being opened.
/// Vanilla <see cref="LockSystem"/> only blocks storage; this system extends the
/// same behaviour to doors (and any other entity that raises <see cref="BeforeDoorOpenedEvent"/>).
/// Also shows a "locked" popup and plays the doorknob sound when opening is blocked.
/// </summary>
public sealed partial class CELockBlockDoorSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier DoorknobSound = new SoundCollectionSpecifier("CEDoorknob")
    {
        Params = AudioParams.Default.WithVariation(0.03f).WithVolume(-5f),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LockComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpened);
    }

    private void OnBeforeDoorOpened(Entity<LockComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancel();

        if (args.User is not { } user)
            return;

        _popup.PopupPredicted(
            Loc.GetString("lock-comp-generic-fail", ("target", ent.Owner)),
            ent.Owner,
            user);

        _audio.PlayPredicted(DoorknobSound, ent.Owner, user);
    }
}

