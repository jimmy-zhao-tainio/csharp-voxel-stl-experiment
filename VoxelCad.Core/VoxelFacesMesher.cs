#nullable enable

using System;
using System.Collections.Generic;
using SolidBuilder.Voxels;

namespace VoxelCad.Core;

internal static class VoxelFacesMesher
{
    public static MeshD Build(VoxelSolid solid)
    {
        if (solid is null)
        {
            throw new ArgumentNullException(nameof(solid));
        }

        var mesh = new MeshD();
        if (solid.BoundaryFaces.Count == 0)
        {
            return mesh;
        }

        var vertexMap = new Dictionary<(int X, int Y, int Z), int>();

        foreach (var face in solid.BoundaryFaces)
        {
            var normalSign = GetFaceNormalSign(solid, face);
            if (normalSign == 0)
            {
                continue;
            }

            var corners = GetFaceCorners(face);

            if (normalSign > 0)
            {
                AddTriangle(mesh, vertexMap, corners.P0, corners.P1, corners.P2);
                AddTriangle(mesh, vertexMap, corners.P0, corners.P2, corners.P3);
            }
            else
            {
                AddTriangle(mesh, vertexMap, corners.P0, corners.P3, corners.P2);
                AddTriangle(mesh, vertexMap, corners.P0, corners.P2, corners.P1);
            }
        }

        return mesh;
    }

    private static void AddTriangle(
        MeshD mesh,
        Dictionary<(int X, int Y, int Z), int> vertexMap,
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c)
    {
        var ia = GetOrAddVertex(mesh, vertexMap, a);
        var ib = GetOrAddVertex(mesh, vertexMap, b);
        var ic = GetOrAddVertex(mesh, vertexMap, c);
        mesh.F.Add(new TriIdx(ia, ib, ic));
    }

    private static int GetOrAddVertex(
        MeshD mesh,
        Dictionary<(int X, int Y, int Z), int> vertexMap,
        (int X, int Y, int Z) point)
    {
        if (!vertexMap.TryGetValue(point, out var index))
        {
            index = mesh.V.Count;
            vertexMap[point] = index;
            mesh.V.Add(new VertexD(point.X, point.Y, point.Z));
        }

        return index;
    }

    private static ((int X, int Y, int Z) P0, (int X, int Y, int Z) P1, (int X, int Y, int Z) P2, (int X, int Y, int Z) P3) GetFaceCorners(FaceKey face)
    {
        return face.Axis switch
        {
            Axis.X => GetAxisXCorners(face),
            Axis.Y => GetAxisYCorners(face),
            Axis.Z => GetAxisZCorners(face),
            _ => throw new ArgumentOutOfRangeException(nameof(face))
        };
    }

    private static ((int X, int Y, int Z) P0, (int X, int Y, int Z) P1, (int X, int Y, int Z) P2, (int X, int Y, int Z) P3) GetAxisXCorners(FaceKey face)
    {
        var x = face.K;
        var y0 = face.A;
        var y1 = face.A + 1;
        var z0 = face.B;
        var z1 = face.B + 1;
        return (
            (x, y0, z0),
            (x, y1, z0),
            (x, y1, z1),
            (x, y0, z1));
    }

    private static ((int X, int Y, int Z) P0, (int X, int Y, int Z) P1, (int X, int Y, int Z) P2, (int X, int Y, int Z) P3) GetAxisYCorners(FaceKey face)
    {
        var y = face.K;
        var x0 = face.A;
        var x1 = face.A + 1;
        var z0 = face.B;
        var z1 = face.B + 1;
        return (
            (x0, y, z0),
            (x1, y, z0),
            (x1, y, z1),
            (x0, y, z1));
    }

    private static ((int X, int Y, int Z) P0, (int X, int Y, int Z) P1, (int X, int Y, int Z) P2, (int X, int Y, int Z) P3) GetAxisZCorners(FaceKey face)
    {
        var z = face.K;
        var x0 = face.A;
        var x1 = face.A + 1;
        var y0 = face.B;
        var y1 = face.B + 1;
        return (
            (x0, y0, z),
            (x1, y0, z),
            (x1, y1, z),
            (x0, y1, z));
    }

    private static int GetFaceNormalSign(VoxelSolid solid, FaceKey face)
    {
        return face.Axis switch
        {
            Axis.X => GetAxisXSign(solid, face),
            Axis.Y => GetAxisYSign(solid, face),
            Axis.Z => GetAxisZSign(solid, face),
            _ => 0
        };
    }

    private static int GetAxisXSign(VoxelSolid solid, FaceKey face)
    {
        var positive = new Int3(face.K, face.A, face.B);
        if (solid.Voxels.Contains(positive))
        {
            return -1;
        }

        var negative = new Int3(face.K - 1, face.A, face.B);
        if (solid.Voxels.Contains(negative))
        {
            return 1;
        }

        return 0;
    }

    private static int GetAxisYSign(VoxelSolid solid, FaceKey face)
    {
        var positive = new Int3(face.A, face.K, face.B);
        if (solid.Voxels.Contains(positive))
        {
            return -1;
        }

        var negative = new Int3(face.A, face.K - 1, face.B);
        if (solid.Voxels.Contains(negative))
        {
            return 1;
        }

        return 0;
    }

    private static int GetAxisZSign(VoxelSolid solid, FaceKey face)
    {
        var positive = new Int3(face.A, face.B, face.K);
        if (solid.Voxels.Contains(positive))
        {
            return -1;
        }

        var negative = new Int3(face.A, face.B, face.K - 1);
        if (solid.Voxels.Contains(negative))
        {
            return 1;
        }

        return 0;
    }
}
