using System;
using SolidBuilder.Voxels;

namespace VoxelCad.Core;

public enum Units
{
    Millimeters,
    Inches
}

public enum QualityProfile
{
    Draft,
    Medium,
    High
}

public enum PartRole
{
    Solid,
    Subtractive
}

public enum SbvxCompression
{
    None,
    Deflate,
    Zstd
}

public readonly struct SaveOptions
{
    public SaveOptions(SbvxCompression compression = SbvxCompression.Deflate, int compressionLevel = 6)
    {
        Compression = compression;
        CompressionLevel = compressionLevel;
    }

    public SbvxCompression Compression { get; }
    public int CompressionLevel { get; }

    public static SaveOptions Default => new();
}

public enum MeshEngine
{
    VoxelFaces,
    SurfaceNets
}

public sealed class QuantizeOptions
{
    public static QuantizeOptions None() => new();

    public static QuantizeOptions Units(double step) => new() { StepUnits = step };

    public double StepUnits { get; set; }
}

public sealed class ExportOptions
{
    public ExportOptions()
    {
    }

    public ExportOptions(QualityProfile quality)
    {
        Quality = quality;
    }

    public MeshEngine Engine { get; set; } = MeshEngine.VoxelFaces;

    public double IsoLevel { get; set; } = 0.5;

    public int SmoothingPasses { get; set; }

    public QuantizeOptions Quantize { get; set; } = QuantizeOptions.None();

    public QualityProfile Quality { get; set; } = QualityProfile.Medium;

    public static ExportOptions Default => new();
}

public sealed class RevoxelizationSettings
{
    public static RevoxelizationSettings Default { get; } = new RevoxelizationSettings();

    public bool ConservativeObb { get; init; } = true;
    public int SamplesPerAxis { get; init; } = 3;
    public double Epsilon { get; init; } = 1e-9;

    public RotateOptions CreateOptions(Axis axis, double degrees, Int3 pivot)
    {
        return ApplyDefaults(new RotateOptions
        {
            Axis = axis,
            Degrees = degrees,
            Pivot = pivot,
            ConservativeObb = ConservativeObb,
            SamplesPerAxis = SamplesPerAxis,
            Epsilon = Epsilon
        });
    }

    public RotateOptions ApplyDefaults(RotateOptions options)
    {
        if (options.SamplesPerAxis <= 0)
        {
            options.SamplesPerAxis = SamplesPerAxis;
        }

        if (options.Epsilon <= 0)
        {
            options.Epsilon = Epsilon;
        }

        return options;
    }
}

public sealed class ProjectSettings
{
    public ProjectSettings(
        Units units = Units.Millimeters,
        int voxelsPerUnit = 1,
        RevoxelizationSettings? revoxelization = null,
        QualityProfile exportQuality = QualityProfile.Medium)
    {
        if (voxelsPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelsPerUnit), "Voxels per unit must be positive.");
        }

        Units = units;
        VoxelsPerUnit = voxelsPerUnit;
        Revoxelization = revoxelization ?? RevoxelizationSettings.Default;
        ExportQuality = exportQuality;
    }

    public Units Units { get; }
    public int VoxelsPerUnit { get; }
    public RevoxelizationSettings Revoxelization { get; }
    public QualityProfile ExportQuality { get; }
}
