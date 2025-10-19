using SolidBuilder.Voxels;
using VoxelCad.Builder;
using VoxelCad.Core;

namespace VoxelCad.Scene.Tests;

public class SceneGraphTests
{
    private static Part CreateBoxPart(string name, Int3 min, Int3 max)
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, min, max);
        return new Part(name, solid);
    }

    [Fact]
    public void BakeWithArbitraryRotationIsWatertight()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);

        var part = CreateBoxPart("box", new Int3(0, 0, 0), new Int3(10, 20, 4));

        var instance1 = scene.AddInstance(part);
        instance1.RotateAny(Axis.Z, 32.5, new Int3(0, 0, 0));

        var instance2 = scene.AddInstance(part);
        instance2.Move(25, 0, 0).RotateAny(Axis.Z, -18.75, new Int3(25, 0, 0));

        var baked = scene.Bake();

        Assert.True(VoxelKernel.IsWatertight(baked));
        Assert.NotEqual(0, VoxelKernel.GetVolume(baked));
    }

    [Fact]
    public void HoleRoleSubtractsFromSolid()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);

        var solidPart = CreateBoxPart("solid", new Int3(0, 0, 0), new Int3(10, 10, 10));
        var holePart = CreateBoxPart("hole", new Int3(2, 2, 2), new Int3(8, 8, 8));

        scene.AddInstance(solidPart, Role.Solid);
        scene.AddInstance(holePart, Role.Hole);

        var baked = scene.Bake();

        Assert.True(VoxelKernel.IsWatertight(baked));
        Assert.Equal(1000 - 216, VoxelKernel.GetVolume(baked));
    }

    [Fact]
    public void VoxelsPerUnitOverrideIncreasesResolution()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);
        var part = CreateBoxPart("box", new Int3(0, 0, 0), new Int3(4, 4, 4));
        scene.AddInstance(part);

        var coarse = scene.Bake();
        var fine = scene.Bake(new BakeOptions { VoxelsPerUnitOverride = 2 });

        Assert.Equal(VoxelKernel.GetVolume(coarse) * 8, VoxelKernel.GetVolume(fine));
    }

    [Fact]
    public void UsingLocalOnRotatedInstanceProducesWatertightResult()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);

        var columnPart = CreateBoxPart("column", new Int3(-4, -4, 0), new Int3(4, 4, 16));
        var columnInstance = scene.AddInstance(columnPart);
        columnInstance.RotateAny(Axis.Z, 17.5, new Int3(0, 0, 0));

        var holeBuilder = new VoxelBuilder()
            .UsingLocal(columnInstance, Role.Solid, b => b.CylinderZ(0, 0, 0, 16, 2));
        var holeSolid = holeBuilder.Build();
        Assert.True(VoxelKernel.IsWatertight(holeSolid));

        var holePart = new Part("hole", holeSolid, Role.Hole);
        scene.AddInstance(holePart, Role.Hole);

        var baked = scene.Bake(new BakeOptions
        {
            RevoxelizeOverride = new RevoxelizationSettings
            {
                ConservativeObb = true,
                SamplesPerAxis = 5,
                Epsilon = 1e-8
            }
        });
        Assert.True(VoxelKernel.GetVolume(baked) > 0);
    }
}
