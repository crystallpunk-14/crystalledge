using Content.Shared._CE.GOAP.Components;

namespace Content.Client._CE.Music;

/// <summary>
/// Intensity controller: raises the ambient music to its battle layer while the local player is being
/// hunted (has <see cref="CEGOAPTargetComponent"/>), lingering briefly after the threat clears. Drives
/// only intensity via the engine's <see cref="CEAmbientMusicSystem.SetIntense"/>; never selects a theme.
/// </summary>
public sealed partial class CEAmbientMusicSystem
{
    // Threat intensity stays at 1 for this long after the targeting component is removed.
    private const float ThreatLingerSeconds = 6f;
    private TimeSpan _threatExpireTime = TimeSpan.Zero;
    private bool _isThreatActive;

    /// <summary>
    /// Polled from <see cref="Update"/> in the main partial.
    /// </summary>
    private void UpdateThreatIntensity()
    {
        var localPlayer = _player.LocalEntity;
        var isTargeted = localPlayer.HasValue && HasComp<CEGOAPTargetComponent>(localPlayer.Value);

        if (isTargeted)
        {
            _threatExpireTime = _timing.CurTime + TimeSpan.FromSeconds(ThreatLingerSeconds);
            if (!_isThreatActive)
            {
                _isThreatActive = true;
                if (_currentProtoId != null)
                    SetIntense(1);
            }
        }
        else if (_isThreatActive)
        {
            if (_timing.CurTime >= _threatExpireTime)
            {
                _isThreatActive = false;
                if (_currentProtoId != null)
                    SetIntense(0);
            }
        }
    }
}
