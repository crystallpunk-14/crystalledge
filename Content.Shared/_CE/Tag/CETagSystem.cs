using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Tag;

public sealed class CETagSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private EntityQuery<CETagComponent> _tagQuery;

    public override void Initialize()
    {
        base.Initialize();

        _tagQuery = GetEntityQuery<CETagComponent>();
    }

    public bool HasTag(EntityUid entityUid, [ForbidLiteral] ProtoId<CETagPrototype> tag)
    {
        return _tagQuery.TryComp(entityUid, out var component) &&
               HasTag(component, tag);
    }

    public bool HasTag(CETagComponent component, [ForbidLiteral] ProtoId<CETagPrototype> tag)
    {
        return component.Tags.Contains(tag);
    }

    public bool HasAllTags(EntityUid entityUid, ProtoId<CETagPrototype> tag) =>
        HasTag(entityUid, tag);
    public bool HasAllTagsArray(CETagComponent component, [ForbidLiteral] ProtoId<CETagPrototype>[] tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (!component.Tags.Contains(tag))
                return false;
        }

        return true;
    }

    public bool HasAllTags(CETagComponent component, [ForbidLiteral] List<ProtoId<CETagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (!component.Tags.Contains(tag))
                return false;
        }

        return true;
    }

    public bool HasAllTags(CETagComponent component, [ForbidLiteral] HashSet<ProtoId<CETagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (!component.Tags.Contains(tag))
                return false;
        }

        return true;
    }

    public bool HasAllTags(CETagComponent component, [ForbidLiteral] IEnumerable<ProtoId<CETagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (!component.Tags.Contains(tag))
                return false;
        }

        return true;
    }

    public bool HasAnyTag(CETagComponent component, [ForbidLiteral] params ProtoId<CETagPrototype>[] tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (component.Tags.Contains(tag))
                return true;
        }

        return false;
    }

    public bool HasAnyTag(CETagComponent component, [ForbidLiteral] HashSet<ProtoId<CETagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (component.Tags.Contains(tag))
                return true;
        }

        return false;
    }

    public bool HasAnyTag(CETagComponent component, [ForbidLiteral] List<ProtoId<CETagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (component.Tags.Contains(tag))
                return true;
        }

        return false;
    }

    public bool HasAnyTag(CETagComponent component, [ForbidLiteral] IEnumerable<ProtoId<CETagPrototype>> tags)
    {
        foreach (var tag in tags)
        {
#if DEBUG
            AssertValidTag(tag);
#endif
            if (component.Tags.Contains(tag))
                return true;
        }

        return false;
    }

    private void AssertValidTag(string id)
    {
        DebugTools.Assert(_proto.HasIndex<CETagPrototype>(id), $"Unknown tag: {id}");
    }
}
