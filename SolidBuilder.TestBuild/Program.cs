using SolidBuilder.Voxels;

var plate = VoxelKernel.CreateEmpty();

// Base plate: 30 x 300 x 4 voxels starting at the origin.
VoxelKernel.AddBox(plate, new Int3(0, 0, 0), new Int3(30, 300, 4));

// Keyboard column cutouts (19 x 19 x 4), fully through the plate.
VoxelKernel.SubtractBox(plate, new Int3(5, 40, 0), new Int3(24, 59, 4));
VoxelKernel.SubtractBox(plate, new Int3(5, 120, 0), new Int3(24, 139, 4));

var (min, max) = VoxelKernel.GetBounds(plate);
var volume = VoxelKernel.GetVolume(plate);
var surface = VoxelKernel.GetSurfaceArea(plate);
var watertight = VoxelKernel.IsWatertight(plate);

Console.WriteLine("Keyboard column plate:");
Console.WriteLine($"  Bounds min=({min.X}, {min.Y}, {min.Z}) maxExclusive=({max.X}, {max.Y}, {max.Z})");
Console.WriteLine($"  Volume (voxels): {volume}");
Console.WriteLine($"  Surface area (faces): {surface}");
Console.WriteLine($"  Watertight: {watertight}");

const long expectedVolume = 33112;
if (volume != expectedVolume)
{
    Console.WriteLine($"  Warning: expected volume {expectedVolume}, got {volume}");
}

var outputDir = AppContext.BaseDirectory;
var autoPath = Path.Combine(outputDir, "plate_auto.sbvx");
var densePath = Path.Combine(outputDir, "plate_dense.sbvx");
var sparsePath = Path.Combine(outputDir, "plate_sparse.sbvx");
var stlPath = Path.Combine(outputDir, "plate.stl");

using (var stream = File.Create(autoPath))
{
    VoxelKernel.AutoWriteSbvx(plate, stream);
}

using (var stream = File.Create(densePath))
{
    VoxelKernel.WriteSbvxDense(plate, stream);
}

using (var stream = File.Create(sparsePath))
{
    VoxelKernel.WriteSbvxSparse(plate, stream);
}

using (var stream = File.Create(stlPath))
{
    VoxelKernel.WriteBinaryStl(plate, "KeyboardPlate", stream);
}

Console.WriteLine("Saved outputs:");
Console.WriteLine($"  plate_auto.sbvx   {GetSize(autoPath)} bytes");
Console.WriteLine($"  plate_dense.sbvx  {GetSize(densePath)} bytes");
Console.WriteLine($"  plate_sparse.sbvx {GetSize(sparsePath)} bytes");
Console.WriteLine($"  plate.stl         {GetSize(stlPath)} bytes");

static long GetSize(string path) => new FileInfo(path).Length;
