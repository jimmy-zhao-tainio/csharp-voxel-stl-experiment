#nullable enable

using System;
using System.Collections.Generic;

namespace VoxelCad.Core;

internal static class MeshOps
{
    public static MeshD QuantizeAndWeld(MeshD mesh, double stepUnits, ProjectSettings settings)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (stepUnits <= 0)
        {
            return mesh;
        }

        var step = stepUnits * settings.VoxelsPerUnit;
        if (step <= 0)
        {
            return mesh;
        }

        var inverseStep = 1.0 / step;
        var vertexRemap = new Dictionary<(long x, long y, long z), int>();
        var vertices = new List<VertexD>();

        int MapVertex(VertexD vertex)
        {
            var qx = (long)Math.Round(vertex.X * inverseStep);
            var qy = (long)Math.Round(vertex.Y * inverseStep);
            var qz = (long)Math.Round(vertex.Z * inverseStep);
            var key = (qx, qy, qz);
            if (vertexRemap.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var snapped = new VertexD(qx * step, qy * step, qz * step);
            var index = vertices.Count;
            vertices.Add(snapped);
            vertexRemap[key] = index;
            return index;
        }

        var triangles = new List<TriIdx>();
        var uniqueFaces = new HashSet<(int, int, int)>();

        foreach (var tri in mesh.F)
        {
            if (tri.A < 0 || tri.B < 0 || tri.C < 0 ||
                tri.A >= mesh.V.Count || tri.B >= mesh.V.Count || tri.C >= mesh.V.Count)
            {
                continue;
            }

            var ia = MapVertex(mesh.V[tri.A]);
            var ib = MapVertex(mesh.V[tri.B]);
            var ic = MapVertex(mesh.V[tri.C]);

            if (ia == ib || ib == ic || ic == ia)
            {
                continue;
            }

            var sorted = SortFaceVertices(ia, ib, ic);
            if (!uniqueFaces.Add(sorted))
            {
                continue;
            }

            triangles.Add(new TriIdx(ia, ib, ic));
        }

        return new MeshD { V = vertices, F = triangles };
    }

    private static (int, int, int) SortFaceVertices(int a, int b, int c)
    {
        if (a > b) (a, b) = (b, a);
        if (b > c) (b, c) = (c, b);
        if (a > b) (a, b) = (b, a);
        return (a, b, c);
    }

    public static void EnsureOutwardNormals(MeshD mesh)
    {
        if (mesh is null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        if (mesh.F.Count == 0)
        {
            return;
        }

        var volume = MeshValidation.SignedVolume(mesh);
        if (volume >= 0)
        {
            return;
        }

        for (var i = 0; i < mesh.F.Count; i++)
        {
            var tri = mesh.F[i];
            (tri.B, tri.C) = (tri.C, tri.B);
            mesh.F[i] = tri;
        }
    }
}
