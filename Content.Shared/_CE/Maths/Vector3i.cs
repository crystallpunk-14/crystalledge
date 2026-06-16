using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Maths;

[Serializable, NetSerializable]
[StructLayout(LayoutKind.Sequential)]
public struct Vector3i : IEquatable<Vector3i>
{
    public static readonly Vector3i Zero  = (0, 0, 0);
    public static readonly Vector3i One   = (1, 1, 1);
    public static readonly Vector3i UnitX = (1, 0, 0);
    public static readonly Vector3i UnitY = (0, 1, 0);
    public static readonly Vector3i UnitZ = (0, 0, 1);

    public int X;
    public int Y;
    public int Z;

    public Vector3i(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    // ── Derived ──────────────────────────────────────────────────────────

    public readonly float Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MathF.Sqrt(LengthSquared);
    }

    public readonly float LengthSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => X * X + Y * Y + Z * Z;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    public static Vector3i ComponentMax(Vector3i a, Vector3i b) =>
        new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

    public static Vector3i ComponentMin(Vector3i a, Vector3i b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));

    /// <summary>Returns the XY components as a <see cref="Vector2i"/>, discarding Z.</summary>
    public readonly Vector2i ToVector2i() => new(X, Y);

    public static Vector3i FromVector2i(Vector2i v, int z) => new(v.X, v.Y, z);

    // ── Operators ─────────────────────────────────────────────────────────

    public static Vector3i operator +(Vector3i a, Vector3i b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3i operator +(Vector3i a, int b) => new(a.X + b, a.Y + b, a.Z + b);

    public static Vector3i operator -(Vector3i a, Vector3i b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3i operator -(Vector3i a, int b) => new(a.X - b, a.Y - b, a.Z - b);
    public static Vector3i operator -(Vector3i a) => new(-a.X, -a.Y, -a.Z);

    public static Vector3i operator *(Vector3i a, Vector3i b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vector3i operator *(Vector3i a, int scale) => new(a.X * scale, a.Y * scale, a.Z * scale);
    public static Vector3i operator *(int scale, Vector3i a) => a * scale;

    public static Vector3i operator /(Vector3i a, Vector3i b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static Vector3i operator /(Vector3i a, int scale) => new(a.X / scale, a.Y / scale, a.Z / scale);

    public static bool operator ==(Vector3i a, Vector3i b) => a.Equals(b);
    public static bool operator !=(Vector3i a, Vector3i b) => !a.Equals(b);

    // ── Equality ──────────────────────────────────────────────────────────

    public readonly bool Equals(Vector3i other) => X == other.X && Y == other.Y && Z == other.Z;

    public readonly override bool Equals(object? obj) => obj is Vector3i v && Equals(v);

    public readonly override int GetHashCode()
    {
        unchecked
        {
            return ((X * 397) ^ Y) * 397 ^ Z;
        }
    }

    // ── Deconstruct / conversions ─────────────────────────────────────────

    public readonly void Deconstruct(out int x, out int y, out int z)
    {
        x = X; y = Y; z = Z;
    }

    public static implicit operator Vector3i((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);

    // ── Formatting ────────────────────────────────────────────────────────

    public readonly override string ToString() => $"({X}, {Y}, {Z})";
}
