using System.Collections.Generic;
using Content.Shared._CE.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Trading.Components;

[RegisterComponent]
public sealed partial class CETradingCategoryComponent : Component
{
    [DataField]
    public HashSet<ProtoId<CETagPrototype>> Tags = new();
}
