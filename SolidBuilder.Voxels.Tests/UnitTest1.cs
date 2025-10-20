using System.IO;
using SolidBuilder.Voxels;

namespace SolidBuilder.Voxels.Tests;

public class KernelTests
{
    [Fact]
    public void SingleVoxel_HasExpectedBoundary()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddVoxel(solid, new Int3(0, 0, 0));

        Assert.Equal(1, VoxelKernel.GetVolume(solid));
        Assert.Equal(6, VoxelKernel.GetSurfaceArea(solid));
        Assert.True(VoxelKernel.IsWatertight(solid));
        Assert.True(VoxelKernel.Is6Connected(solid));
    }

    [Fact]
    public void FaceAdjacentVoxels_DoNotExposeInteriorFace()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddVoxel(solid, new Int3(0, 0, 0));
        VoxelKernel.AddVoxel(solid, new Int3(1, 0, 0));

        var sharedFace = new FaceKey(Axis.X, 1, 0, 0);
        Assert.DoesNotContain(sharedFace, solid.BoundaryFaces);
        Assert.Equal(2, VoxelKernel.GetVolume(solid));
        Assert.Equal(10, VoxelKernel.GetSurfaceArea(solid));
        Assert.True(VoxelKernel.IsWatertight(solid));
    }

    [Fact]
    public void AddBox_WithExclusiveBoundsProducesCounts()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, new Int3(0, 0, 0), new Int3(2, 2, 1));

        Assert.Equal(4, VoxelKernel.GetVolume(solid));
        Assert.Equal(16, VoxelKernel.GetSurfaceArea(solid));
        Assert.True(VoxelKernel.IsWatertight(solid));
        Assert.True(VoxelKernel.Is6Connected(solid));
    }

    [Fact]
    public void DenseRoundTrip_PreservesStructure()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, new Int3(1, 2, 3), new Int3(3, 4, 5));

        using var buffer = new MemoryStream();
        VoxelKernel.WriteSbvxDense(solid, buffer);
        buffer.Position = 0;

        var loaded = VoxelKernel.ReadSbvxDense(buffer);
        Assert.True(solid.Voxels.SetEquals(loaded.Voxels));
        Assert.Equal(solid.BoundaryFaces.Count, loaded.BoundaryFaces.Count);
        Assert.True(VoxelKernel.IsWatertight(loaded));
    }

    [Fact]
    public void SparseRoundTrip_PreservesStructure()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddVoxel(solid, new Int3(-5, 0, 0));
        VoxelKernel.AddVoxel(solid, new Int3(10, 20, 30));

        using var buffer = new MemoryStream();
        VoxelKernel.WriteSbvxSparse(solid, buffer);
        buffer.Position = 0;

        var loaded = VoxelKernel.ReadSbvx(buffer);
        Assert.True(solid.Voxels.SetEquals(loaded.Voxels));
        Assert.Equal(solid.BoundaryFaces.Count, loaded.BoundaryFaces.Count);
        Assert.True(VoxelKernel.IsWatertight(loaded));
    }

    [Fact]
    public void AutoWrite_SelectsEncodingByOccupancy()
    {
        var denseSolid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(denseSolid, new Int3(0, 0, 0), new Int3(2, 2, 2)); // 8 of 8 cells => dense

        using var denseBuffer = new MemoryStream();
        VoxelKernel.AutoWriteSbvx(denseSolid, denseBuffer);
        var denseBytes = denseBuffer.ToArray();
        Assert.True(denseBytes.Length >= 7);
        Assert.Equal(0, denseBytes[6]); // encoding slot

        var sparseSolid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddVoxel(sparseSolid, new Int3(0, 0, 0));
        VoxelKernel.AddVoxel(sparseSolid, new Int3(3, 3, 3)); // bounds 4x4x4 with 2/64 occupancy

        using var sparseBuffer = new MemoryStream();
        VoxelKernel.AutoWriteSbvx(sparseSolid, sparseBuffer);
        var sparseBytes = sparseBuffer.ToArray();
        Assert.True(sparseBytes.Length >= 7);
        Assert.Equal(1, sparseBytes[6]); // encoding slot
    }

    [Fact]
    public void RotateRevoxelized_WatertightWithObb()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, new Int3(0, 0, 0), new Int3(20, 10, 4));

        Assert.True(VoxelKernel.IsWatertight(solid));

        var options = new RotateOptions
        {
            Axis = Axis.Z,
            Degrees = 30.0,
            Pivot = new Int3(0, 0, 0),
            ConservativeObb = true,
            Epsilon = 1e-8
        };

        var rotated = VoxelKernel.RotateRevoxelized(solid, options);

        Assert.True(VoxelKernel.IsWatertight(rotated));
        Assert.NotEqual(0, VoxelKernel.GetVolume(rotated));
    }

    [Fact]
    public void RotateRevoxelized_WatertightWithSupersampling()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, new Int3(0, 0, 0), new Int3(20, 10, 4));

        Assert.True(VoxelKernel.IsWatertight(solid));

        var options = new RotateOptions
        {
            Axis = Axis.Z,
            Degrees = 30.0,
            Pivot = new Int3(0, 0, 0),
            ConservativeObb = false,
            SamplesPerAxis = 4,
            Epsilon = 1e-8
        };

        var rotated = VoxelKernel.RotateRevoxelized(solid, options);

        Assert.True(VoxelKernel.IsWatertight(rotated));
        Assert.NotEqual(0, VoxelKernel.GetVolume(rotated));
    }

    [Fact]
    public void MorphologyOpenCloseAffectsBumpsAndPits()
    {
        var pitSolid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(pitSolid, new Int3(0, 0, 0), new Int3(10, 10, 2));
        VoxelKernel.RemoveVoxel(pitSolid, new Int3(5, 5, 1));
        Assert.True(VoxelKernel.IsWatertight(pitSolid));

        var closed = VoxelKernel.Close(pitSolid, radius: 1);
        Assert.Contains(new Int3(5, 5, 1), closed.Voxels);
        Assert.True(VoxelKernel.IsWatertight(closed));

        var bumpSolid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(bumpSolid, new Int3(0, 0, 0), new Int3(10, 10, 2));
        VoxelKernel.AddVoxel(bumpSolid, new Int3(5, 5, 2));
        Assert.True(VoxelKernel.IsWatertight(bumpSolid));

        var opened = VoxelKernel.Open(bumpSolid, radius: 1);
        Assert.DoesNotContain(new Int3(5, 5, 2), opened.Voxels);
        Assert.True(VoxelKernel.IsWatertight(opened));
    }
}
