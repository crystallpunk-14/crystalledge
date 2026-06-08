using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server._CE.CBT;

public sealed partial class CEClosedBetaTestSystem : EntitySystem
{
    [Dependency] private IConsoleHost _consoleHost = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private TimeSpan _nextUpdateTime = TimeSpan.Zero;
    private readonly TimeSpan _updateFrequency = TimeSpan.FromSeconds(60f);

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _enabled = _cfg.GetCVar(CCVars.CEClosedBetaTest);
        _cfg.OnValueChanged(CCVars.CEClosedBetaTest,
            _ => { _enabled = _cfg.GetCVar(CCVars.CEClosedBetaTest); },
            true);
    }

    // Вы можете сказать: Эд, ты ебанулся? Это же лютый щиткод!
    // И я вам отвечу: Да. Но сама система ограничения времени работы сервера - временная штука на этап разработки, которая будет удалена.
    // Мне просто лень каждый раз запускать и выключать сервер ручками.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enabled || _timing.CurTime < _nextUpdateTime)
            return;

        _nextUpdateTime = _timing.CurTime + _updateFrequency;
        var now = DateTime.UtcNow;

        LanguageRule(now);
        LimitPlaytimeRule(now);
        ApplyAnnouncements(now);
    }

    private void LanguageRule(DateTime now)
    {
        var isWeekend = now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (!isWeekend)
            return;

        var curLang = _cfg.GetCVar(CCVars.ServerLanguage);
        var isSaturday = now.DayOfWeek == DayOfWeek.Saturday;

        if (isSaturday && curLang != "ru-RU")
        {
            _cfg.SetCVar(CCVars.ServerLanguage, "ru-RU");

            _chatSystem.DispatchGlobalAnnouncement(
                "WARNING: The server changes its language to Russian. For the changes to apply to your device, reconnect to the server.",
                announcementSound: new SoundPathSpecifier("/Audio/Effects/beep1.ogg"),
                sender: "Server"
            );
        }
        else if (!isSaturday && curLang != "en-US")
        {
            _cfg.SetCVar(CCVars.ServerLanguage, "en-US");

            _chatSystem.DispatchGlobalAnnouncement(
                "WARNING: The server changes its language to English. For the changes to apply to your device, reconnect to the server.",
                announcementSound: new SoundPathSpecifier("/Audio/Effects/beep1.ogg"),
                sender: "Server"
            );
        }
    }

    private void LimitPlaytimeRule(DateTime now)
    {
        var isWeekend = now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var allowedTime = now.Hour is >= 9 and < 13 or >= 17 and < 21;
        var allowed = isWeekend && allowedTime;

        if (allowed)
        {
            if (_ticker.Paused)
                _ticker.TogglePause();
        }
        else
        {
            if (_ticker.RunLevel == GameRunLevel.InRound)
                _roundEnd.EndRound();

            if (!_ticker.Paused)
                _ticker.TogglePause();
        }
    }

    private void ApplyAnnouncements(DateTime now)
    {
        var isWeekend = now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        if (!isWeekend)
            return;

        var timeMap = new (int Hour, int Minute, Action Action)[]
        {
            (12, 45, () =>
            {
                _chatSystem.DispatchGlobalAnnouncement(
                    Loc.GetString("ce-cbt-close-15m"),
                    announcementSound: new SoundPathSpecifier("/Audio/Effects/beep1.ogg"),
                    sender: "Server"
                );
            }),
            (12, 58, () =>
            {
                _consoleHost.ExecuteCommand("endround");
            }),
            (13, 0, () =>
            {
                _consoleHost.ExecuteCommand("golobby");
            }),
            (20, 45, () =>
            {
                _chatSystem.DispatchGlobalAnnouncement(
                    Loc.GetString("ce-cbt-close-15m"),
                    announcementSound: new SoundPathSpecifier("/Audio/Effects/beep1.ogg"),
                    sender: "Server"
                );
            }),
            (20, 58, () =>
            {
                _consoleHost.ExecuteCommand("endround");
            }),
            (21, 0, () =>
            {
                _consoleHost.ExecuteCommand("golobby");
            }),
        };

        foreach (var (hour, minute, action) in timeMap)
        {
            if (now.Hour == hour && now.Minute == minute)
                action.Invoke();
        }
    }
}
