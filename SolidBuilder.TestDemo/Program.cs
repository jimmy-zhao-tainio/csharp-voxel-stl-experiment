using System;
using System.IO;
using SolidBuilder.Voxels;
using VoxelCad.Builder;
using VoxelCad.Core;
using VoxelCad.Scene;

var settings = new ProjectSettings(
    units: Units.Millimeters,
    voxelsPerUnit: 1,
    revoxelization: new RevoxelizationSettings
    {
        ConservativeObb = true,
        SamplesPerAxis = 7,   // higher ⇒ smoother rotated surfaces
        Epsilon = 1e-9
    },
    quality: QualityProfile.High);

var scene = new VoxelCad.Scene.Scene(settings);

var basePart = scene.NewPart("base", builder =>
{
    builder.Box(new Int3(0, 0, 0), new Int3(40, 40, 6));
}, addInstance: false);
var baseInstance = scene.AddInstance(basePart);

var towerPart = scene.NewPart("tower", builder =>
{
    builder.CylinderZ(0, 0, 0, 28, 8);
}, addInstance: false);
var towerInstance = scene.AddInstance(towerPart);
towerInstance.Move(20, 20, 6);

var weldedCorePart = scene.Weld(baseInstance, towerInstance, radius: 2, replaceInstances: false);
scene.RemoveInstance(baseInstance);
scene.RemoveInstance(towerInstance);
var coreInstance = scene.AddInstance(weldedCorePart);

var wingPart = scene.NewPart("wing", builder =>
{
    builder.Box(new Int3(-4, 0, 0), new Int3(4, 28, 3));
}, addInstance: false);

var wingA = scene.AddInstance(wingPart);
wingA.Move(20, 20, 16);
wingA.RotateAny(Axis.Z, 25, new Int3(20, 20, 16));

var wingB = scene.AddInstance(wingPart);
wingB.Move(20, 20, 16);
wingB.Rotate90(Axis.Z, 1, new Int3(20, 20, 16));

var mergedWingPart = scene.Weld(coreInstance, wingA, radius: 2, replaceInstances: false);
scene.RemoveInstance(coreInstance);
scene.RemoveInstance(wingA);
coreInstance = scene.AddInstance(mergedWingPart);

var finalCorePart = scene.Weld(coreInstance, wingB, radius: 2, replaceInstances: false);
scene.RemoveInstance(coreInstance);
scene.RemoveInstance(wingB);
scene.AddInstance(finalCorePart);

var ventPart = scene.NewPart("vent", builder =>
{
    builder.CylinderZ(0, 0, 0, 8, 3);
}, role: Role.Hole, addInstance: false);
var vent = scene.AddInstance(ventPart, Role.Hole);
vent.Move(20, 20, 6);

var draftSolid = scene.Bake();
var draftWatertight = VoxelKernel.IsWatertight(draftSolid);
var solid = scene.BakeForQuality(QualityProfile.High);
var watertight = VoxelKernel.IsWatertight(solid);
var voxels = VoxelKernel.GetVolume(solid);

var exportOptions = new ExportOptions
{
    Engine = MeshEngine.VoxelFaces,
    Quantize = QuantizeOptions.Units(0.01)
};

var outputDirectory = AppContext.BaseDirectory;
var stlPath = Path.Combine(outputDirectory, "demo_tower.stl");
scene.Project.ExportStl(solid, stlPath, exportOptions);

var fileInfo = new FileInfo(stlPath);
fileInfo.Refresh();
var sizeKb = fileInfo.Length / 1024.0;

Console.WriteLine("SolidBuilder Test Demo");
Console.WriteLine($"  Units: {settings.Units}");
Console.WriteLine($"  Voxels per unit: {settings.VoxelsPerUnit}");
Console.WriteLine($"  Draft watertight: {draftWatertight}");
Console.WriteLine($"  Watertight: {watertight}");
Console.WriteLine($"  Voxels: {voxels}");
Console.WriteLine($"  STL path: {stlPath}");
Console.WriteLine($"  File size (KB): {Math.Round(sizeKb, 1)}");
