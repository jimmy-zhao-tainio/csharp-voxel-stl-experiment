using System;
using System.IO;
using SolidBuilder.Voxels;
using System.Linq;
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
    public void BakeAtResolutionUpscalesVoxelCount()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 2);
        var scene = new Scene(settings);
        var part = CreateBoxPart("block", new Int3(0, 0, 0), new Int3(4, 4, 4));
        scene.AddInstance(part);

        var baseSolid = scene.BakeAtResolution(2);
        var highRes = scene.BakeAtResolution(4);

        Assert.True(VoxelKernel.IsWatertight(baseSolid));
        Assert.True(VoxelKernel.IsWatertight(highRes));
        Assert.Equal(VoxelKernel.GetVolume(baseSolid) * 8, VoxelKernel.GetVolume(highRes));
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

    [Fact]
    public void WeldConnectsSeparatedPlates()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);

        var plate = CreateBoxPart("plate", new Int3(0, 0, 0), new Int3(10, 10, 3));
        var instA = scene.AddInstance(plate);
        var instB = scene.AddInstance(plate);
        instB.Move(12, 0, 0);

        var welded = scene.Weld(instA, instB, replaceInstances: false);

        Assert.True(VoxelKernel.IsWatertight(welded.Model));
        Assert.True(VoxelKernel.Is6Connected(welded.Model));
    }

    [Fact]
    public void BridgeAxisCreatesPrismConnector()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);

        var block = CreateBoxPart("block", new Int3(0, 0, 0), new Int3(6, 6, 6));
        var instA = scene.AddInstance(block);
        var instB = scene.AddInstance(block);
        instB.Move(0, 12, 0);

        var bridge = scene.BridgeAxis(instA, instB, Axis.Y, thickness: 1, mask: null, name: null, addInstance: false);

        Assert.True(VoxelKernel.IsWatertight(bridge.Model));
        Assert.Contains(bridge.Model.Voxels, v => v.Y >= 6 && v.Y < 12);
    }

    [Fact]
    public void StrutAddsWatertightBrace()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var scene = new Scene(settings);

        var node = CreateBoxPart("node", new Int3(0, 0, 0), new Int3(4, 4, 4));
        var instA = scene.AddInstance(node);
        var instB = scene.AddInstance(node);
        instB.Move(12, 12, 12);

        var strut = scene.Strut(instA, instB, radius: 1, name: null, addInstance: false);

        Assert.True(VoxelKernel.IsWatertight(strut.Model));
        Assert.Contains(strut.Model.Voxels, v => v.X > 4 && v.X < 12 && v.Y > 4 && v.Y < 12 && v.Z > 4 && v.Z < 12);
    }

    [Fact]
    public void NewPartAddsInstanceByDefault()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 2);
        var scene = new Scene(settings);

        var part = scene.NewPart("pillar", builder => builder.Box(new Int3(0, 0, 0), new Int3(1, 1, 2)));

        var instance = Assert.Single(scene.Instances);
        Assert.Equal(part, instance.Part);
        Assert.Equal(Role.Solid, instance.Role);
        Assert.Equal(16, VoxelKernel.GetVolume(part.Model));
        Assert.True(VoxelKernel.IsWatertight(part.Model));
    }

    [Fact]
    public void NewPartCanSkipInstanceAndRespectRole()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 3);
        var scene = new Scene(settings);

        var part = scene.NewPart("void", builder => builder.Box(new Int3(0, 0, 0), new Int3(1, 1, 1)), Role.Hole, addInstance: false);

        Assert.Empty(scene.Instances);
        Assert.Equal(Role.Hole, part.DefaultRole);
        Assert.Equal(27, VoxelKernel.GetVolume(part.Model));
        Assert.True(VoxelKernel.IsWatertight(part.Model));
    }

    [Fact]
    public void QualityPresetsIncreaseResolutionAndStayWatertight()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 2);
        var scene = new Scene(settings);

        scene.NewPart("panel", builder =>
        {
            builder.Box(new Int3(0, 0, 0), new Int3(24, 24, 4));
            builder.Subtract(inner =>
            {
                for (var x = 2; x < 22; x += 4)
                {
                    for (var y = 2; y < 22; y += 4)
                    {
                        inner.Box(new Int3(x, y, 0), new Int3(x + 1, y + 1, 1));
                    }
                }
            });
        });

        var draft = scene.BakeForQuality(QualityProfile.Draft);
        var medium = scene.BakeForQuality(QualityProfile.Medium);
        var high = scene.BakeForQuality(QualityProfile.High);

        Assert.True(VoxelKernel.IsWatertight(draft));
        Assert.True(VoxelKernel.IsWatertight(medium));
        Assert.True(VoxelKernel.IsWatertight(high));

        Assert.True(VoxelKernel.GetVolume(medium) >= VoxelKernel.GetVolume(draft));
        Assert.True(VoxelKernel.GetVolume(high) >= VoxelKernel.GetVolume(medium));
    }

    [Fact]
    public void ExportPresetsProduceManifoldMeshes()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 2);
        var scene = new Scene(settings);

        scene.NewPart("panel", builder =>
        {
            builder.Box(new Int3(0, 0, 0), new Int3(20, 20, 4));
            builder.Subtract(inner =>
            {
                for (var x = 1; x < 19; x += 3)
                {
                    for (var y = 1; y < 19; y += 3)
                    {
                        inner.Box(new Int3(x, y, 0), new Int3(x + 1, y + 1, 1));
                    }
                }
            });
        });

        using var temp = new TempDir();

        void ValidateQuality(QualityProfile profile, double expectedQuantize)
        {
            var path = Path.Combine(temp.Path, $"quality_{profile}.stl");
            scene.ExportStl(path, profile);

            var solid = scene.BakeForQuality(profile);
            var mesh = VoxelFacesMesher.Build(solid);
            if (expectedQuantize > 0)
            {
                mesh = MeshOps.QuantizeAndWeld(mesh, expectedQuantize, settings);
            }
            MeshOps.EnsureOutwardNormals(mesh);
            Assert.True(MeshValidation.IsClosedManifoldFuzzy(mesh, 1e-6));
            Assert.True(MeshValidation.SignedVolume(mesh) > 0);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }

        ValidateQuality(QualityProfile.Draft, 0);
        ValidateQuality(QualityProfile.Medium, 0.02);
        ValidateQuality(QualityProfile.High, 0.01);
    }

    [Fact]
    public void CustomQuantizeOverridesPreset()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 2);
        var scene = new Scene(settings);

        scene.NewPart("panel", builder => builder.Box(new Int3(0, 0, 0), new Int3(12, 12, 3)));

        var solid = scene.BakeForQuality(QualityProfile.High);
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "custom.stl");

        var customOptions = new ExportOptions { Quantize = QuantizeOptions.Units(0.03) };
        scene.ExportStl(path, QualityProfile.High, customOptions);

        var mesh = MeshOps.QuantizeAndWeld(VoxelFacesMesher.Build(solid), 0.03, settings);
        MeshOps.EnsureOutwardNormals(mesh);
        Assert.True(MeshValidation.IsClosedManifoldFuzzy(mesh, 1e-6));
        Assert.True(MeshValidation.SignedVolume(mesh) > 0);
        Assert.True(File.Exists(path));
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"VoxelCad_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
