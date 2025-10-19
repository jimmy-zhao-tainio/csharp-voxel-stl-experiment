using SolidBuilder.Voxels;
using VoxelCad.Core;
using VoxelCad.Scene;
using SceneRole = VoxelCad.Scene.Role;

namespace VoxelCad.Builder.Tests;

public class VoxelBuilderTests
{
    [Fact]
    public void BoxProducesExpectedCounts()
    {
        var solid = new VoxelBuilder()
            .Box(new Int3(0, 0, 0), new Int3(2, 2, 1))
            .Build();

        Assert.Equal(4, VoxelKernel.GetVolume(solid));
        Assert.Equal(16, VoxelKernel.GetSurfaceArea(solid));
        Assert.True(VoxelKernel.IsWatertight(solid));
    }

    [Fact]
    public void PlaceAndArrayXComposeOffsets()
    {
        var solid = new VoxelBuilder()
            .Place(new Int3(1, 0, 0), b =>
                b.ArrayX(3, 2, inner =>
                    inner.Box(new Int3(0, 0, 0), new Int3(1, 1, 1))))
            .Build();

        Assert.Equal(3, VoxelKernel.GetVolume(solid));
        Assert.True(VoxelKernel.IsWatertight(solid));
        Assert.Contains(new Int3(1, 0, 0), solid.Voxels);
        Assert.Contains(new Int3(3, 0, 0), solid.Voxels);
        Assert.Contains(new Int3(5, 0, 0), solid.Voxels);
    }

    [Fact]
    public void BooleanOpsYieldExpectedCounts()
    {
        var solid = new VoxelBuilder()
            .Box(new Int3(0, 0, 0), new Int3(2, 2, 1))
            .Union(b => b.Translate(1, 0, 0).Box(new Int3(0, 0, 0), new Int3(2, 2, 1)))
            .Subtract(b => b.Box(new Int3(1, 0, 0), new Int3(2, 2, 1)))
            .Intersect(b => b.Box(new Int3(0, 0, 0), new Int3(3, 3, 1)))
            .Build();

        // Starting volume 4, union adds shifted box (overlap 2 voxels) => volume 6.
        // Subtract removes 2 voxels => volume 4.
        Assert.Equal(4, VoxelKernel.GetVolume(solid));
        Assert.True(VoxelKernel.IsWatertight(solid));
    }

    [Fact]
    public void RotateAny_SubtractedPatternIsWatertight()
    {
        var panel = new VoxelBuilder()
            .Box(new Int3(0, 0, 0), new Int3(60, 60, 4))
            .Subtract(b =>
                b.RotateAny(Axis.Z, 30.0, HolePattern))
            .Build();

        Assert.True(VoxelKernel.IsWatertight(panel));
        Assert.NotEqual(0, VoxelKernel.GetVolume(panel));
    }

    [Fact]
    public void RotateAnyWith_SupersampleStaysWatertight()
    {
        var obb = new VoxelBuilder()
            .Box(new Int3(0, 0, 0), new Int3(60, 60, 4))
            .Subtract(b => b.RotateAny(Axis.Z, 30.0, HolePattern))
            .Build();

        var options = new RotateOptions
        {
            Pivot = new Int3(0, 0, 0),
            ConservativeObb = false,
            SamplesPerAxis = 5,
            Epsilon = 1e-8
        };

        var supersampled = new VoxelBuilder()
            .Box(new Int3(0, 0, 0), new Int3(60, 60, 4))
            .Subtract(b => b.RotateAnyWith(Axis.Z, 30.0, options, HolePattern))
            .Build();

        Assert.True(VoxelKernel.IsWatertight(obb));
        Assert.True(VoxelKernel.IsWatertight(supersampled));
        Assert.NotEqual(0, VoxelKernel.GetVolume(obb));
        Assert.NotEqual(0, VoxelKernel.GetVolume(supersampled));
    }

    [Fact]
    public void CylinderXAndYProduceExpectedVoxels()
    {
        var solidX = new VoxelBuilder()
            .CylinderX(0, 0, -2, 3, 2)
            .Build();

        Assert.True(VoxelKernel.IsWatertight(solidX));
        Assert.True(solidX.Voxels.Count > 0);

        var solidY = new VoxelBuilder()
            .CylinderY(0, 0, -2, 3, 2)
            .Build();

        Assert.True(VoxelKernel.IsWatertight(solidY));
        Assert.Equal(VoxelKernel.GetVolume(solidX), VoxelKernel.GetVolume(solidY));
    }

    [Fact]
    public void UsingLocalAppliesInstanceFrame()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var columnSolid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(columnSolid, new Int3(-2, -2, 0), new Int3(2, 2, 12));
        var part = new Part("column", columnSolid);

        var scene = new VoxelCad.Scene.Scene(settings);
        var instance = scene.AddInstance(part);
        instance.RotateAny(Axis.Z, 22.5, new Int3(0, 0, 0));

        var builder = new VoxelBuilder()
            .Box(new Int3(-3, -3, 0), new Int3(3, 3, 12))
            .UsingLocal(instance, SceneRole.Hole, b => b.CylinderZ(0, 0, 0, 12, 1));
        var result = builder.Build();

        Assert.True(VoxelKernel.IsWatertight(result));
        Assert.NotEqual(0, VoxelKernel.GetVolume(result));
    }

    private static void HolePattern(VoxelBuilder pattern)
    {
        pattern.Box(new Int3(5, 5, 0), new Int3(15, 25, 4));
        pattern.Place(new Int3(20, 0, 0), inner =>
        {
            inner.Box(new Int3(5, 5, 0), new Int3(15, 25, 4));
        });
    }
}
