using System;
using System.IO;
using SolidBuilder.Voxels;
using VoxelCad.Core;

namespace VoxelCad.Core.Tests;

public class CoreSceneTests
{
    [Fact]
    public void VoxelsPerUnitScalingAffectsVolume()
    {
        var settings1 = new ProjectSettings(voxelsPerUnit: 1);
        var project1 = new Project(settings1);
        var scene1 = project1.NewScene();
        scene1.NewPart("box", b => b.Box(new Int3(0, 0, 0), new Int3(10, 10, 2)));
        var solid1 = scene1.BuildSolid();

        var settings2 = new ProjectSettings(voxelsPerUnit: 2);
        var project2 = new Project(settings2);
        var scene2 = project2.NewScene();
        scene2.NewPart("box", b => b.Box(new Int3(0, 0, 0), new Int3(10, 10, 2)));
        var solid2 = scene2.BuildSolid();

        Assert.Equal(200, VoxelKernel.GetVolume(solid1));
        Assert.Equal(200 * 8, VoxelKernel.GetVolume(solid2));
    }

    [Fact]
    public void SaveAndExportProduceFiles()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var project = new Project(settings);
        var scene = project.NewScene();
        scene.NewPart("panel", b => b.Box(new Int3(0, 0, 0), new Int3(4, 4, 2)));

        using var tempDir = new TempDir();
        var sbvxPath = Path.Combine(tempDir.Path, "test.vox");
        var stlPath = Path.Combine(tempDir.Path, "test.stl");

        scene.SaveProject(sbvxPath, new SaveOptions(SbvxCompression.None, 6));
        scene.ExportStl(stlPath);

        Assert.True(File.Exists(sbvxPath));
        Assert.True(File.Exists(stlPath));

        var header = new byte[5];
        using (var stream = File.OpenRead(sbvxPath))
        {
            Assert.Equal(5, stream.Read(header, 0, 5));
        }

        Assert.Equal(new byte[] { (byte)'S', (byte)'B', (byte)'V', (byte)'X', 0 }, header);
        Assert.True(new FileInfo(stlPath).Length > 0);
    }

    [Fact]
    public void ExportStlHonorsOptionsFallback()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var project = new Project(settings);
        var scene = project.NewScene();
        scene.NewPart("shape", b => b.Box(new Int3(0, 0, 0), new Int3(2, 2, 2)));

        using var tempDir = new TempDir();
        var stlPathDefault = Path.Combine(tempDir.Path, "default.stl");
        var stlPathOptions = Path.Combine(tempDir.Path, "options.stl");

        scene.ExportStl(stlPathDefault);
        scene.ExportStl(stlPathOptions, new ExportOptions
        {
            Engine = MeshEngine.SurfaceNets,
            IsoLevel = 0.4,
            SmoothingPasses = 2,
            Quantize = QuantizeOptions.Units(0.1)
        });

        Assert.True(File.Exists(stlPathDefault));
        Assert.True(File.Exists(stlPathOptions));
        Assert.True(new FileInfo(stlPathDefault).Length > 0);
        Assert.True(new FileInfo(stlPathOptions).Length > 0);
    }

    [Fact]
    public void GreedyQuadMergeReducesTriangleCount()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, new Int3(0, 0, 0), new Int3(30, 300, 4));

        var naiveTriangles = VoxelKernel.GetSurfaceArea(solid) * 2;
        var triangles = VoxelKernel.ToTriangles(solid);

        Assert.True(triangles.Count <= naiveTriangles / 2);
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
