using SolidBuilder.Voxels;

namespace VoxelCad.Builder;

using Int3 = SolidBuilder.Voxels.Int3;

public sealed class VoxelBuilder
{
    private readonly VoxelSolid _solid;
    private readonly Stack<List<TransformOp>> _transformStack;
    private List<TransformOp> _currentTransforms;

    public VoxelBuilder()
    {
        _solid = VoxelKernel.CreateEmpty();
        _transformStack = new Stack<List<TransformOp>>();
        _currentTransforms = new List<TransformOp>();
    }

    private VoxelBuilder(List<TransformOp> currentTransforms)
    {
        _solid = VoxelKernel.CreateEmpty();
        _transformStack = new Stack<List<TransformOp>>();
        _currentTransforms = CloneTransforms(currentTransforms);
    }

    public VoxelSolid Build()
    {
        return new VoxelSolid(
            new HashSet<Int3>(_solid.Voxels),
            new HashSet<FaceKey>(_solid.BoundaryFaces));
    }

    public VoxelBuilder Box(Int3 min, Int3 maxExclusive)
    {
        var temp = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(temp, min, maxExclusive);
        ApplyTransformed(temp, subtract: false);
        return this;
    }

    public VoxelBuilder CutBox(Int3 min, Int3 maxExclusive)
    {
        var temp = VoxelKernel.CreateEmpty();
        VoxelKernel.AddBox(temp, min, maxExclusive);
        ApplyTransformed(temp, subtract: true);
        return this;
    }

    public VoxelBuilder CylinderZ(int cx, int cy, int zMin, int zMaxExclusive, int radius)
    {
        var temp = VoxelKernel.CreateEmpty();
        VoxelKernel.AddCylinderZ(temp, cx, cy, zMin, zMaxExclusive, radius);
        ApplyTransformed(temp, subtract: false);
        return this;
    }

    public VoxelBuilder CutCylinderZ(int cx, int cy, int zMin, int zMaxExclusive, int radius)
    {
        var temp = VoxelKernel.CreateEmpty();
        VoxelKernel.AddCylinderZ(temp, cx, cy, zMin, zMaxExclusive, radius);
        ApplyTransformed(temp, subtract: true);
        return this;
    }

    public VoxelBuilder Sphere(Int3 center, int radius)
    {
        var temp = VoxelKernel.CreateEmpty();
        VoxelKernel.AddSphere(temp, center, radius);
        ApplyTransformed(temp, subtract: false);
        return this;
    }

    public VoxelBuilder CutSphere(Int3 center, int radius)
    {
        var temp = VoxelKernel.CreateEmpty();
        VoxelKernel.AddSphere(temp, center, radius);
        ApplyTransformed(temp, subtract: true);
        return this;
    }

    public VoxelBuilder Translate(int dx, int dy, int dz)
    {
        if (dx == 0 && dy == 0 && dz == 0)
        {
            return this;
        }

        _currentTransforms.Add(TransformOp.Translate(dx, dy, dz));
        return this;
    }

    public VoxelBuilder Rotate90(Axis axis, int quarterTurns)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0)
        {
            return this;
        }

        _currentTransforms.Add(TransformOp.Rotate(axis, turns));
        return this;
    }

    public VoxelBuilder Mirror(Axis axis)
    {
        _currentTransforms.Add(TransformOp.Mirror(axis));
        return this;
    }

    public VoxelBuilder ResetTransform()
    {
        _currentTransforms = new List<TransformOp>();
        return this;
    }

    public VoxelBuilder Place(Int3 offset, Action<VoxelBuilder> scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));

        var next = CloneTransforms(_currentTransforms);
        if (offset.X != 0 || offset.Y != 0 || offset.Z != 0)
        {
            next.Add(TransformOp.Translate(offset.X, offset.Y, offset.Z));
        }

        WithTransform(next, scope);
        return this;
    }

    public VoxelBuilder ArrayX(int count, int step, Action<VoxelBuilder> scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));
        for (var i = 0; i < count; i++)
        {
            Place(new Int3(step * i, 0, 0), scope);
        }

        return this;
    }

    public VoxelBuilder ArrayY(int count, int step, Action<VoxelBuilder> scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));
        for (var i = 0; i < count; i++)
        {
            Place(new Int3(0, step * i, 0), scope);
        }

        return this;
    }

    public VoxelBuilder Grid(int countX, int stepX, int countY, int stepY, Action<VoxelBuilder> scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));

        for (var ix = 0; ix < countX; ix++)
        {
            for (var iy = 0; iy < countY; iy++)
            {
                Place(new Int3(stepX * ix, stepY * iy, 0), scope);
            }
        }

        return this;
    }

    public VoxelBuilder Union(Action<VoxelBuilder> scope)
    {
        var other = RunChild(scope);
        var result = VoxelKernel.Union(_solid, other);
        CopyInto(_solid, result);
        return this;
    }

    public VoxelBuilder Subtract(Action<VoxelBuilder> scope)
    {
        var other = RunChild(scope);
        var result = VoxelKernel.Subtract(_solid, other);
        CopyInto(_solid, result);
        return this;
    }

    public VoxelBuilder Intersect(Action<VoxelBuilder> scope)
    {
        var other = RunChild(scope);
        var result = VoxelKernel.Intersect(_solid, other);
        CopyInto(_solid, result);
        return this;
    }

    private void ApplyTransformed(VoxelSolid temp, bool subtract)
    {
        var transformed = ApplyTransform(temp, _currentTransforms);
        if (subtract)
        {
            VoxelKernel.RemoveVoxels(_solid, transformed.Voxels);
        }
        else
        {
            VoxelKernel.AddVoxels(_solid, transformed.Voxels);
        }
    }

    private VoxelSolid RunChild(Action<VoxelBuilder> scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));

        var child = new VoxelBuilder(_currentTransforms);
        scope(child);
        return child.Build();
    }

    private void WithTransform(List<TransformOp> next, Action<VoxelBuilder> scope)
    {
        _transformStack.Push(_currentTransforms);
        _currentTransforms = next;
        try
        {
            scope(this);
        }
        finally
        {
            _currentTransforms = _transformStack.Pop();
        }
    }

    private static List<TransformOp> CloneTransforms(List<TransformOp> source) => new(source);

    private static VoxelSolid ApplyTransform(VoxelSolid solid, List<TransformOp> ops)
    {
        var result = VoxelKernel.CreateEmpty();
        VoxelKernel.AddVoxels(result, solid.Voxels);
        foreach (var op in ops)
        {
            result = op.Apply(result);
        }

        return result;
    }

    private static void CopyInto(VoxelSolid target, VoxelSolid source)
    {
        target.Voxels.Clear();
        target.BoundaryFaces.Clear();
        VoxelKernel.AddVoxels(target, source.Voxels);
    }

    private readonly struct TransformOp
    {
        private TransformOp(TransformType type, Axis axis, int value, Int3 delta)
        {
            Type = type;
            Axis = axis;
            Value = value;
            Delta = delta;
        }

        private enum TransformType
        {
            Translate,
            Rotate,
            Mirror
        }

        private TransformType Type { get; }
        private Axis Axis { get; }
        private int Value { get; }
        private Int3 Delta { get; }

        public static TransformOp Translate(int dx, int dy, int dz) =>
            new(TransformType.Translate, Axis.X, 0, new Int3(dx, dy, dz));

        public static TransformOp Rotate(Axis axis, int quarterTurns) =>
            new(TransformType.Rotate, axis, quarterTurns, default);

        public static TransformOp Mirror(Axis axis) =>
            new(TransformType.Mirror, axis, 0, default);

        public VoxelSolid Apply(VoxelSolid solid)
        {
            return Type switch
            {
                TransformType.Translate => VoxelKernel.Translate(solid, Delta.X, Delta.Y, Delta.Z),
                TransformType.Rotate => VoxelKernel.Rotate90(solid, Axis, Value),
                TransformType.Mirror => VoxelKernel.Mirror(solid, Axis),
                _ => solid
            };
        }
    }
}
