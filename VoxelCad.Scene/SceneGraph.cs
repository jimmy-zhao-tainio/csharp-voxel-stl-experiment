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
    private readonly List<Instance> _instances = new();

    public Scene(ProjectSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public IReadOnlyList<Instance> Instances => _instances;
    public ProjectSettings Settings { get; }

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

    public VoxelSolid Bake(BakeOptions? options = null)
    {
        var opts = options ?? BakeOptions.Default;
        var revoxelization = opts.RevoxelizeOverride ?? Settings.Revoxelization;
        var voxelsPerUnit = opts.VoxelsPerUnitOverride ?? Settings.VoxelsPerUnit;
        var baseScale = Settings.VoxelsPerUnit;

        VoxelSolid? result = null;

        foreach (var instance in _instances)
        {
            var solid = CloneSolid(instance.Part.Model);

            if (voxelsPerUnit != baseScale)
            {
                solid = ResampleSolid(solid, baseScale, voxelsPerUnit);
            }

            if (instance.Exact is not null)
            {
                solid = ApplyExact(solid, instance.Exact);
            }

            if (instance.Any is FrameAny any)
            {
                var rotateOptions = revoxelization.CreateOptions(any.Axis, any.Degrees, any.Pivot);
                if (any.SamplesPerAxis > 0)
                {
                    rotateOptions.SamplesPerAxis = any.SamplesPerAxis;
                }

                if (any.Epsilon > 0)
                {
                    rotateOptions.Epsilon = any.Epsilon;
                }

                solid = VoxelKernel.RotateRevoxelized(solid, rotateOptions);
            }

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
}

public sealed class BakeOptions
{
    public static BakeOptions Default { get; } = new();

    public bool Incremental { get; init; }
    public RevoxelizationSettings? RevoxelizeOverride { get; init; }
    public int? VoxelsPerUnitOverride { get; init; }
}
