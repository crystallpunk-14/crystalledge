using Content.Server.Chat.Systems;
using Content.Shared._CE.Actions;

namespace Content.Server._CE.Actions;

public sealed partial class CEActionSystem : CESharedActionSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeDoAfter();
    }
}
