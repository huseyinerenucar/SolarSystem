using System;
using UnityEngine;

/// <summary>
/// Double precision 3D vector for high-accuracy astronomical calculations.
/// Prevents floating-point drift in long-running simulations.
/// Compatible with Unity's Job System (blittable type).
/// </summary>
[System.Serializable]
public struct Vector3D : IEquatable<Vector3D>
{
    public double x;
    public double y;
    public double z;

    public Vector3D(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3D(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public static Vector3D zero => new(0, 0, 0);
    public static Vector3D one => new(1, 1, 1);
    public static Vector3D up => new(0, 1, 0);
    public static Vector3D down => new(0, -1, 0);
    public static Vector3D left => new(-1, 0, 0);
    public static Vector3D right => new(1, 0, 0);
    public static Vector3D forward => new(0, 0, 1);
    public static Vector3D back => new(0, 0, -1);

    public readonly double magnitude => Math.Sqrt((x * x) + (y * y) + (z * z));
    public readonly double sqrMagnitude => (x * x) + (y * y) + (z * z);

    public readonly Vector3D normalized
    {
        get
        {
            double mag = magnitude;
            return mag > 1e-10 ? this / mag : zero;
        }
    }

    public static Vector3D operator +(Vector3D a, Vector3D b) => new Vector3D(a.x + b.x, a.y + b.y, a.z + b.z);

    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.x - b.x, a.y - b.y, a.z - b.z);

    public static Vector3D operator -(Vector3D a) => new(-a.x, -a.y, -a.z);

    public static Vector3D operator *(Vector3D a, double d) => new(a.x * d, a.y * d, a.z * d);

    public static Vector3D operator *(double d, Vector3D a) => new(a.x * d, a.y * d, a.z * d);

    public static Vector3D operator /(Vector3D a, double d) => new(a.x / d, a.y / d, a.z / d);

    public static double Dot(Vector3D a, Vector3D b) => (a.x * b.x) + (a.y * b.y) + (a.z * b.z);

    public static Vector3D Cross(Vector3D a, Vector3D b) => new(
        (a.y * b.z) - (a.z * b.y),
        (a.z * b.x) - (a.x * b.z),
        (a.x * b.y) - (a.y * b.x)
    );

    public static double Distance(Vector3D a, Vector3D b) => (a - b).magnitude;

    public static double SqrDistance(Vector3D a, Vector3D b) => (a - b).sqrMagnitude;

    public static Vector3D Lerp(Vector3D a, Vector3D b, double t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return a + ((b - a) * t);
    }

    public static Vector3D Scale(Vector3D a, Vector3D b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

    public readonly Vector3 ToVector3() => new((float)x, (float)y, (float)z);

    public static implicit operator Vector3(Vector3D v) => v.ToVector3();
    public static implicit operator Vector3D(Vector3 v) => new(v);

    public readonly bool Equals(Vector3D other) => x == other.x && y == other.y && z == other.z;

    public override readonly bool Equals(object obj) => obj is Vector3D other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(x, y, z);

    public static bool operator ==(Vector3D a, Vector3D b) => a.Equals(b);
    public static bool operator !=(Vector3D a, Vector3D b) => !a.Equals(b);

    public override readonly string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    public readonly string ToString(string format) => $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
}