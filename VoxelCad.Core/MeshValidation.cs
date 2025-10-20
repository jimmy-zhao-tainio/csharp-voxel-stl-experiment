#nullable enable

using System;
using System.Collections.Generic;

namespace VoxelCad.Core;

internal static class MeshValidation
{
    public static bool IsClosedManifold(MeshD mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.F.Count == 0 || mesh.V.Count < 3)
        {
            return false;
        }

        var edgeIncidence = new Dictionary<(int a, int b), int>();

        foreach (var tri in mesh.F)
        {
            if (!IsTriangleIndicesValid(mesh, tri))
            {
                return false;
            }

            if (!HasPositiveArea(mesh, tri))
            {
                return false;
            }

            AddEdge(edgeIncidence, tri.A, tri.B);
            AddEdge(edgeIncidence, tri.B, tri.C);
            AddEdge(edgeIncidence, tri.C, tri.A);
        }

        foreach (var count in edgeIncidence.Values)
        {
            if (count != 2)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsClosedManifoldFuzzy(MeshD mesh, double gridStep)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (gridStep <= 0 || double.IsNaN(gridStep) || double.IsInfinity(gridStep))
        {
            throw new ArgumentOutOfRangeException(nameof(gridStep), "Grid step must be a finite positive value.");
        }

        var welded = WeldToGrid(mesh, gridStep);
        return IsClosedManifold(welded);
    }

    public static double SignedVolume(MeshD mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        double volume = 0;

        foreach (var tri in mesh.F)
        {
            if (!IsTriangleIndicesValid(mesh, tri))
            {
                throw new ArgumentException("Triangle contains vertex indices outside the mesh.", nameof(mesh));
            }

            var a = mesh.V[tri.A];
            var b = mesh.V[tri.B];
            var c = mesh.V[tri.C];

            var crossX = a.Y * b.Z - a.Z * b.Y;
            var crossY = a.Z * b.X - a.X * b.Z;
            var crossZ = a.X * b.Y - a.Y * b.X;

            volume += (crossX * c.X + crossY * c.Y + crossZ * c.Z) / 6.0;
        }

        return volume;
    }

    public static int UniqueVertexCount(MeshD mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        var unique = new HashSet<VertexD>();

        foreach (var vertex in mesh.V)
        {
            unique.Add(vertex);
        }

        return unique.Count;
    }

    private static void AddEdge(Dictionary<(int a, int b), int> edgeIncidence, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);

        if (edgeIncidence.TryGetValue(key, out var count))
        {
            edgeIncidence[key] = count + 1;
        }
        else
        {
            edgeIncidence[key] = 1;
        }
    }

    private static bool IsTriangleIndicesValid(MeshD mesh, TriIdx tri)
    {
        return tri.A >= 0 && tri.A < mesh.V.Count &&
               tri.B >= 0 && tri.B < mesh.V.Count &&
               tri.C >= 0 && tri.C < mesh.V.Count;
    }

    private static bool HasPositiveArea(MeshD mesh, TriIdx tri)
    {
        var a = mesh.V[tri.A];
        var b = mesh.V[tri.B];
        var c = mesh.V[tri.C];

        var abX = b.X - a.X;
        var abY = b.Y - a.Y;
        var abZ = b.Z - a.Z;

        var acX = c.X - a.X;
        var acY = c.Y - a.Y;
        var acZ = c.Z - a.Z;

        var crossX = abY * acZ - abZ * acY;
        var crossY = abZ * acX - abX * acZ;
        var crossZ = abX * acY - abY * acX;

        var crossLengthSquared = crossX * crossX + crossY * crossY + crossZ * crossZ;
        return crossLengthSquared > double.Epsilon;
    }

    private static MeshD WeldToGrid(MeshD mesh, double gridStep)
    {
        var result = new MeshD();
        if (mesh.V.Count == 0)
        {
            return result;
        }

        var remap = new int[mesh.V.Count];
        var indexByKey = new Dictionary<VertexKey, int>();
        var inverseStep = 1.0 / gridStep;

        for (var i = 0; i < mesh.V.Count; i++)
        {
            var vertex = mesh.V[i];
            var key = new VertexKey(
                Snap(vertex.X, inverseStep),
                Snap(vertex.Y, inverseStep),
                Snap(vertex.Z, inverseStep));

            if (!indexByKey.TryGetValue(key, out var newIndex))
            {
                newIndex = result.V.Count;
                indexByKey[key] = newIndex;
                result.V.Add(new VertexD(
                    key.X * gridStep,
                    key.Y * gridStep,
                    key.Z * gridStep));
            }

            remap[i] = newIndex;
        }

        foreach (var tri in mesh.F)
        {
            if (!IsTriangleIndicesValid(mesh, tri))
            {
                continue;
            }

            var a = remap[tri.A];
            var b = remap[tri.B];
            var c = remap[tri.C];
            result.F.Add(new TriIdx(a, b, c));
        }

        return result;
    }

    private static long Snap(double value, double inverseStep)
    {
        var scaled = value * inverseStep;
        var rounded = Math.Round(scaled, 0, MidpointRounding.AwayFromZero);
        return (long)rounded;
    }

    private readonly struct VertexKey : IEquatable<VertexKey>
    {
        public VertexKey(long x, long y, long z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public long X { get; }
        public long Y { get; }
        public long Z { get; }

        public bool Equals(VertexKey other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is VertexKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    }
}
