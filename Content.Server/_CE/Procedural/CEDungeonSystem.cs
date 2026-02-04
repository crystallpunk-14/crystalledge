using Content.Server._CE.ZLevels.Core;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural;

public sealed partial class CEDungeonSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    public override void Initialize()
    {
        base.Initialize();

        InitializeRooms();
    }
}
