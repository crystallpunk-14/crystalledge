// SPDX-FileCopyrightText: 2026 Mirrorcult
//
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared.Camera;
using Robust.Shared.Noise;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Camera;

/// <summary>
///     Handles sending rotational or translational screenshake to an entity, managing the screenshake commands
///     of every entity currently screenshaking, and setting offset/rotation when updated
/// </summary>
public sealed class CEScreenshakeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    #region Internal

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEScreenshakeComponent, CEGetEyeRotationEvent>(OnGetEyeRotation);
        SubscribeLocalEvent<CEScreenshakeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        SubscribeLocalEvent<CEScreenshakeComponent, EntityUnpausedEvent>(OnEntityUnpaused);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // TODO mirror might make sense to never remove individual commands and only remove the comp if theyre all > calculatedend instead.
        var shakers = EntityQueryEnumerator<EyeComponent, CEScreenshakeComponent>();
        while (shakers.MoveNext(out var ent, out var eye, out var shake))
        {
            if (shake.Commands.Count == 0)
            {
                RemCompDeferred<CEScreenshakeComponent>(ent);
                continue;
            }

            foreach (var command in shake.Commands.ToList())
            {
                // handle removing old commands
                if (_timing.CurTime < command.CalculatedEnd)
                    continue;
                shake.Commands.Remove(command);
                Dirty(ent, shake);
            }
        }
    }

    private void OnGetEyeOffset(Entity<CEScreenshakeComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (!TryComp<EyeComponent>(ent, out var eye))
            return;

        var noise = new FastNoiseLite(67);
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

        var accumulatedOffset = Vector2.Zero;
        var maxOffset = new Vector2(0.15f, 0.15f);
        foreach (var command in ent.Comp.Commands)
        {
            if (command.Translational == null)
                continue;

            var trauma =
                CalculateTraumaValueForCurrentTime(command.Translational, command.Start);
            if (trauma <= 0)
                continue;

            noise.SetFrequency(command.Translational.Frequency);

            // using the starst c ommand for y pos kinda doesnt work in the case where multiple shakes get sent at the same time
            // and the shakes are identical otherwise. but like dont do that or something idk
            var offsetX = (maxOffset.X * trauma) * noise.GetNoise((float)_timing.RealTime.TotalMilliseconds, (float)command.Start.TotalMilliseconds);
            noise.SetSeed(68);
            var offsetY = (maxOffset.Y * trauma) * noise.GetNoise((float)_timing.RealTime.TotalMilliseconds, (float)command.Start.TotalMilliseconds);
            noise.SetSeed(67);
            accumulatedOffset += new Vector2(offsetX, offsetY);
        }

        args.Offset += accumulatedOffset;
    }

    private void OnGetEyeRotation(Entity<CEScreenshakeComponent> ent, ref CEGetEyeRotationEvent args)
    {
        if (!TryComp<EyeComponent>(ent, out var eye))
            return;

        var noise = new FastNoiseLite(67 + 420); // Epic bacon
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

        // 20deg max
        var accumulatedAngle = Angle.Zero;
        var maxAngleDegrees = 20f;
        foreach (var command in ent.Comp.Commands)
        {
            if (command.Rotational == null)
                continue;

            var trauma =
                CalculateTraumaValueForCurrentTime(command.Rotational, command.Start);
            if (trauma <= 0)
                continue;

            noise.SetFrequency(command.Rotational.Frequency);

            var angle = (maxAngleDegrees * trauma) * noise.GetNoise((float)_timing.RealTime.TotalMilliseconds, (float)command.Start.TotalMilliseconds);
            accumulatedAngle += Angle.FromDegrees(angle);
        }

        // TODO ughhh this shit breaks with something idk
        args.Rotation += accumulatedAngle;
    }

    private void OnEntityUnpaused(Entity<CEScreenshakeComponent> ent, ref EntityUnpausedEvent args)
    {
        // rebuild screenshake commands but with offset times
        var newSet = new HashSet<CEScreenshakeCommand>();
        foreach (var command in ent.Comp.Commands)
        {
            var newCommand = command with
            {
                CalculatedEnd = command.CalculatedEnd + args.PausedTime,
                Start = command.Start + args.PausedTime,
            };

            newSet.Add(newCommand);
        }

        ent.Comp.Commands = newSet;
        Dirty(ent);
    }

    /// <summary>
    ///     Calculates when both traumas will be at least = 0 given the decay rate and start time.
    /// </summary>
    private TimeSpan CalculateEndTimeForCommand(Entity<CEScreenshakeComponent> ent, CEScreenshakeParameters? translation, CEScreenshakeParameters? rotation, TimeSpan start)
    {
        // https://www.desmos.com/calculator/optip8eucx
        var secsUntilRotationalEnd = rotation != null ? MathF.Sqrt(rotation.Trauma / rotation.DecayRate) : 0f;
        var secsUntilTranslationalEnd = translation != null ? MathF.Sqrt(translation.Trauma / translation.DecayRate) : 0f;
        var larger = secsUntilTranslationalEnd >= secsUntilRotationalEnd
            ? secsUntilTranslationalEnd
            : secsUntilRotationalEnd;

        return start + TimeSpan.FromSeconds(larger);
    }

    /// <summary>
    ///     Gets the trauma value for the current time, given the decay rate and start time.
    /// </summary>
    private float CalculateTraumaValueForCurrentTime(CEScreenshakeParameters parameters, TimeSpan start)
    {
        var timeDiff = _timing.CurTime - start;

        // erm
        if (timeDiff < TimeSpan.Zero)
            return 0f;

        // trauma decays quadratically with seconds passed
        // https://www.desmos.com/calculator/optip8eucx
        var totalSecsSquared = (float) (timeDiff.TotalSeconds * timeDiff.TotalSeconds);
        return -totalSecsSquared * parameters.DecayRate + parameters.Trauma;
    }

    #endregion

    #region Public API

    public void Screenshake(EntityUid uid, CEScreenshakeParameters? translation, CEScreenshakeParameters? rotation)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!HasComp<EyeComponent>(uid))
            return;

        var comp = EnsureComp<CEScreenshakeComponent>(uid);
        var time = _timing.CurTime;
        var end = CalculateEndTimeForCommand((uid, comp), translation, rotation, time);
        var command = new CEScreenshakeCommand(translation, rotation, time, end);

        comp.Commands.Add(command);
        Dirty(uid, comp);
    }

    public void Screenshake(Filter filter, CEScreenshakeParameters? translation, CEScreenshakeParameters? rotation)
    {
        foreach (var player in filter.Recipients)
        {
            if (player.AttachedEntity is {} ent)
                Screenshake(ent, translation, rotation);
        }
    }

    #endregion
}
