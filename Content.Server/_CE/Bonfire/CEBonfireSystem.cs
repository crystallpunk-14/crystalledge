using Content.Shared._CE.Bonfire;

namespace Content.Server._CE.Bonfire;

/// <summary>
/// Server-side bonfire system. All interaction logic lives in <see cref="CESharedBonfireSystem"/>;
/// this sealed class exists so the shared abstract base can be registered on the server.
/// </summary>
public sealed class CEBonfireSystem : CESharedBonfireSystem;
