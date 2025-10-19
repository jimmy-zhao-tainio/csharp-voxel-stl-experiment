using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using SolidBuilder.Voxels;
using VoxelCad.Builder;

namespace VoxelCad.Core;

public sealed class Project
{
    public Project(ProjectSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public ProjectSettings Settings { get; }

    public Scene NewScene()
    {
        return new Scene(this);
    }
}

public sealed class Scene
{
    private readonly Project _project;
    private readonly List<ScenePart> _parts = new();

    internal Scene(Project project)
    {
        _project = project;
    }

    public ScenePart NewPart(string name, Action<UnitBuilder> build, PartRole role = PartRole.Solid)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Part name cannot be null or whitespace.", nameof(name));
        }

        if (build is null)
        {
            throw new ArgumentNullException(nameof(build));
        }

        var voxelBuilder = new VoxelBuilder();
        var unitBuilder = new UnitBuilder(voxelBuilder, _project.Settings.VoxelsPerUnit, _project.Settings.Revoxelization);
        build(unitBuilder);
        var solid = voxelBuilder.Build();
        var part = new ScenePart(name, role, solid);
        _parts.Add(part);
        return part;
    }

    public void SaveProject(string path, SaveOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        var saveOptions = options ?? SaveOptions.Default;
        var solid = BuildSolid();

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var buffer = new MemoryStream();
        VoxelKernel.AutoWriteSbvx(solid, buffer);
        var data = buffer.ToArray();

        using var output = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        switch (saveOptions.Compression)
        {
            case SbvxCompression.None:
                output.Write(data, 0, data.Length);
                break;
            case SbvxCompression.Deflate:
                using (var deflate = new DeflateStream(output, MapCompressionLevel(saveOptions.CompressionLevel), leaveOpen: false))
                {
                    deflate.Write(data, 0, data.Length);
                }
                break;
            case SbvxCompression.Zstd:
                var zstd = TryCreateZstdStream(output, saveOptions.CompressionLevel);
                if (zstd is null)
                {
                    using var fallback = new DeflateStream(output, MapCompressionLevel(saveOptions.CompressionLevel), leaveOpen: false);
                    fallback.Write(data, 0, data.Length);
                }
                else
                {
                    using (zstd)
                    {
                        zstd.Write(data, 0, data.Length);
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options), "Unknown compression choice.");
        }
    }

    public void ExportStl(string path, ExportOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var solid = BuildSolid();
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        VoxelKernel.WriteBinaryStl(solid, Path.GetFileNameWithoutExtension(path), stream);
    }

    public VoxelSolid BuildSolid()
    {
        var result = VoxelKernel.CreateEmpty();
        foreach (var part in _parts)
        {
            switch (part.Role)
            {
                case PartRole.Solid:
                    VoxelKernel.AddVoxels(result, part.Solid.Voxels);
                    break;
                case PartRole.Subtractive:
                    VoxelKernel.RemoveVoxels(result, part.Solid.Voxels);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(part.Role));
            }
        }

        return result;
    }

    private static CompressionLevel MapCompressionLevel(int level)
    {
        if (level <= 1)
        {
            return CompressionLevel.Fastest;
        }

        if (level >= 9)
        {
            return CompressionLevel.SmallestSize;
        }

        return CompressionLevel.Optimal;
    }

    private static Stream? TryCreateZstdStream(Stream output, int level)
    {
        var type = Type.GetType("ZstdSharp.CompressionStream, ZstdSharp")
                   ?? Type.GetType("ZstdNet.CompressionStream, ZstdNet");
        if (type is null)
        {
            return null;
        }

        try
        {
            // Prefer constructor (Stream, int)
            var ctor = type.GetConstructor(new[] { typeof(Stream), typeof(int) })
                       ?? type.GetConstructor(new[] { typeof(Stream) });
            if (ctor is null)
            {
                return null;
            }

            return ctor.GetParameters().Length switch
            {
                2 => (Stream)ctor.Invoke(new object[] { output, level }),
                1 => (Stream)ctor.Invoke(new object[] { output }),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}

public sealed class ScenePart
{
    internal ScenePart(string name, PartRole role, VoxelSolid solid)
    {
        Name = name;
        Role = role;
        Solid = solid;
    }

    public string Name { get; }
    public PartRole Role { get; }
    public VoxelSolid Solid { get; }
}

public sealed class UnitBuilder
{
    private readonly VoxelBuilder _inner;
    private readonly int _scale;
    private readonly RevoxelizationSettings _revoxelization;

    internal UnitBuilder(VoxelBuilder inner, int scale, RevoxelizationSettings revoxelization)
    {
        _inner = inner;
        _scale = scale;
        _revoxelization = revoxelization;
    }

    public UnitBuilder Box(Int3 min, Int3 maxExclusive)
    {
        _inner.Box(Scale(min), Scale(maxExclusive));
        return this;
    }

    public UnitBuilder CutBox(Int3 min, Int3 maxExclusive)
    {
        _inner.CutBox(Scale(min), Scale(maxExclusive));
        return this;
    }

    public UnitBuilder CylinderZ(int cx, int cy, int zMin, int zMaxExclusive, int radius)
    {
        _inner.CylinderZ(Scale(cx), Scale(cy), Scale(zMin), Scale(zMaxExclusive), Scale(radius));
        return this;
    }

    public UnitBuilder CylinderX(int cy, int cz, int xMin, int xMaxExclusive, int radius)
    {
        _inner.CylinderX(Scale(cy), Scale(cz), Scale(xMin), Scale(xMaxExclusive), Scale(radius));
        return this;
    }

    public UnitBuilder CylinderY(int cx, int cz, int yMin, int yMaxExclusive, int radius)
    {
        _inner.CylinderY(Scale(cx), Scale(cz), Scale(yMin), Scale(yMaxExclusive), Scale(radius));
        return this;
    }

    public UnitBuilder CutCylinderZ(int cx, int cy, int zMin, int zMaxExclusive, int radius)
    {
        _inner.CutCylinderZ(Scale(cx), Scale(cy), Scale(zMin), Scale(zMaxExclusive), Scale(radius));
        return this;
    }

    public UnitBuilder CutCylinderX(int cy, int cz, int xMin, int xMaxExclusive, int radius)
    {
        _inner.CutCylinderX(Scale(cy), Scale(cz), Scale(xMin), Scale(xMaxExclusive), Scale(radius));
        return this;
    }

    public UnitBuilder CutCylinderY(int cx, int cz, int yMin, int yMaxExclusive, int radius)
    {
        _inner.CutCylinderY(Scale(cx), Scale(cz), Scale(yMin), Scale(yMaxExclusive), Scale(radius));
        return this;
    }

    public UnitBuilder Sphere(Int3 center, int radius)
    {
        _inner.Sphere(Scale(center), Scale(radius));
        return this;
    }

    public UnitBuilder CutSphere(Int3 center, int radius)
    {
        _inner.CutSphere(Scale(center), Scale(radius));
        return this;
    }

    public UnitBuilder Translate(int dx, int dy, int dz)
    {
        _inner.Translate(Scale(dx), Scale(dy), Scale(dz));
        return this;
    }

    public UnitBuilder Rotate90(Axis axis, int quarterTurns)
    {
        _inner.Rotate90(axis, quarterTurns);
        return this;
    }

    public UnitBuilder Rotate90Around(Axis axis, int quarterTurns, Int3 pivot)
    {
        _inner.Rotate90Around(axis, quarterTurns, Scale(pivot));
        return this;
    }

    public UnitBuilder Mirror(Axis axis)
    {
        _inner.Mirror(axis);
        return this;
    }

    public UnitBuilder MirrorAround(Axis axis, Int3 pivot)
    {
        _inner.MirrorAround(axis, Scale(pivot));
        return this;
    }

    public UnitBuilder ResetTransform()
    {
        _inner.ResetTransform();
        return this;
    }

    public UnitBuilder Place(Int3 offset, Action<UnitBuilder> scope)
    {
        _inner.Place(Scale(offset), child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder ArrayX(int count, int step, Action<UnitBuilder> scope)
    {
        _inner.ArrayX(count, Scale(step), child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder ArrayY(int count, int step, Action<UnitBuilder> scope)
    {
        _inner.ArrayY(count, Scale(step), child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder Grid(int countX, int stepX, int countY, int stepY, Action<UnitBuilder> scope)
    {
        _inner.Grid(countX, Scale(stepX), countY, Scale(stepY), child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder Union(Action<UnitBuilder> scope)
    {
        _inner.Union(child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder Subtract(Action<UnitBuilder> scope)
    {
        _inner.Subtract(child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder Intersect(Action<UnitBuilder> scope)
    {
        _inner.Intersect(child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder RotateAny(Axis axis, double degrees, Action<UnitBuilder> scope)
    {
        var options = _revoxelization.CreateOptions(axis, degrees, Scale(Int3Zero));
        _inner.RotateAnyWith(axis, degrees, options, child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder RotateAnyAround(Axis axis, double degrees, Int3 pivot, Action<UnitBuilder> scope)
    {
        var options = _revoxelization.CreateOptions(axis, degrees, Scale(pivot));
        _inner.RotateAnyWith(axis, degrees, options, child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });
        return this;
    }

    public UnitBuilder RotateAnyWith(Axis axis, double degrees, RotateOptions options, Action<UnitBuilder> scope)
    {
        var merged = _revoxelization.ApplyDefaults(options);
        merged.Axis = axis;
        merged.Degrees = degrees;
        merged.Pivot = Scale(merged.Pivot);

        _inner.RotateAnyWith(axis, degrees, merged, child =>
        {
            scope(new UnitBuilder(child, _scale, _revoxelization));
        });

        return this;
    }

    private static readonly Int3 Int3Zero = new(0, 0, 0);

    private Int3 Scale(Int3 value) => new(value.X * _scale, value.Y * _scale, value.Z * _scale);
    private int Scale(int value) => value * _scale;
}
