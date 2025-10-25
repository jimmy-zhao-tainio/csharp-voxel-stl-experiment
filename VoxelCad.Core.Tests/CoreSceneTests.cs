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

        var solid = scene.BuildSolid();
        var mesh = VoxelFacesMesher.Build(solid);
        Assert.True(MeshValidation.IsClosedManifold(mesh), DescribeMeshIssues(mesh));
        Assert.True(MeshValidation.SignedVolume(mesh) > 0);
        Assert.Equal(mesh.V.Count, MeshValidation.UniqueVertexCount(mesh));

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
            Engine = MeshEngine.VoxelFaces,
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
    public void VoxelFacesMesh_HasPositiveVolumeAndIsClosed()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var project = new Project(settings);
        var scene = project.NewScene();
        scene.NewPart("shape", b =>
        {
            b.Box(new Int3(0, 0, 0), new Int3(6, 6, 3));
            b.Subtract(inner => inner.CylinderZ(3, 3, 0, 3, 2));
        });

        var solid = scene.BuildSolid();
        var mesh = VoxelFacesMesher.Build(solid);

        Assert.True(MeshValidation.IsClosedManifold(mesh), DescribeMeshIssues(mesh));
        Assert.True(MeshValidation.SignedVolume(mesh) > 0);
    }

    [Fact]
    public void SurfaceNetsExportThrowsUntilImplemented()
    {
        var settings = new ProjectSettings(voxelsPerUnit: 1);
        var project = new Project(settings);
        var scene = project.NewScene();
        scene.NewPart("shape", b => b.Box(new Int3(0, 0, 0), new Int3(2, 2, 2)));

        using var tempDir = new TempDir();
        var stlPath = Path.Combine(tempDir.Path, "surface-nets.stl");

        Assert.Throws<NotImplementedException>(() => scene.ExportStl(stlPath, new ExportOptions
        {
            Engine = MeshEngine.SurfaceNets
        }));
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

    [Fact]
    public void EnsureOutwardNormalsFlipsNegativeVolume()
    {
        var solid = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(solid, new Int3(0, 0, 0), new Int3(4, 4, 4));

        var mesh = VoxelFacesMesher.Build(solid);

        for (var i = 0; i < mesh.F.Count; i++)
        {
            var tri = mesh.F[i];
            (tri.B, tri.C) = (tri.C, tri.B);
            mesh.F[i] = tri;
        }

        var invertedVolume = MeshValidation.SignedVolume(mesh);
        Assert.True(invertedVolume < 0);

        MeshOps.EnsureOutwardNormals(mesh);

        var correctedVolume = MeshValidation.SignedVolume(mesh);
        Assert.True(correctedVolume > 0);
        Assert.True(MeshValidation.IsClosedManifold(mesh), DescribeMeshIssues(mesh));
    }

    private static string DescribeMeshIssues(MeshD mesh)
    {
        var degenerates = new List<int>();
        for (var i = 0; i < mesh.F.Count; i++)
        {
            var tri = mesh.F[i];
            var a = mesh.V[tri.A];
            var b = mesh.V[tri.B];
            var c = mesh.V[tri.C];
            var abx = b.X - a.X;
            var aby = b.Y - a.Y;
            var abz = b.Z - a.Z;
            var acx = c.X - a.X;
            var acy = c.Y - a.Y;
            var acz = c.Z - a.Z;
            var crossX = aby * acz - abz * acy;
            var crossY = abz * acx - abx * acz;
            var crossZ = abx * acy - aby * acx;
            var areaSq = crossX * crossX + crossY * crossY + crossZ * crossZ;
            if (areaSq <= double.Epsilon)
            {
                degenerates.Add(i);
            }
        }

        var edges = new Dictionary<(int a, int b), int>();
        foreach (var tri in mesh.F)
        {
            CountEdge(edges, tri.A, tri.B);
            CountEdge(edges, tri.B, tri.C);
            CountEdge(edges, tri.C, tri.A);
        }

        var incidenceIssues = new List<string>();
        foreach (var pair in edges.Where(pair => pair.Value != 2))
        {
            var va = mesh.V[pair.Key.a];
            var vb = mesh.V[pair.Key.b];
            var triRefs = new List<int>();
            for (var i = 0; i < mesh.F.Count; i++)
            {
                var tri = mesh.F[i];
                var hasA = tri.A == pair.Key.a || tri.B == pair.Key.a || tri.C == pair.Key.a;
                var hasB = tri.A == pair.Key.b || tri.B == pair.Key.b || tri.C == pair.Key.b;
                if (hasA && hasB)
                {
                    triRefs.Add(i);
                }
            }

            incidenceIssues.Add($"({pair.Key.a},{pair.Key.b})={pair.Value}[({va.X},{va.Y},{va.Z})->({vb.X},{vb.Y},{vb.Z}) tris: {string.Join(',', triRefs)}]");
        }

        var parts = new List<string>();
        if (degenerates.Count > 0)
        {
            parts.Add($"degenerate tris: {string.Join(',', degenerates)}");
        }

        if (incidenceIssues.Count > 0)
        {
            parts.Add($"edge incidence: {string.Join(';', incidenceIssues)}");
        }

        return parts.Count == 0 ? "MeshValidation reported failure" : string.Join(" | ", parts);
    }

    private static void CountEdge(Dictionary<(int a, int b), int> edges, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        edges.TryGetValue(key, out var count);
        edges[key] = count + 1;
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

