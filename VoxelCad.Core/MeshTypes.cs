using System.Collections.Generic;

namespace VoxelCad.Core;

public struct VertexD
{
    public double X;
    public double Y;
    public double Z;

    public VertexD(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}

public struct TriIdx
{
    public int A;
    public int B;
    public int C;

    public TriIdx(int a, int b, int c)
    {
        A = a;
        B = b;
        C = c;
    }
}

public sealed class MeshD
{
    public List<VertexD> V = new();
    public List<TriIdx> F = new();
}
