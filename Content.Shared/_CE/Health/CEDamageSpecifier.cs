using System.Linq;
using Content.Shared._CE.Health.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Health;

/// <summary>
/// Specifies damage dealt using CE damage types.
/// Each key is a <see cref="CEDamageTypePrototype"/> ID, each value is the integer damage amount.
/// The total damage applied to health is the sum of all values after modification events.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CEDamageSpecifier
{
    [DataField]
    public Dictionary<ProtoId<CEDamageTypePrototype>, int> Types = new();

    public CEDamageSpecifier()
    {
    }

    public CEDamageSpecifier(ProtoId<CEDamageTypePrototype> type, int amount)
    {
        Types[type] = amount;
    }

    public CEDamageSpecifier(CEDamageSpecifier other)
    {
        Types = new Dictionary<ProtoId<CEDamageTypePrototype>, int>(other.Types);
    }

    /// <summary>
    /// Total damage across all types.
    /// </summary>
    public int Total => Types.Values.Sum();

    public static CEDamageSpecifier operator *(CEDamageSpecifier spec, float multiplier)
    {
        var result = new CEDamageSpecifier();
        foreach (var (type, value) in spec.Types)
            result.Types[type] = (int)(value * multiplier);
        return result;
    }

    public static CEDamageSpecifier operator +(CEDamageSpecifier a, CEDamageSpecifier b)
    {
        var result = new CEDamageSpecifier(a);
        foreach (var (type, value) in b.Types)
        {
            result.Types.TryGetValue(type, out var existing);
            result.Types[type] = existing + value;
        }

        return result;
    }
}
