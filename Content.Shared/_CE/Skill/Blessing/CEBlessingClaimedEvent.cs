namespace Content.Shared._CE.Skill.Blessing;

/// <summary>
/// Raised on a blessing entity when a player successfully claims it (takes the skill).
/// Handled by the server to clean up other blessings and mark the player.
/// </summary>
[ByRefEvent]
public readonly struct CEBlessingClaimedEvent(EntityUid player)
{
    public readonly EntityUid Player = player;
}
