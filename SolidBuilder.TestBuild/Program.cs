using SolidBuilder.Voxels;
using VoxelCad.Builder;

var panelBuilder = new VoxelBuilder()
    .Box(new Int3(0, 0, 0), new Int3(60, 240, 6))
    .Subtract(b =>
        b.RotateAnyAround(Axis.Z, 17.5, new Int3(30, 120, 0), holes =>
        {
            for (var gx = 0; gx < 3; gx++)
            {
                for (var gy = 0; gy < 8; gy++)
                {
                    var cx = 10 + gx * 20;
                    var cy = 20 + gy * 30;
                    holes.CylinderZ(cx, cy, 0, 6, 5);
                }
            }
        }));

var panel = panelBuilder.Build();

var volume = VoxelKernel.GetVolume(panel);
var surface = VoxelKernel.GetSurfaceArea(panel);
var watertight = VoxelKernel.IsWatertight(panel);

Console.WriteLine("Rotated hole panel:");
Console.WriteLine($"  Volume (voxels): {volume}");
Console.WriteLine($"  Surface area (faces): {surface}");
Console.WriteLine($"  Watertight: {watertight}");

var outputDir = AppContext.BaseDirectory;
var autoPath = Path.Combine(outputDir, "panel_rotated_auto.sbvx");
var stlPath = Path.Combine(outputDir, "panel_rotated.stl");

using (var stream = File.Create(autoPath))
{
    VoxelKernel.AutoWriteSbvx(panel, stream);
}

using (var stream = File.Create(stlPath))
{
    VoxelKernel.WriteBinaryStl(panel, "RotatedPanel", stream);
}

Console.WriteLine("Saved outputs:");
Console.WriteLine($"  panel_rotated_auto.sbvx {GetSize(autoPath)} bytes");
Console.WriteLine($"  panel_rotated.stl       {GetSize(stlPath)} bytes");

static long GetSize(string path) => new FileInfo(path).Length;
