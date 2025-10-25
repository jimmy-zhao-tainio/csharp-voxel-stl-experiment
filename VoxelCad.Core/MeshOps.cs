#nullable enable

using System;

namespace VoxelCad.Core;

internal static class MeshOps
{
    /// <summary>
    /// Ensures the triangles in the mesh use outward-facing orientation by checking the signed volume.
    /// </summary>
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
