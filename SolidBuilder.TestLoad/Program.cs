using SolidBuilder.Voxels;

const long ExpectedVolume = 33112;

var baseDir = AppContext.BaseDirectory;
var solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
var sourceDir = Path.Combine(solutionDir, "SolidBuilder.TestBuild");

var sbvxPath = FindLatest(sourceDir, "plate_auto.sbvx")
    ?? throw new FileNotFoundException("Could not locate plate_auto.sbvx. Run SolidBuilder.TestBuild first.");

using var input = File.OpenRead(sbvxPath);
var solid = VoxelKernel.ReadSbvx(input);

var watertight = VoxelKernel.IsWatertight(solid);
var volume = VoxelKernel.GetVolume(solid);
var surface = VoxelKernel.GetSurfaceArea(solid);
var triangles = VoxelKernel.ToTriangles(solid);

Console.WriteLine("Loaded SBVX asset:");
Console.WriteLine($"  Source: {sbvxPath}");
Console.WriteLine($"  Watertight: {watertight}");
Console.WriteLine($"  Volume (voxels): {volume}");
Console.WriteLine($"  Surface area (faces): {surface}");
Console.WriteLine($"  Triangles (faces × 2): {triangles.Count}");

if (!watertight)
{
    Console.WriteLine("  Warning: mesh is not watertight.");
}

if (volume != ExpectedVolume)
{
    Console.WriteLine($"  Warning: expected volume {ExpectedVolume}, got {volume}.");
}

var outputDir = baseDir;
var roundtripStl = Path.Combine(outputDir, "plate_roundtrip.stl");
using (var stream = File.Create(roundtripStl))
{
    VoxelKernel.WriteBinaryStl(solid, "KeyboardPlateRoundTrip", stream);
}

Console.WriteLine($"Re-exported STL: {roundtripStl} ({GetSize(roundtripStl)} bytes)");

var originalStl = FindLatest(sourceDir, "plate.stl");
if (originalStl is not null)
{
    var originalCount = ReadStlTriangleCount(originalStl);
    Console.WriteLine($"Original STL triangles: {originalCount}");
    Console.WriteLine($"Round-trip STL triangles: {triangles.Count}");
}

static string? FindLatest(string root, string fileName)
{
    if (!Directory.Exists(root))
    {
        return null;
    }

    string? latest = null;
    var latestTime = DateTime.MinValue;

    foreach (var path in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
    {
        var info = new FileInfo(path);
        if (info.LastWriteTimeUtc > latestTime)
        {
            latest = path;
            latestTime = info.LastWriteTimeUtc;
        }
    }

    return latest;
}

static long GetSize(string path) => new FileInfo(path).Length;

static uint ReadStlTriangleCount(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream);
    var header = reader.ReadBytes(80);
    if (header.Length != 80)
    {
        throw new InvalidDataException("STL header incomplete.");
    }

    return reader.ReadUInt32();
}
