using Content.Server.GameTicking.Events;
using Content.Shared._CE.Procedural;
using Content.Shared.CCVar;

namespace Content.Server._CE.Procedural;

public sealed partial class CEDungeonSystem
{
    private void InitializeCache()
    {
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {

        if (!_configManager.GetCVar(CCVars.ProcgenPreload))
            return;

        // Force all templates to be setup.
        //foreach (var room in _proto.EnumeratePrototypes<CEDungeonRoom3DPrototype>())
        //{
        //    if (!_proto.Resolve(room.ZLevelMap, out var indexedZMap))
        //        continue;
//
        //    foreach (var path in indexedZMap.Maps)
        //    {
        //        GetOrCreateTemplate(path);
        //    }
        //}
    }
}
