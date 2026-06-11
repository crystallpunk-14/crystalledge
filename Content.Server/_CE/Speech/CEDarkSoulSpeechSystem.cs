using System.Numerics;
using System.Text;
using Content.Server.Chat.Systems;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP.Components;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CE.Speech;

public sealed partial class CEDarkSoulSpeechSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    [Dependency] private EntityQuery<CEDarkSoulSpeechComponent> _soulQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private EntityQuery<CEDarkSoulSpeechAccentComponent> _accentQuery = default!;
    [Dependency] private EntityQuery<CEGOAPComponent> _goapQuery = default!;

    private static readonly char[] GhostAlphabet =
    [
        'ä', 'ã', 'ç', 'ø', 'ђ', 'œ', 'Ї', 'Ћ', 'ў', 'ž', 'ö', 'є', 'þ',
        'ï', 'ñ', 'ë', 'â', 'ô', 'û', 'î', 'ê', 'ù', 'ü',
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEDarkSoulSpeechComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CEDarkSoulSpeechComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<CEDarkSoulSpeechComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CEDarkSoulSpeechComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerAttached(EntityUid uid, CEDarkSoulSpeechComponent comp, PlayerAttachedEvent args)
    {
        comp.IsPlayerControlled = true;
        if (comp.InConversation)
            EndConversation(uid, comp);
    }

    private void OnPlayerDetached(EntityUid uid, CEDarkSoulSpeechComponent comp, PlayerDetachedEvent args)
    {
        comp.IsPlayerControlled = false;
        comp.NextSpeechAt = _timing.CurTime + RandomInterval(comp);
    }

    private void OnInit(EntityUid uid, CEDarkSoulSpeechComponent comp, ComponentInit args)
    {
        comp.NextSpeechAt = _timing.CurTime + RandomInterval(comp);
    }

    private void OnListen(Entity<CEDarkSoulSpeechComponent> ent, ref ListenEvent args)
    {
        if (args.Message.Contains('~') || _soulQuery.HasComponent(args.Source))
            return;

        foreach (var word in args.Message.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = word.Trim('.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '-', '~');
            if (clean.Length >= ent.Comp.MinWordLength)
                ent.Comp.HeardWords.Add(clean);
        }

        while (ent.Comp.HeardWords.Count > ent.Comp.MaxHeardWords)
        {
            ent.Comp.HeardWords.Remove(_random.Pick(new List<string>(ent.Comp.HeardWords)));
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<CEDarkSoulSpeechComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsPlayerControlled)
                continue;

            if (comp.InConversation)
            {
                // Timeout: partner hasn't responded in MaxSpeakPause seconds
                if (comp.SpeakingUntil != TimeSpan.Zero && _timing.CurTime >= comp.SpeakingUntil)
                    EndConversation(uid, comp);
                else
                    HandleConversationTurn(uid, comp);
            }
            else if (_timing.CurTime >= comp.NextSpeechAt)
            {
                TryStartConversation(uid, comp);
            }
        }
    }

    private void TryStartConversation(EntityUid uid, CEDarkSoulSpeechComponent comp)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform))
        {
            comp.NextSpeechAt = _timing.CurTime + RandomInterval(comp);
            return;
        }

        var soulCandidates = new List<EntityUid>();
        var allCandidates = new List<EntityUid>();

        foreach (var entity in _lookup.GetEntitiesInRange(xform.Coordinates, comp.TargetSearchRadius))
        {
            if (entity == uid || !Exists(entity))
                continue;

            if (_soulQuery.TryGetComponent(entity, out var soulComp)
                && !soulComp.InConversation
                && !soulComp.IsPlayerControlled)
            {
                soulCandidates.Add(entity);
                allCandidates.Add(entity);
            }
            else if (_actorQuery.HasComponent(entity))
            {
                allCandidates.Add(entity);
            }
        }

        if (allCandidates.Count == 0)
        {
            comp.NextSpeechAt = _timing.CurTime + RandomInterval(comp);
            return;
        }

        var partner = soulCandidates.Count > 0 && _random.Prob(comp.ConversationChance)
            ? _random.Pick(soulCandidates)
            : _random.Pick(allCandidates);

        StartConversation(uid, comp, partner);
    }

    private void StartConversation(EntityUid uid, CEDarkSoulSpeechComponent comp, EntityUid partner)
    {
        comp.ConversationPartner = partner;
        comp.ConversationTurnsLeft = comp.ConversationTurnPairs * 2;
        comp.IsMyTurn = true;
        comp.NextTurnAt = _timing.CurTime;
        comp.SpeakingUntil = TimeSpan.Zero;
        StopMovement(uid);

        if (_soulQuery.TryGetComponent(partner, out var partnerComp))
        {
            partnerComp.ConversationPartner = uid;
            partnerComp.IsMyTurn = false;
            partnerComp.ConversationTurnsLeft = comp.ConversationTurnsLeft;
            partnerComp.SpeakingUntil = TimeSpan.Zero;
            StopMovement(partner);
        }
    }

    private void HandleConversationTurn(EntityUid uid, CEDarkSoulSpeechComponent comp)
    {
        if (!comp.IsMyTurn || _timing.CurTime < comp.NextTurnAt)
            return;

        if (comp.ConversationPartner is not { } partner || !Exists(partner))
        {
            EndConversation(uid, comp);
            return;
        }

        SpeakTo(uid, comp, partner);

        comp.ConversationTurnsLeft--;
        comp.IsMyTurn = false;
        // Reset timeout: partner has MaxSpeakPause seconds to respond
        comp.SpeakingUntil = _timing.CurTime + TimeSpan.FromSeconds(comp.MaxSpeakPause);

        if (comp.ConversationTurnsLeft <= 0)
        {
            EndConversation(uid, comp);
            return;
        }

        if (_soulQuery.TryGetComponent(partner, out var partnerComp))
        {
            partnerComp.IsMyTurn = true;
            partnerComp.NextTurnAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(partnerComp.MinTurnDelay, partnerComp.MaxTurnDelay));
        }
        else
        {
            // Partner has no soul (player) — soul monologues
            comp.IsMyTurn = true;
            comp.NextTurnAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(comp.MinTurnDelay, comp.MaxTurnDelay));
        }
    }

    private void SpeakTo(EntityUid uid, CEDarkSoulSpeechComponent comp, EntityUid target)
    {
        RotateFaceTo(uid, target);
        var phrase = GenerateGhostPhrase(comp, BuildContextWords(uid, comp));
        _chat.TrySendInGameICMessage(uid, phrase, InGameICChatType.Speak, ChatTransmitRange.HideChat);
    }

    private void EndConversation(EntityUid uid, CEDarkSoulSpeechComponent comp)
    {
        if (comp.ConversationPartner is { } partner && _soulQuery.TryGetComponent(partner, out var partnerComp))
        {
            partnerComp.ConversationPartner = null;
            partnerComp.IsMyTurn = false;
            partnerComp.ConversationTurnsLeft = 0;
            partnerComp.SpeakingUntil = TimeSpan.Zero;
            ResumeMovement(partner);
            partnerComp.NextSpeechAt = _timing.CurTime + RandomInterval(partnerComp);
        }

        comp.ConversationPartner = null;
        comp.IsMyTurn = false;
        comp.ConversationTurnsLeft = 0;
        comp.SpeakingUntil = TimeSpan.Zero;
        ResumeMovement(uid);
        comp.NextSpeechAt = _timing.CurTime + RandomInterval(comp);
    }

    private void StopMovement(EntityUid uid)
    {
        if (_goapQuery.TryGetComponent(uid, out var goap))
            goap.WorldState["SoulIsSpeaking"] = true;
        _steering.Unregister(uid);
    }

    private void ResumeMovement(EntityUid uid)
    {
        if (_goapQuery.TryGetComponent(uid, out var goap))
            goap.WorldState.Remove("SoulIsSpeaking");
    }

    private void RotateFaceTo(EntityUid uid, EntityUid target)
    {
        if (!_xformQuery.TryGetComponent(uid, out var xform) ||
            !_xformQuery.TryGetComponent(target, out var targetXform))
            return;

        var dir = _xforms.GetWorldPosition(targetXform) - _xforms.GetWorldPosition(xform);
        if (dir == Vector2.Zero)
            return;

        _xforms.SetLocalRotationNoLerp(uid, dir.ToWorldAngle());
    }

    private string GenerateGhostPhrase(CEDarkSoulSpeechComponent comp, HashSet<string> contextWords)
    {
        var wordCount = _random.Next(comp.MinWords, comp.MaxWords + 1);
        var words = new List<string>(wordCount);
        var contextList = new List<string>(contextWords);

        var maxRealWords = Math.Min(2, wordCount - 1);
        var realWordsUsed = 0;

        for (var i = 0; i < wordCount; i++)
        {
            var canUseReal = contextList.Count > 0 && realWordsUsed < maxRealWords;
            if (canUseReal && _random.Prob(comp.WordChance))
            {
                var picked = _random.Pick(contextList);
                contextList.Remove(picked);
                words.Add(picked);
                realWordsUsed++;
            }
            else
            {
                var len = _random.Next(2, 8);
                var sb = new StringBuilder(len);
                for (var j = 0; j < len; j++)
                    sb.Append(_random.Pick(GhostAlphabet));
                words.Add(sb.ToString());
            }
        }

        var phrase = string.Join(' ', words);
        var r = _random.NextFloat();
        if (r < 0.15f)
            phrase += "!";
        else if (r < 0.35f)
            phrase += "?";
        return phrase;
    }

    private HashSet<string> BuildContextWords(EntityUid uid, CEDarkSoulSpeechComponent comp)
    {
        var words = new HashSet<string>(comp.HeardWords);

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return words;

        foreach (var entity in _lookup.GetEntitiesInRange(xform.Coordinates, comp.TargetSearchRadius))
        {
            if (_actorQuery.HasComponent(entity))
                words.Add(Name(entity));
        }

        foreach (var entity in _lookup.GetEntitiesInRange(xform.Coordinates, comp.AccentPickupRadius))
        {
            if (_accentQuery.HasComponent(entity))
                words.Add(Name(entity));
        }

        return words;
    }

    private TimeSpan RandomInterval(CEDarkSoulSpeechComponent comp)
        => TimeSpan.FromSeconds(_random.NextFloat(comp.MinSpeechInterval, comp.MaxSpeechInterval));
}
