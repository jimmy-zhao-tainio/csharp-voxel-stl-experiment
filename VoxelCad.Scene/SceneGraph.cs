using System;
using System.Collections.Generic;
using SolidBuilder.Voxels;
using VoxelCad.Core;

namespace VoxelCad.Scene;

public sealed class Part
{
    public Part(string name, VoxelSolid model, Role defaultRole = Role.Solid)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        DefaultRole = defaultRole;
    }

    public string Name { get; }
    public VoxelSolid Model { get; }
    public Role DefaultRole { get; }
}

public enum Role
{
    Solid,
    Hole,
    Intersect
}

public enum Metric
{
    LInf,
    L1,
    L2
}

public readonly struct AabbI
{
    public AabbI(Int3 min, Int3 max)
    {
        if (min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
        {
            throw new ArgumentException("Invalid AABB bounds.");
        }

        Min = min;
        Max = max;
    }

    public Int3 Min { get; }
    public Int3 Max { get; }

    public bool Contains(Int3 point)
    {
        return point.X >= Min.X && point.X < Max.X &&
               point.Y >= Min.Y && point.Y < Max.Y &&
               point.Z >= Min.Z && point.Z < Max.Z;
    }
}

public sealed class FrameExact
{
    public FrameExact()
    {
        Translation = new Int3(0, 0, 0);
        Matrix = Identity();
    }

    public Int3 Translation { get; set; }
    public int[,] Matrix { get; }

    public FrameExact Clone()
    {
        var clone = new FrameExact();
        clone.Translation = new Int3(Translation.X, Translation.Y, Translation.Z);
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                clone.Matrix[i, j] = Matrix[i, j];
            }
        }

        return clone;
    }

    private static int[,] Identity()
    {
        var matrix = new int[3, 3];
        matrix[0, 0] = 1;
        matrix[1, 1] = 1;
        matrix[2, 2] = 1;
        return matrix;
    }
}

public sealed class FrameAny
{
    public Axis Axis { get; init; }
    public double Degrees { get; init; }
    public Int3 Pivot { get; init; }
    public double Epsilon { get; init; }
    public int SamplesPerAxis { get; init; }
}

public sealed class Instance
{
    private readonly Scene _scene;

    internal Instance(Scene scene, Part part, Role role)
    {
        _scene = scene;
        Part = part;
        Role = role;
        Exact = new FrameExact();
    }

    public Part Part { get; }
    public Role Role { get; private set; }
    public FrameExact Exact { get; }
    public FrameAny? Any { get; private set; }

    public Instance SetRole(Role role)
    {
        Role = role;
        return this;
    }

    public Instance Move(int dx, int dy, int dz)
    {
        var t = Exact.Translation;
        Exact.Translation = new Int3(t.X + dx, t.Y + dy, t.Z + dz);
        return this;
    }

    public Instance Rotate90(Axis axis, int quarterTurns, Int3? pivot = null)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0)
        {
            return this;
        }

        var pivotValue = pivot ?? default;
        var rotation = CreateRotationMatrix(axis, turns);
        ApplyMatrix(rotation, pivotValue);
        return this;
    }

    public Instance Mirror(Axis axis, Int3? pivot = null)
    {
        var pivotValue = pivot ?? default;
        var reflection = CreateReflectionMatrix(axis);
        ApplyMatrix(reflection, pivotValue);
        return this;
    }

    public Instance RotateAny(Axis axis, double degrees, Int3? pivot = null, double? epsilon = null, int? samplesPerAxis = null)
    {
        var pivotValue = pivot ?? default;
        Any = new FrameAny
        {
            Axis = axis,
            Degrees = degrees,
            Pivot = pivotValue,
            Epsilon = epsilon.GetValueOrDefault(0),
            SamplesPerAxis = samplesPerAxis.GetValueOrDefault(0)
        };
        return this;
    }

    private void ApplyMatrix(int[,] rotation, Int3 pivot)
    {
        var current = Exact.Matrix;
        var composed = Multiply(rotation, current);
        CopyMatrix(composed, current);

        var translation = Exact.Translation;
        var vector = new Int3(translation.X - pivot.X, translation.Y - pivot.Y, translation.Z - pivot.Z);
        var rotated = TransformVector(rotation, vector);
        Exact.Translation = new Int3(rotated.X + pivot.X, rotated.Y + pivot.Y, rotated.Z + pivot.Z);
    }

    private static int[,] CreateRotationMatrix(Axis axis, int turns)
    {
        int[,] matrix = new int[3, 3];
        matrix[0, 0] = 1;
        matrix[1, 1] = 1;
        matrix[2, 2] = 1;

        for (var i = 0; i < turns; i++)
        {
            matrix = axis switch
            {
                Axis.X => Multiply(new int[3, 3]
                {
                    { 1, 0, 0 },
                    { 0, 0, -1 },
                    { 0, 1, 0 }
                }, matrix),
                Axis.Y => Multiply(new int[3, 3]
                {
                    { 0, 0, 1 },
                    { 0, 1, 0 },
                    { -1, 0, 0 }
                }, matrix),
                Axis.Z => Multiply(new int[3, 3]
                {
                    { 0, -1, 0 },
                    { 1, 0, 0 },
                    { 0, 0, 1 }
                }, matrix),
                _ => matrix
            };
        }

        return matrix;
    }

    private static int[,] CreateReflectionMatrix(Axis axis)
    {
        return axis switch
        {
            Axis.X => new int[3, 3]
            {
                { -1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, 1 }
            },
            Axis.Y => new int[3, 3]
            {
                { 1, 0, 0 },
                { 0, -1, 0 },
                { 0, 0, 1 }
            },
            Axis.Z => new int[3, 3]
            {
                { 1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, -1 }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
    }

    private static int[,] Multiply(int[,] left, int[,] right)
    {
        var result = new int[3, 3];
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                result[i, j] = 0;
                for (var k = 0; k < 3; k++)
                {
                    result[i, j] += left[i, k] * right[k, j];
                }
            }
        }

        return result;
    }

    private static void CopyMatrix(int[,] source, int[,] destination)
    {
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                destination[i, j] = source[i, j];
            }
        }
    }

    private static Int3 TransformVector(int[,] matrix, Int3 vector)
    {
        var x = matrix[0, 0] * vector.X + matrix[0, 1] * vector.Y + matrix[0, 2] * vector.Z;
        var y = matrix[1, 0] * vector.X + matrix[1, 1] * vector.Y + matrix[1, 2] * vector.Z;
        var z = matrix[2, 0] * vector.X + matrix[2, 1] * vector.Y + matrix[2, 2] * vector.Z;
        return new Int3(x, y, z);
    }
}

public sealed class Scene
{
    private readonly List<Part> _parts = new();
    private readonly List<Instance> _instances = new();

    public Scene(ProjectSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Project = new Project(Settings);
    }

    public IReadOnlyList<Instance> Instances => _instances;
    public ProjectSettings Settings { get; }
    public Project Project { get; }

    public Instance AddInstance(Part part, Role role = Role.Solid)
    {
        if (part is null) throw new ArgumentNullException(nameof(part));

        var actualRole = role == Role.Solid && part.DefaultRole != Role.Solid
            ? part.DefaultRole
            : role;

        var instance = new Instance(this, part, actualRole);
        _instances.Add(instance);
        return instance;
    }

    public Part NewPart(string name, Action<UnitBuilder> build, Role role = Role.Solid, bool addInstance = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Part name cannot be null or whitespace.", nameof(name));
        }

        if (build is null)
        {
            throw new ArgumentNullException(nameof(build));
        }

        var coreScene = Project.NewScene();
        var built = coreScene.NewPart(name, build, MapRole(role));
        var part = RegisterPart(built.Name, built.Solid, role);

        if (addInstance)
        {
            AddInstance(part, role);
        }

        return part;
    }

    public bool RemoveInstance(Instance instance)
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        return _instances.Remove(instance);
    }

    private Part RegisterPart(string name, VoxelSolid solid, Role defaultRole = Role.Solid)
    {
        var part = new Part(name, solid, defaultRole);
        _parts.Add(part);
        return part;
    }

    private static PartRole MapRole(Role role)
    {
        return role switch
        {
            Role.Solid => PartRole.Solid,
            _ => PartRole.Subtractive
        };
    }

    public VoxelSolid Bake(BakeOptions? options = null)
    {
        var opts = options ?? BakeOptions.Default;
        var revoxelization = opts.RevoxelizeOverride ?? Settings.Revoxelization;
        var voxelsPerUnit = opts.VoxelsPerUnitOverride ?? Settings.VoxelsPerUnit;
        var baseScale = Settings.VoxelsPerUnit;

        VoxelSolid? result = null;

        foreach (var instance in _instances)
        {
            var solid = BuildSolidForInstance(instance, revoxelization, baseScale, voxelsPerUnit);

            if (result is null)
            {
                result = solid;
                continue;
            }

            result = instance.Role switch
            {
                Role.Solid => VoxelKernel.Union(result, solid),
                Role.Hole => VoxelKernel.Subtract(result, solid),
                Role.Intersect => VoxelKernel.Intersect(result, solid),
                _ => result
            };
        }

        return result ?? VoxelKernel.CreateEmpty();
    }

    public VoxelSolid BakeAtResolution(int voxelsPerUnit)
    {
        if (voxelsPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(voxelsPerUnit), "Voxels per unit must be positive.");
        }

        var baseVoxelsPerUnit = Settings.VoxelsPerUnit;
        if (voxelsPerUnit == baseVoxelsPerUnit)
        {
            return Bake();
        }

        if (voxelsPerUnit % baseVoxelsPerUnit != 0)
        {
            throw new ArgumentException("Requested resolution must be an integer multiple of the project setting.", nameof(voxelsPerUnit));
        }

        var scale = voxelsPerUnit / baseVoxelsPerUnit;
        var baked = Bake();
        return ScaleSolid(baked, scale);
    }

    public VoxelSolid MorphOpen(int radius, Metric metric = Metric.LInf)
    {
        return MorphOpen(radius, metric, Bake());
    }

    public VoxelSolid MorphOpen(int radius, Metric metric, VoxelSolid source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return VoxelKernel.Open(source, radius, ToKernelMetric(metric));
    }

    public VoxelSolid MorphClose(int radius, Metric metric = Metric.LInf)
    {
        return MorphClose(radius, metric, Bake());
    }

    public VoxelSolid MorphClose(int radius, Metric metric, VoxelSolid source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return VoxelKernel.Close(source, radius, ToKernelMetric(metric));
    }

    public VoxelSolid BakeForQuality(QualityProfile profile)
    {
        var vpu = Settings.VoxelsPerUnit;
        return profile switch
        {
            QualityProfile.Draft => Bake(),
            QualityProfile.Medium => MorphClose(1, Metric.LInf, BakeAtResolution(vpu * 2)),
            QualityProfile.High =>
                MorphOpen(1, Metric.LInf,
                    MorphClose(1, Metric.LInf, BakeAtResolution(vpu * 3))),
            _ => Bake()
        };
    }

    public void ExportStl(string path, QualityProfile? profile = null, ExportOptions? options = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        var quality = profile ?? Settings.Quality;
        var solid = BakeForQuality(quality);
        var exportOptions = options ?? new ExportOptions();

        if (options is null)
        {
            switch (quality)
            {
                case QualityProfile.Medium:
                    exportOptions.Quantize = QuantizeOptions.Units(0.02);
                    break;
                case QualityProfile.High:
                    exportOptions.Quantize = QuantizeOptions.Units(0.01);
                    break;
            }
        }

        Project.ExportStl(solid, path, exportOptions);
    }

    private static VoxelKernel.Metric ToKernelMetric(Metric metric)
    {
        return metric switch
        {
            Metric.LInf => VoxelKernel.Metric.LInf,
            Metric.L1 => VoxelKernel.Metric.L1,
            _ => VoxelKernel.Metric.LInf
        };
    }

    private static VoxelSolid ScaleSolid(VoxelSolid source, int scale)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be positive.");
        }

        if (scale == 1)
        {
            var clone = VoxelKernel.CreateEmpty();
            VoxelKernel.AddVoxels(clone, source.Voxels);
            return clone;
        }

        var result = VoxelKernel.CreateEmpty();
        foreach (var voxel in source.Voxels)
        {
            var baseX = voxel.X * scale;
            var baseY = voxel.Y * scale;
            var baseZ = voxel.Z * scale;

            for (var dx = 0; dx < scale; dx++)
            {
                for (var dy = 0; dy < scale; dy++)
                {
                    for (var dz = 0; dz < scale; dz++)
                    {
                        var scaled = new Int3(baseX + dx, baseY + dy, baseZ + dz);
                        VoxelKernel.AddVoxel(result, scaled);
                    }
                }
            }
        }

        return result;
    }

    public Part Weld(Instance a, Instance b, int? radius = null, Metric metric = Metric.LInf, bool replaceInstances = true, string? name = null)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        var revo = Settings.Revoxelization;
        var baseScale = Settings.VoxelsPerUnit;
        var solidA = BuildSolidForInstance(a, revo, baseScale, baseScale);
        var solidB = BuildSolidForInstance(b, revo, baseScale, baseScale);
        var union = VoxelKernel.Union(solidA, solidB);

        var closingRadius = radius ?? DetermineClosingRadius(union, solidA, solidB, metric);
        var closed = closingRadius > 0 ? Close(union, closingRadius, metric) : union;

        var partName = name ?? $"Weld({a.Part.Name},{b.Part.Name})";
        var part = RegisterPart(partName, closed);

        if (replaceInstances)
        {
            RemoveInstance(a);
            RemoveInstance(b);
            AddInstance(part, Role.Solid);
        }

        return part;
    }

    public Part BridgeAxis(Instance a, Instance b, Axis axis, int thickness = 1, AabbI? mask = null, string? name = null, bool addInstance = true)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (thickness < 1) throw new ArgumentOutOfRangeException(nameof(thickness));

        var revo = Settings.Revoxelization;
        var baseScale = Settings.VoxelsPerUnit;
        var solidA = BuildSolidForInstance(a, revo, baseScale, baseScale);
        var solidB = BuildSolidForInstance(b, revo, baseScale, baseScale);

        var bridge = BuildBridgePrism(solidA, solidB, axis, thickness, mask);
        var combined = VoxelKernel.Union(VoxelKernel.Union(solidA, solidB), bridge);

        var partName = name ?? $"Bridge({axis})";
        var part = RegisterPart(partName, combined);

        if (addInstance)
        {
            AddInstance(part, Role.Solid);
        }

        return part;
    }

    public Part Strut(Instance a, Instance b, int radius = 1, string? name = null, bool addInstance = true)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

        var revo = Settings.Revoxelization;
        var baseScale = Settings.VoxelsPerUnit;
        var solidA = BuildSolidForInstance(a, revo, baseScale, baseScale);
        var solidB = BuildSolidForInstance(b, revo, baseScale, baseScale);

        var strut = BuildStrut(solidA, solidB, radius);
        var combined = VoxelKernel.Union(VoxelKernel.Union(solidA, solidB), strut);

        var partName = name ?? $"Strut({a.Part.Name},{b.Part.Name})";
        var part = RegisterPart(partName, combined);

        if (addInstance)
        {
            AddInstance(part, Role.Solid);
        }

        return part;
    }

    private VoxelSolid BuildSolidForInstance(Instance instance, RevoxelizationSettings revoxelization, int baseScale, int targetScale)
    {
        var solid = CloneSolid(instance.Part.Model);

        if (targetScale != baseScale)
        {
            solid = ResampleSolid(solid, baseScale, targetScale);
        }

        solid = ApplyExact(solid, instance.Exact);

        if (instance.Any is FrameAny any)
        {
            solid = ApplyAny(solid, any, revoxelization);
        }

        return solid;
    }

    private static VoxelSolid CloneSolid(VoxelSolid source)
    {
        var clone = VoxelKernel.CreateEmpty();
        VoxelKernel.AddVoxels(clone, source.Voxels);
        return clone;
    }

    private static VoxelSolid ApplyExact(VoxelSolid solid, FrameExact exact)
    {
        var result = VoxelKernel.CreateEmpty();
        foreach (var voxel in solid.Voxels)
        {
            var transformed = TransformVoxel(exact.Matrix, voxel);
            transformed = new Int3(transformed.X + exact.Translation.X, transformed.Y + exact.Translation.Y, transformed.Z + exact.Translation.Z);
            VoxelKernel.AddVoxel(result, transformed);
        }

        return result;
    }

    private static Int3 TransformVoxel(int[,] matrix, Int3 voxel)
    {
        var x = matrix[0, 0] * voxel.X + matrix[0, 1] * voxel.Y + matrix[0, 2] * voxel.Z;
        var y = matrix[1, 0] * voxel.X + matrix[1, 1] * voxel.Y + matrix[1, 2] * voxel.Z;
        var z = matrix[2, 0] * voxel.X + matrix[2, 1] * voxel.Y + matrix[2, 2] * voxel.Z;
        return new Int3(x, y, z);
    }

    private static VoxelSolid ApplyAny(VoxelSolid solid, FrameAny any, RevoxelizationSettings settings)
    {
        var options = settings.CreateOptions(any.Axis, any.Degrees, any.Pivot);
        if (any.SamplesPerAxis > 0)
        {
            options.SamplesPerAxis = any.SamplesPerAxis;
        }

        if (any.Epsilon > 0)
        {
            options.Epsilon = any.Epsilon;
        }

        return VoxelKernel.RotateRevoxelized(solid, options);
    }

    private static VoxelSolid ResampleSolid(VoxelSolid solid, int baseScale, int targetScale)
    {
        if (targetScale == baseScale)
        {
            return CloneSolid(solid);
        }

        if (targetScale < baseScale || targetScale % baseScale != 0)
        {
            throw new InvalidOperationException("VoxelsPerUnitOverride must be a positive multiple of the current voxels per unit.");
        }

        var factor = targetScale / baseScale;
        var result = VoxelKernel.CreateEmpty();

        foreach (var voxel in solid.Voxels)
        {
            var baseX = voxel.X * factor;
            var baseY = voxel.Y * factor;
            var baseZ = voxel.Z * factor;

            for (var dx = 0; dx < factor; dx++)
            {
                for (var dy = 0; dy < factor; dy++)
                {
                    for (var dz = 0; dz < factor; dz++)
                    {
                        VoxelKernel.AddVoxel(result, new Int3(baseX + dx, baseY + dy, baseZ + dz));
                    }
                }
            }
        }

        return result;
    }

    private int DetermineClosingRadius(VoxelSolid union, VoxelSolid solidA, VoxelSolid solidB, Metric metric)
    {
        if (VoxelKernel.Is6Connected(union))
        {
            return 0;
        }

        var boundsA = VoxelKernel.GetBounds(solidA);
        var boundsB = VoxelKernel.GetBounds(solidB);
        var initial = GetChebyshevGap(boundsA, boundsB);
        var high = Math.Max(1, initial);

        var closed = Close(union, high, metric);
        var safety = 0;
        while (!VoxelKernel.Is6Connected(closed) && safety < 16)
        {
            high *= 2;
            closed = Close(union, high, metric);
            safety++;
        }

        if (!VoxelKernel.Is6Connected(closed))
        {
            return high;
        }

        var low = 1;
        var best = high;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = Close(union, mid, metric);
            if (VoxelKernel.Is6Connected(candidate))
            {
                best = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return Math.Max(best, 1);
    }

    private static int GetChebyshevGap((Int3 min, Int3 maxExclusive) boundsA, (Int3 min, Int3 maxExclusive) boundsB)
    {
        var gapX = AxisGap(boundsA.min.X, boundsA.maxExclusive.X, boundsB.min.X, boundsB.maxExclusive.X);
        var gapY = AxisGap(boundsA.min.Y, boundsA.maxExclusive.Y, boundsB.min.Y, boundsB.maxExclusive.Y);
        var gapZ = AxisGap(boundsA.min.Z, boundsA.maxExclusive.Z, boundsB.min.Z, boundsB.maxExclusive.Z);
        return Math.Max(gapX, Math.Max(gapY, gapZ));
    }

    private static int AxisGap(int minA, int maxA, int minB, int maxB)
    {
        if (maxA <= minB)
        {
            return minB - maxA;
        }

        if (maxB <= minA)
        {
            return minA - maxB;
        }

        return 0;
    }

    private static VoxelSolid Close(VoxelSolid solid, int radius, Metric metric)
    {
        if (radius <= 0)
        {
            return CloneSolid(solid);
        }

        var dilated = Dilate(solid, radius, metric);
        return Erode(dilated, radius, metric);
    }

    private static VoxelSolid Dilate(VoxelSolid solid, int radius, Metric metric)
    {
        var result = VoxelKernel.CreateEmpty();
        var offsets = BuildStructuringElement(radius, metric);

        foreach (var voxel in solid.Voxels)
        {
            foreach (var offset in offsets)
            {
                VoxelKernel.AddVoxel(result, new Int3(voxel.X + offset.X, voxel.Y + offset.Y, voxel.Z + offset.Z));
            }
        }

        return result;
    }

    private static VoxelSolid Erode(VoxelSolid solid, int radius, Metric metric)
    {
        var result = VoxelKernel.CreateEmpty();
        var offsets = BuildStructuringElement(radius, metric);
        var voxels = solid.Voxels;

        foreach (var voxel in voxels)
        {
            var keep = true;
            foreach (var offset in offsets)
            {
                var candidate = new Int3(voxel.X + offset.X, voxel.Y + offset.Y, voxel.Z + offset.Z);
                if (!voxels.Contains(candidate))
                {
                    keep = false;
                    break;
                }
            }

            if (keep)
            {
                VoxelKernel.AddVoxel(result, voxel);
            }
        }

        return result;
    }

    private static List<Int3> BuildStructuringElement(int radius, Metric metric)
    {
        var offsets = new List<Int3>();
        if (radius <= 0)
        {
            offsets.Add(new Int3(0, 0, 0));
            return offsets;
        }

        var radiusSquared = radius * radius;
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    var include = metric switch
                    {
                        Metric.LInf => Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Abs(dz)) <= radius,
                        Metric.L1 => Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz) <= radius,
                        Metric.L2 => dx * dx + dy * dy + dz * dz <= radiusSquared,
                        _ => true
                    };

                    if (include)
                    {
                        offsets.Add(new Int3(dx, dy, dz));
                    }
                }
            }
        }

        return offsets;
    }

    private static VoxelSolid BuildBridgePrism(VoxelSolid solidA, VoxelSolid solidB, Axis axis, int thickness, AabbI? mask)
    {
        var projectionA = BuildProjection(solidA, axis);
        var projectionB = BuildProjection(solidB, axis);
        var footprint = new HashSet<(int, int)>(projectionA.Keys);
        footprint.IntersectWith(projectionB.Keys);

        var boundsA = VoxelKernel.GetBounds(solidA);
        var boundsB = VoxelKernel.GetBounds(solidB);

        if (footprint.Count == 0)
        {
            var firstAxis = GetFirstAxis(axis);
            var secondAxis = GetSecondAxis(axis);
            var range1 = OverlapRange(GetAxisRange(boundsA, firstAxis), GetAxisRange(boundsB, firstAxis));
            var range2 = OverlapRange(GetAxisRange(boundsA, secondAxis), GetAxisRange(boundsB, secondAxis));

            for (var i = range1.min; i < range1.max; i++)
            {
                for (var j = range2.min; j < range2.max; j++)
                {
                    footprint.Add((i, j));
                }
            }
        }

        var bridge = VoxelKernel.CreateEmpty();
        var aFirst = AxisCenter(boundsA, axis) <= AxisCenter(boundsB, axis);

        foreach (var key in footprint)
        {
            var rangeA = projectionA.TryGetValue(key, out var rA) ? rA : GetAxisRange(boundsA, axis);
            var rangeB = projectionB.TryGetValue(key, out var rB) ? rB : GetAxisRange(boundsB, axis);

            var start = aFirst ? rangeA.max : rangeB.max;
            var end = aFirst ? rangeB.min : rangeA.min;

            if (start > end)
            {
                var temp = start;
                start = end;
                end = temp;
            }

            start -= thickness - 1;
            end += thickness;

            if (start >= end)
            {
                continue;
            }

            for (var pos = start; pos < end; pos++)
            {
                var voxel = axis switch
                {
                    Axis.X => new Int3(pos, key.Item1, key.Item2),
                    Axis.Y => new Int3(key.Item1, pos, key.Item2),
                    Axis.Z => new Int3(key.Item1, key.Item2, pos),
                    _ => throw new ArgumentOutOfRangeException(nameof(axis))
                };

                if (mask.HasValue && !mask.Value.Contains(voxel))
                {
                    continue;
                }

                VoxelKernel.AddVoxel(bridge, voxel);
            }
        }

        return bridge;
    }

    private static Dictionary<(int, int), (int min, int max)> BuildProjection(VoxelSolid solid, Axis axis)
    {
        var map = new Dictionary<(int, int), (int min, int max)>();

        foreach (var voxel in solid.Voxels)
        {
            int keyA;
            int keyB;
            int axisCoord;

            switch (axis)
            {
                case Axis.X:
                    keyA = voxel.Y;
                    keyB = voxel.Z;
                    axisCoord = voxel.X;
                    break;
                case Axis.Y:
                    keyA = voxel.X;
                    keyB = voxel.Z;
                    axisCoord = voxel.Y;
                    break;
                case Axis.Z:
                    keyA = voxel.X;
                    keyB = voxel.Y;
                    axisCoord = voxel.Z;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis));
            }

            var key = (keyA, keyB);
            if (map.TryGetValue(key, out var range))
            {
                var min = Math.Min(range.min, axisCoord);
                var max = Math.Max(range.max, axisCoord + 1);
                map[key] = (min, max);
            }
            else
            {
                map[key] = (axisCoord, axisCoord + 1);
            }
        }

        return map;
    }

    private static (int min, int max) GetAxisRange((Int3 min, Int3 maxExclusive) bounds, Axis axis)
    {
        return axis switch
        {
            Axis.X => (bounds.min.X, bounds.maxExclusive.X),
            Axis.Y => (bounds.min.Y, bounds.maxExclusive.Y),
            Axis.Z => (bounds.min.Z, bounds.maxExclusive.Z),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };
    }

    private static (int min, int max) OverlapRange((int min, int max) a, (int min, int max) b)
    {
        return (Math.Max(a.min, b.min), Math.Min(a.max, b.max));
    }

    private static Axis GetFirstAxis(Axis axis) => axis switch
    {
        Axis.X => Axis.Y,
        Axis.Y => Axis.X,
        Axis.Z => Axis.X,
        _ => Axis.X
    };

    private static Axis GetSecondAxis(Axis axis) => axis switch
    {
        Axis.X => Axis.Z,
        Axis.Y => Axis.Z,
        Axis.Z => Axis.Y,
        _ => Axis.Y
    };

    private static double AxisCenter((Int3 min, Int3 maxExclusive) bounds, Axis axis)
    {
        var range = GetAxisRange(bounds, axis);
        return (range.min + range.max) * 0.5;
    }

    private static VoxelSolid BuildStrut(VoxelSolid solidA, VoxelSolid solidB, int radius)
    {
        var surfaceA = GetSurfaceVoxels(solidA);
        var surfaceB = GetSurfaceVoxels(solidB);

        if (surfaceA.Count == 0)
        {
            surfaceA.AddRange(solidA.Voxels);
        }

        if (surfaceB.Count == 0)
        {
            surfaceB.AddRange(solidB.Voxels);
        }

        var bestA = surfaceA[0];
        var bestB = surfaceB[0];
        var bestDistance = long.MaxValue;

        foreach (var va in surfaceA)
        {
            foreach (var vb in surfaceB)
            {
                var dx = va.X - vb.X;
                var dy = va.Y - vb.Y;
                var dz = va.Z - vb.Z;
                var dist = (long)dx * dx + (long)dy * dy + (long)dz * dz;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestA = va;
                    bestB = vb;
                    if (dist == 0)
                    {
                        break;
                    }
                }
            }
        }

        var strut = VoxelKernel.CreateEmpty();
        var offsets = BuildStructuringElement(radius, Metric.LInf);
        foreach (var point in RasterizeLine(bestA, bestB))
        {
            foreach (var offset in offsets)
            {
                VoxelKernel.AddVoxel(strut, new Int3(point.X + offset.X, point.Y + offset.Y, point.Z + offset.Z));
            }
        }

        return strut;
    }

    private static List<Int3> GetSurfaceVoxels(VoxelSolid solid)
    {
        var result = new List<Int3>();
        var voxels = solid.Voxels;

        foreach (var voxel in voxels)
        {
            var exposed = false;
            foreach (var neighbor in SixNeighbors(voxel))
            {
                if (!voxels.Contains(neighbor))
                {
                    exposed = true;
                    break;
                }
            }

            if (exposed)
            {
                result.Add(voxel);
            }
        }

        return result;
    }

    private static IEnumerable<Int3> SixNeighbors(Int3 voxel)
    {
        yield return new Int3(voxel.X - 1, voxel.Y, voxel.Z);
        yield return new Int3(voxel.X + 1, voxel.Y, voxel.Z);
        yield return new Int3(voxel.X, voxel.Y - 1, voxel.Z);
        yield return new Int3(voxel.X, voxel.Y + 1, voxel.Z);
        yield return new Int3(voxel.X, voxel.Y, voxel.Z - 1);
        yield return new Int3(voxel.X, voxel.Y, voxel.Z + 1);
    }

    private static IEnumerable<Int3> RasterizeLine(Int3 start, Int3 end)
    {
        var x1 = start.X;
        var y1 = start.Y;
        var z1 = start.Z;
        var x2 = end.X;
        var y2 = end.Y;
        var z2 = end.Z;

        var dx = Math.Abs(x2 - x1);
        var dy = Math.Abs(y2 - y1);
        var dz = Math.Abs(z2 - z1);

        var xs = x2 > x1 ? 1 : -1;
        var ys = y2 > y1 ? 1 : -1;
        var zs = z2 > z1 ? 1 : -1;

        yield return new Int3(x1, y1, z1);

        if (dx >= dy && dx >= dz)
        {
            var p1 = 2 * dy - dx;
            var p2 = 2 * dz - dx;
            while (x1 != x2)
            {
                x1 += xs;
                if (p1 >= 0)
                {
                    y1 += ys;
                    p1 -= 2 * dx;
                }
                if (p2 >= 0)
                {
                    z1 += zs;
                    p2 -= 2 * dx;
                }
                p1 += 2 * dy;
                p2 += 2 * dz;
                yield return new Int3(x1, y1, z1);
            }
        }
        else if (dy >= dx && dy >= dz)
        {
            var p1 = 2 * dx - dy;
            var p2 = 2 * dz - dy;
            while (y1 != y2)
            {
                y1 += ys;
                if (p1 >= 0)
                {
                    x1 += xs;
                    p1 -= 2 * dy;
                }
                if (p2 >= 0)
                {
                    z1 += zs;
                    p2 -= 2 * dy;
                }
                p1 += 2 * dx;
                p2 += 2 * dz;
                yield return new Int3(x1, y1, z1);
            }
        }
        else
        {
            var p1 = 2 * dy - dz;
            var p2 = 2 * dx - dz;
            while (z1 != z2)
            {
                z1 += zs;
                if (p1 >= 0)
                {
                    y1 += ys;
                    p1 -= 2 * dz;
                }
                if (p2 >= 0)
                {
                    x1 += xs;
                    p2 -= 2 * dz;
                }
                p1 += 2 * dy;
                p2 += 2 * dx;
                yield return new Int3(x1, y1, z1);
            }
        }
    }
}

public sealed class BakeOptions
{
    public static BakeOptions Default { get; } = new();

    public bool Incremental { get; init; }
    public RevoxelizationSettings? RevoxelizeOverride { get; init; }
    public int? VoxelsPerUnitOverride { get; init; }
}
