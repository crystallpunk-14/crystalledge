using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether the CE GOAP AI system is enabled.
    /// </summary>
    public static readonly CVarDef<bool> CEGOAPEnabled =
        CVarDef.Create("ce.goap.enabled", true);

    /// <summary>
    /// Maximum number of GOAP agents updated per tick.
    /// </summary>
    public static readonly CVarDef<int> CEGOAPMaxUpdates =
        CVarDef.Create("ce.goap.max_updates", 128);

    /// <summary>
    /// Interval in seconds between sensor updates for GOAP agents.
    /// </summary>
    public static readonly CVarDef<float> CEGOAPSensorInterval =
        CVarDef.Create("ce.goap.sensor_interval", 0.2f);
}
