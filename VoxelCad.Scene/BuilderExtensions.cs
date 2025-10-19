using System;
using SolidBuilder.Voxels;
using VoxelCad.Builder;

namespace VoxelCad.Scene;

public static class BuilderSceneExtensions
{
    private const double DefaultEpsilon = 1e-9;
    private const int DefaultSamplesPerAxis = 3;

    public static VoxelBuilder UsingLocal(this VoxelBuilder builder, Instance instance, Action<VoxelBuilder> scope) =>
        UsingLocal(builder, instance, Role.Solid, scope);

    public static VoxelBuilder UsingLocal(this VoxelBuilder builder, Instance instance, Role role, Action<VoxelBuilder> scope)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        if (scope is null) throw new ArgumentNullException(nameof(scope));

        switch (role)
        {
            case Role.Solid:
                builder.Union(child => ApplyFrame(child, instance, scope));
                break;
            case Role.Hole:
                builder.Subtract(child => ApplyFrame(child, instance, scope));
                break;
            case Role.Intersect:
                builder.Intersect(child => ApplyFrame(child, instance, scope));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(role));
        }

        return builder;
    }

    private static void ApplyFrame(VoxelBuilder builder, Instance instance, Action<VoxelBuilder> scope)
    {
        Action<VoxelBuilder> runner = scope;

        if (instance.Any is FrameAny any)
        {
            var previous = runner;
            runner = vb =>
            {
                var options = new RotateOptions
                {
                    Axis = any.Axis,
                    Degrees = any.Degrees,
                    Pivot = any.Pivot,
                    ConservativeObb = true,
                    SamplesPerAxis = any.SamplesPerAxis > 0 ? any.SamplesPerAxis : DefaultSamplesPerAxis,
                    Epsilon = any.Epsilon > 0 ? any.Epsilon : DefaultEpsilon
                };

                vb.RotateAnyWith(any.Axis, any.Degrees, options, previous);
            };
        }

        if (instance.Exact is not null)
        {
            builder.WithLinearTransform(CloneMatrix(instance.Exact.Matrix), instance.Exact.Translation, runner);
        }
        else
        {
            runner(builder);
        }
    }

    private static int[,] CloneMatrix(int[,] matrix)
    {
        var copy = new int[3, 3];
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                copy[i, j] = matrix[i, j];
            }
        }

        return copy;
    }
}
