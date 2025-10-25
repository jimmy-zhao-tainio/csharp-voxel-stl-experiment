using System;
using SolidBuilder.Voxels;
using VoxelCad.Core;

namespace SolidBuilder.Api;

public enum Mesher
{
    VoxelFaces,
    SurfaceNets
}

public interface IMesher
{
    MeshD Generate(VoxelSolid solid);
}

internal sealed class VoxelFacesMesher : IMesher
{
    public MeshD Generate(VoxelSolid solid)
    {
        return VoxelCad.Core.VoxelFacesMesher.Build(solid);
    }
}

internal sealed class SurfaceNetsMesher : IMesher
{
    public MeshD Generate(VoxelSolid solid)
    {
        throw new NotImplementedException("SurfaceNets mesher not implemented yet.");
    }
}
