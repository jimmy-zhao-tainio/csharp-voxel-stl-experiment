using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SolidBuilder.Voxels;

public enum Axis
{
    X = 0,
    Y = 1,
    Z = 2
}

public struct Int3 : IEquatable<Int3>
{
    public int X;
    public int Y;
    public int Z;

    public Int3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool Equals(Int3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => obj is Int3 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}

public struct FaceKey : IEquatable<FaceKey>
{
    public Axis Axis;
    public int K;
    public int A;
    public int B;

    public FaceKey(Axis axis, int k, int a, int b)
    {
        Axis = axis;
        K = k;
        A = a;
        B = b;
    }

    public bool Equals(FaceKey other) => Axis == other.Axis && K == other.K && A == other.A && B == other.B;

    public override bool Equals(object? obj) => obj is FaceKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine((int)Axis, K, A, B);

    public override string ToString() => $"{Axis}({K}; {A}, {B})";
}

public class VoxelSolid
{
    public HashSet<Int3> Voxels { get; }
    public HashSet<FaceKey> BoundaryFaces { get; }

    public VoxelSolid(HashSet<Int3>? voxels = null, HashSet<FaceKey>? boundaryFaces = null)
    {
        Voxels = voxels ?? new HashSet<Int3>();
        BoundaryFaces = boundaryFaces ?? new HashSet<FaceKey>();
    }
}

public static class VoxelKernel
{
    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly Axis Axis;
        public readonly int K;
        public readonly int A;
        public readonly int B;

        public EdgeKey(Axis axis, int k, int a, int b)
        {
            Axis = axis;
            K = k;
            A = a;
            B = b;
        }

        public bool Equals(EdgeKey other) => Axis == other.Axis && K == other.K && A == other.A && B == other.B;

        public override bool Equals(object? obj) => obj is EdgeKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Axis, K, A, B);
    }

    private readonly struct Triangle
    {
        public Triangle(Int3 a, Int3 b, Int3 c, Axis axis, int normalSign)
        {
            A = a;
            B = b;
            C = c;
            Axis = axis;
            NormalSign = normalSign;
        }

        public Int3 A { get; }
        public Int3 B { get; }
        public Int3 C { get; }
        public Axis Axis { get; }
        public int NormalSign { get; }
    }

    private readonly struct SbvxHeader
    {
        public SbvxHeader(
            byte version,
            byte encoding,
            int minX,
            int minY,
            int minZ,
            uint sizeX,
            uint sizeY,
            uint sizeZ,
            ulong payloadBytes)
        {
            Version = version;
            Encoding = encoding;
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            PayloadBytes = payloadBytes;
        }

        public byte Version { get; }
        public byte Encoding { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MinZ { get; }
        public uint SizeX { get; }
        public uint SizeY { get; }
        public uint SizeZ { get; }
        public ulong PayloadBytes { get; }
    }

    private const byte SbvxVersion = 1;
    private const byte SbvxEncodingDense = 0;
    private const byte SbvxEncodingSparse = 1;

    public static VoxelSolid CreateEmpty() => new();

    public static void AddVoxel(VoxelSolid solid, Int3 cell)
    {
        if (!solid.Voxels.Add(cell))
        {
            return;
        }

        foreach (var (neighbor, face) in EnumerateNeighborFaces(cell))
        {
            if (solid.Voxels.Contains(neighbor))
            {
                solid.BoundaryFaces.Remove(face);
            }
            else
            {
                solid.BoundaryFaces.Add(face);
            }
        }
    }

    public static void RemoveVoxel(VoxelSolid solid, Int3 cell)
    {
        if (!solid.Voxels.Remove(cell))
        {
            return;
        }

        foreach (var (neighbor, face) in EnumerateNeighborFaces(cell))
        {
            if (solid.Voxels.Contains(neighbor))
            {
                solid.BoundaryFaces.Add(face);
            }
            else
            {
                solid.BoundaryFaces.Remove(face);
            }
        }
    }

    public static void AddVoxels(VoxelSolid solid, IEnumerable<Int3> cells)
    {
        foreach (var cell in cells)
        {
            AddVoxel(solid, cell);
        }
    }

    public static void RemoveVoxels(VoxelSolid solid, IEnumerable<Int3> cells)
    {
        foreach (var cell in cells)
        {
            RemoveVoxel(solid, cell);
        }
    }

    public static void AddBox(VoxelSolid solid, Int3 min, Int3 maxExclusive)
    {
        if (min.X >= maxExclusive.X || min.Y >= maxExclusive.Y || min.Z >= maxExclusive.Z)
        {
            return;
        }

        for (var x = min.X; x < maxExclusive.X; x++)
        {
            for (var y = min.Y; y < maxExclusive.Y; y++)
            {
                for (var z = min.Z; z < maxExclusive.Z; z++)
                {
                    AddVoxel(solid, new Int3(x, y, z));
                }
            }
        }
    }

    public static void SubtractBox(VoxelSolid solid, Int3 min, Int3 maxExclusive)
    {
        if (min.X >= maxExclusive.X || min.Y >= maxExclusive.Y || min.Z >= maxExclusive.Z)
        {
            return;
        }

        for (var x = min.X; x < maxExclusive.X; x++)
        {
            for (var y = min.Y; y < maxExclusive.Y; y++)
            {
                for (var z = min.Z; z < maxExclusive.Z; z++)
                {
                    RemoveVoxel(solid, new Int3(x, y, z));
                }
            }
        }
    }

    public static void AddCylinderZ(VoxelSolid solid, int cx, int cy, int zMin, int zMaxExclusive, int radius)
    {
        if (zMin >= zMaxExclusive || radius < 0)
        {
            return;
        }

        var radiusSquared = radius * radius;
        for (var x = cx - radius; x <= cx + radius; x++)
        {
            var dx = x - cx;
            var dx2 = dx * dx;
            for (var y = cy - radius; y <= cy + radius; y++)
            {
                var dy = y - cy;
                if (dx2 + dy * dy > radiusSquared)
                {
                    continue;
                }

                for (var z = zMin; z < zMaxExclusive; z++)
                {
                    AddVoxel(solid, new Int3(x, y, z));
                }
            }
        }
    }

    public static void AddSphere(VoxelSolid solid, Int3 center, int radius)
    {
        if (radius < 0)
        {
            return;
        }

        var radiusSquared = radius * radius;
        for (var x = center.X - radius; x <= center.X + radius; x++)
        {
            var dx = x - center.X;
            var dx2 = dx * dx;
            for (var y = center.Y - radius; y <= center.Y + radius; y++)
            {
                var dy = y - center.Y;
                var dxy2 = dx2 + dy * dy;
                if (dxy2 > radiusSquared)
                {
                    continue;
                }

                for (var z = center.Z - radius; z <= center.Z + radius; z++)
                {
                    var dz = z - center.Z;
                    if (dxy2 + dz * dz <= radiusSquared)
                    {
                        AddVoxel(solid, new Int3(x, y, z));
                    }
                }
            }
        }
    }

    public static VoxelSolid Union(VoxelSolid a, VoxelSolid b)
    {
        var result = CreateEmpty();
        AddVoxels(result, a.Voxels);
        AddVoxels(result, b.Voxels);
        return result;
    }

    public static VoxelSolid Intersect(VoxelSolid a, VoxelSolid b)
    {
        var result = CreateEmpty();
        foreach (var cell in a.Voxels)
        {
            if (b.Voxels.Contains(cell))
            {
                AddVoxel(result, cell);
            }
        }

        return result;
    }

    public static VoxelSolid Subtract(VoxelSolid a, VoxelSolid b)
    {
        var result = CreateEmpty();
        foreach (var cell in a.Voxels)
        {
            if (!b.Voxels.Contains(cell))
            {
                AddVoxel(result, cell);
            }
        }

        return result;
    }

    public static VoxelSolid Translate(VoxelSolid solid, int dx, int dy, int dz)
    {
        var result = CreateEmpty();
        foreach (var cell in solid.Voxels)
        {
            AddVoxel(result, new Int3(cell.X + dx, cell.Y + dy, cell.Z + dz));
        }

        return result;
    }

    public static VoxelSolid Rotate90(VoxelSolid solid, Axis axis, int quarterTurns)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0)
        {
            var copy = CreateEmpty();
            AddVoxels(copy, solid.Voxels);
            return copy;
        }

        var result = CreateEmpty();
        foreach (var cell in solid.Voxels)
        {
            AddVoxel(result, RotateCell(cell, axis, turns));
        }

        return result;
    }

    public static VoxelSolid Mirror(VoxelSolid solid, Axis axis)
    {
        var result = CreateEmpty();
        foreach (var cell in solid.Voxels)
        {
            AddVoxel(result, axis switch
            {
                Axis.X => new Int3(-cell.X - 1, cell.Y, cell.Z),
                Axis.Y => new Int3(cell.X, -cell.Y - 1, cell.Z),
                Axis.Z => new Int3(cell.X, cell.Y, -cell.Z - 1),
                _ => cell
            });
        }

        return result;
    }

    public static (Int3 min, Int3 maxExclusive) GetBounds(VoxelSolid solid)
    {
        if (solid.Voxels.Count == 0)
        {
            return (new Int3(0, 0, 0), new Int3(0, 0, 0));
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var minZ = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var maxZ = int.MinValue;

        foreach (var cell in solid.Voxels)
        {
            if (cell.X < minX) minX = cell.X;
            if (cell.Y < minY) minY = cell.Y;
            if (cell.Z < minZ) minZ = cell.Z;
            if (cell.X + 1 > maxX) maxX = cell.X + 1;
            if (cell.Y + 1 > maxY) maxY = cell.Y + 1;
            if (cell.Z + 1 > maxZ) maxZ = cell.Z + 1;
        }

        return (new Int3(minX, minY, minZ), new Int3(maxX, maxY, maxZ));
    }

    public static long GetVolume(VoxelSolid solid) => solid.Voxels.Count;

    public static long GetSurfaceArea(VoxelSolid solid) => solid.BoundaryFaces.Count;

    public static bool Is6Connected(VoxelSolid solid)
    {
        if (solid.Voxels.Count <= 1)
        {
            return true;
        }

        var visited = new HashSet<Int3>();
        var queue = new Queue<Int3>();

        using (var enumerator = solid.Voxels.GetEnumerator())
        {
            if (!enumerator.MoveNext())
            {
                return true;
            }

            queue.Enqueue(enumerator.Current);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var neighbor in EnumerateNeighborCells(current))
            {
                if (solid.Voxels.Contains(neighbor) && !visited.Contains(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited.Count == solid.Voxels.Count;
    }

    public static bool IsWatertight(VoxelSolid solid)
    {
        if (solid.Voxels.Count == 0)
        {
            return true;
        }

        if (solid.BoundaryFaces.Count == 0)
        {
            return false;
        }

        var edgeCounts = new Dictionary<EdgeKey, int>();
        foreach (var face in solid.BoundaryFaces)
        {
            foreach (var edge in GetFaceEdges(face))
            {
                edgeCounts.TryGetValue(edge, out var count);
                edgeCounts[edge] = count + 1;
            }
        }

        foreach (var count in edgeCounts.Values)
        {
            if (count != 2)
            {
                return false;
            }
        }

        return true;
    }

    public static List<(Int3 a, Int3 b, Int3 c)> ToTriangles(VoxelSolid solid)
    {
        var result = new List<(Int3 a, Int3 b, Int3 c)>();
        foreach (var triangle in EnumerateTriangles(solid))
        {
            result.Add((triangle.A, triangle.B, triangle.C));
        }

        return result;
    }

    public static void WriteBinaryStl(VoxelSolid solid, string name, Stream output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var triangles = new List<Triangle>();
        foreach (var triangle in EnumerateTriangles(solid))
        {
            triangles.Add(triangle);
        }

        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        var header = new byte[80];
        if (!string.IsNullOrEmpty(name))
        {
            var bytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(bytes, header, Math.Min(bytes.Length, header.Length));
        }

        writer.Write(header);
        writer.Write((uint)triangles.Count);

        foreach (var triangle in triangles)
        {
            var normal = GetNormal(triangle.Axis, triangle.NormalSign);
            writer.Write((float)normal.X);
            writer.Write((float)normal.Y);
            writer.Write((float)normal.Z);

            writer.Write((float)triangle.A.X);
            writer.Write((float)triangle.A.Y);
            writer.Write((float)triangle.A.Z);

            writer.Write((float)triangle.B.X);
            writer.Write((float)triangle.B.Y);
            writer.Write((float)triangle.B.Z);

            writer.Write((float)triangle.C.X);
            writer.Write((float)triangle.C.Y);
            writer.Write((float)triangle.C.Z);

            writer.Write((ushort)0);
        }
    }

    public static void WriteSbvxDense(VoxelSolid solid, Stream output)
    {
        if (solid is null)
        {
            throw new ArgumentNullException(nameof(solid));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var (min, max) = GetBounds(solid);
        var sizeX = GetExtent(max.X - min.X);
        var sizeY = GetExtent(max.Y - min.Y);
        var sizeZ = GetExtent(max.Z - min.Z);

        var totalCells = checked((ulong)sizeX * (ulong)sizeY * (ulong)sizeZ);
        var payloadBytes = (totalCells + 7UL) / 8UL;

        if (payloadBytes > int.MaxValue)
        {
            throw new InvalidOperationException("Dense payload exceeds supported size.");
        }

        // Payload packs occupancy bits with X fastest, then Y, then Z progression; each byte stores eight
        // successive cells in least-significant-bit-first order so bit 0 corresponds to index 0, bit 1 to index 1, etc.
        var payload = payloadBytes == 0 ? Array.Empty<byte>() : new byte[(int)payloadBytes];
        var sizeXi = checked((int)sizeX);
        var sizeYi = checked((int)sizeY);
        var sizeZi = checked((int)sizeZ);
        var originX = min.X;
        var originY = min.Y;
        var originZ = min.Z;

        if (totalCells > 0)
        {
            foreach (var cell in solid.Voxels)
            {
                var localX = cell.X - originX;
                var localY = cell.Y - originY;
                var localZ = cell.Z - originZ;
                if ((uint)localX >= sizeX || (uint)localY >= sizeY || (uint)localZ >= sizeZ)
                {
                    throw new InvalidOperationException("Voxel lies outside computed dense bounds.");
                }

                var index = localX + (long)sizeXi * (localY + (long)sizeYi * localZ);
                if (index < 0 || (ulong)index >= totalCells)
                {
                    throw new InvalidOperationException("Dense voxel index out of range.");
                }

                var bitIndex = (int)index;
                payload[bitIndex >> 3] |= (byte)(1 << (bitIndex & 7));
            }
        }

        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        WriteSbvxHeader(writer, SbvxEncodingDense, min, sizeX, sizeY, sizeZ, payloadBytes);
        if (payload.Length > 0)
        {
            writer.Write(payload);
        }
    }

    public static VoxelSolid ReadSbvxDense(Stream input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        using var reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
        var header = ReadSbvxHeader(reader);
        if (header.Encoding != SbvxEncodingDense)
        {
            throw new InvalidDataException("Stream does not contain SBVX dense encoding.");
        }

        return ReadSbvxDensePayload(reader, header);
    }

    public static void WriteSbvxSparse(VoxelSolid solid, Stream output)
    {
        if (solid is null)
        {
            throw new ArgumentNullException(nameof(solid));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var (min, max) = GetBounds(solid);
        var sizeX = GetExtent(max.X - min.X);
        var sizeY = GetExtent(max.Y - min.Y);
        var sizeZ = GetExtent(max.Z - min.Z);

        var voxels = new List<Int3>(solid.Voxels);
        var count = voxels.Count;
        if (count > 1)
        {
            var origin = min;
            voxels.Sort((a, b) =>
            {
                var mortonA = MortonKey(a, origin);
                var mortonB = MortonKey(b, origin);
                var cmp = mortonA.CompareTo(mortonB);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = a.Z.CompareTo(b.Z);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = a.Y.CompareTo(b.Y);
                if (cmp != 0)
                {
                    return cmp;
                }

                return a.X.CompareTo(b.X);
            });
        }

        var payloadBytes = 4UL + 12UL * (ulong)count;
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        WriteSbvxHeader(writer, SbvxEncodingSparse, min, sizeX, sizeY, sizeZ, payloadBytes);
        writer.Write((uint)count);
        foreach (var cell in voxels)
        {
            writer.Write(cell.X);
            writer.Write(cell.Y);
            writer.Write(cell.Z);
        }
    }

    public static VoxelSolid ReadSbvxSparse(Stream input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        using var reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
        var header = ReadSbvxHeader(reader);
        if (header.Encoding != SbvxEncodingSparse)
        {
            throw new InvalidDataException("Stream does not contain SBVX sparse encoding.");
        }

        return ReadSbvxSparsePayload(reader, header);
    }

    public static void AutoWriteSbvx(VoxelSolid solid, Stream output)
    {
        if (solid is null)
        {
            throw new ArgumentNullException(nameof(solid));
        }

        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        var (min, max) = GetBounds(solid);
        var sizeX = GetExtent(max.X - min.X);
        var sizeY = GetExtent(max.Y - min.Y);
        var sizeZ = GetExtent(max.Z - min.Z);
        var totalCells = checked((ulong)sizeX * (ulong)sizeY * (ulong)sizeZ);
        var voxelCount = (ulong)solid.Voxels.Count;

        if (totalCells == 0 || voxelCount == 0)
        {
            WriteSbvxSparse(solid, output);
            return;
        }

        if (voxelCount * 4UL >= totalCells)
        {
            WriteSbvxDense(solid, output);
        }
        else
        {
            WriteSbvxSparse(solid, output);
        }
    }

    public static VoxelSolid ReadSbvx(Stream input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        using var reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);
        var header = ReadSbvxHeader(reader);
        return header.Encoding switch
        {
            SbvxEncodingDense => ReadSbvxDensePayload(reader, header),
            SbvxEncodingSparse => ReadSbvxSparsePayload(reader, header),
            _ => throw new InvalidDataException($"Unsupported SBVX encoding {header.Encoding}.")
        };
    }

    private static void WriteSbvxHeader(BinaryWriter writer, byte encoding, Int3 min, uint sizeX, uint sizeY, uint sizeZ, ulong payloadBytes)
    {
        writer.Write("SBVX\0"u8);
        writer.Write(SbvxVersion);
        writer.Write(encoding);
        writer.Write(min.X);
        writer.Write(min.Y);
        writer.Write(min.Z);
        writer.Write(sizeX);
        writer.Write(sizeY);
        writer.Write(sizeZ);
        writer.Write(payloadBytes);
    }

    private static SbvxHeader ReadSbvxHeader(BinaryReader reader)
    {
        Span<byte> magic = stackalloc byte[5];
        var read = reader.Read(magic);
        if (read != magic.Length || !magic.SequenceEqual("SBVX\0"u8))
        {
            throw new InvalidDataException("Stream does not contain SBVX data.");
        }

        var version = reader.ReadByte();
        if (version != SbvxVersion)
        {
            throw new InvalidDataException($"Unsupported SBVX version {version}.");
        }

        var encoding = reader.ReadByte();
        var minX = reader.ReadInt32();
        var minY = reader.ReadInt32();
        var minZ = reader.ReadInt32();
        var sizeX = reader.ReadUInt32();
        var sizeY = reader.ReadUInt32();
        var sizeZ = reader.ReadUInt32();
        var payloadBytes = reader.ReadUInt64();
        return new SbvxHeader(version, encoding, minX, minY, minZ, sizeX, sizeY, sizeZ, payloadBytes);
    }

    private static VoxelSolid ReadSbvxDensePayload(BinaryReader reader, SbvxHeader header)
    {
        var solid = CreateEmpty();
        var sizeX = header.SizeX;
        var sizeY = header.SizeY;
        var sizeZ = header.SizeZ;
        var totalCells = checked((ulong)sizeX * (ulong)sizeY * (ulong)sizeZ);
        var expectedBytes = (totalCells + 7UL) / 8UL;
        if (header.PayloadBytes != expectedBytes)
        {
            throw new InvalidDataException("Dense payload byte count mismatch.");
        }

        if (expectedBytes > int.MaxValue)
        {
            throw new InvalidDataException("Dense payload exceeds supported size.");
        }

        var payload = reader.ReadBytes((int)expectedBytes);
        if ((ulong)payload.Length != expectedBytes)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading dense payload.");
        }

        if (totalCells == 0)
        {
            return solid;
        }

        var min = new Int3(header.MinX, header.MinY, header.MinZ);
        var sizeXi = checked((int)sizeX);
        var sizeYi = checked((int)sizeY);
        var sizeZi = checked((int)sizeZ);
        var bitIndex = 0;

        // Iterate in Z-major order but compute linear index with X fastest, matching the packed bitfield definition.
        for (var z = 0; z < sizeZi; z++)
        {
            for (var y = 0; y < sizeYi; y++)
            {
                for (var x = 0; x < sizeXi; x++, bitIndex++)
                {
                    var byteIndex = bitIndex >> 3;
                    var bitMask = 1 << (bitIndex & 7);
                    if ((payload[byteIndex] & bitMask) != 0)
                    {
                        AddVoxel(solid, new Int3(min.X + x, min.Y + y, min.Z + z));
                    }
                }
            }
        }

        return solid;
    }

    private static VoxelSolid ReadSbvxSparsePayload(BinaryReader reader, SbvxHeader header)
    {
        var solid = CreateEmpty();
        var count = reader.ReadUInt32();
        var expectedBytes = 4UL + 12UL * count;
        if (header.PayloadBytes != expectedBytes)
        {
            throw new InvalidDataException("Sparse payload byte count mismatch.");
        }

        var min = new Int3(header.MinX, header.MinY, header.MinZ);
        var maxExclusive = new Int3(
            checked(header.MinX + (int)header.SizeX),
            checked(header.MinY + (int)header.SizeY),
            checked(header.MinZ + (int)header.SizeZ));

        for (var i = 0; i < count; i++)
        {
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var z = reader.ReadInt32();

            if (x < min.X || x >= maxExclusive.X ||
                y < min.Y || y >= maxExclusive.Y ||
                z < min.Z || z >= maxExclusive.Z)
            {
                throw new InvalidDataException("Sparse voxel lies outside declared bounds.");
            }

            AddVoxel(solid, new Int3(x, y, z));
        }

        return solid;
    }

    private static uint GetExtent(int delta) => delta <= 0 ? 0u : (uint)delta;

    private static ulong MortonKey(Int3 cell, Int3 origin)
    {
        var dx = cell.X - origin.X;
        var dy = cell.Y - origin.Y;
        var dz = cell.Z - origin.Z;
        if (dx < 0 || dy < 0 || dz < 0)
        {
            throw new InvalidOperationException("Voxel lies outside computed sparse bounds.");
        }

        return MortonEncode3D((uint)dx, (uint)dy, (uint)dz);
    }

    private static ulong MortonEncode3D(uint x, uint y, uint z)
    {
        ulong value = 0;
        for (var bit = 0; bit < 21; bit++)
        {
            var shift = bit * 3;
            value |= ((ulong)((x >> bit) & 1u)) << shift;
            value |= ((ulong)((y >> bit) & 1u)) << (shift + 1);
            value |= ((ulong)((z >> bit) & 1u)) << (shift + 2);
        }

        return value;
    }

    private static IEnumerable<(Int3 neighbor, FaceKey face)> EnumerateNeighborFaces(Int3 cell)
    {
        yield return (new Int3(cell.X - 1, cell.Y, cell.Z), new FaceKey(Axis.X, cell.X, cell.Y, cell.Z));
        yield return (new Int3(cell.X + 1, cell.Y, cell.Z), new FaceKey(Axis.X, cell.X + 1, cell.Y, cell.Z));
        yield return (new Int3(cell.X, cell.Y - 1, cell.Z), new FaceKey(Axis.Y, cell.Y, cell.X, cell.Z));
        yield return (new Int3(cell.X, cell.Y + 1, cell.Z), new FaceKey(Axis.Y, cell.Y + 1, cell.X, cell.Z));
        yield return (new Int3(cell.X, cell.Y, cell.Z - 1), new FaceKey(Axis.Z, cell.Z, cell.X, cell.Y));
        yield return (new Int3(cell.X, cell.Y, cell.Z + 1), new FaceKey(Axis.Z, cell.Z + 1, cell.X, cell.Y));
    }

    private static IEnumerable<Int3> EnumerateNeighborCells(Int3 cell)
    {
        yield return new Int3(cell.X - 1, cell.Y, cell.Z);
        yield return new Int3(cell.X + 1, cell.Y, cell.Z);
        yield return new Int3(cell.X, cell.Y - 1, cell.Z);
        yield return new Int3(cell.X, cell.Y + 1, cell.Z);
        yield return new Int3(cell.X, cell.Y, cell.Z - 1);
        yield return new Int3(cell.X, cell.Y, cell.Z + 1);
    }

    private static Int3 RotateCell(Int3 cell, Axis axis, int turns)
    {
        Int3 current = cell;
        for (var i = 0; i < turns; i++)
        {
            current = axis switch
            {
                Axis.X => new Int3(current.X, -current.Z, current.Y),
                Axis.Y => new Int3(current.Z, current.Y, -current.X),
                Axis.Z => new Int3(-current.Y, current.X, current.Z),
                _ => current
            };
        }

        return current;
    }

    private static IEnumerable<EdgeKey> GetFaceEdges(FaceKey face)
    {
        switch (face.Axis)
        {
            case Axis.X:
                yield return new EdgeKey(Axis.Y, face.A, face.K, face.B);
                yield return new EdgeKey(Axis.Y, face.A, face.K, face.B + 1);
                yield return new EdgeKey(Axis.Z, face.B, face.K, face.A);
                yield return new EdgeKey(Axis.Z, face.B, face.K, face.A + 1);
                break;
            case Axis.Y:
                yield return new EdgeKey(Axis.X, face.A, face.K, face.B);
                yield return new EdgeKey(Axis.X, face.A, face.K, face.B + 1);
                yield return new EdgeKey(Axis.Z, face.B, face.A, face.K);
                yield return new EdgeKey(Axis.Z, face.B, face.A + 1, face.K);
                break;
            case Axis.Z:
                yield return new EdgeKey(Axis.X, face.A, face.B, face.K);
                yield return new EdgeKey(Axis.X, face.A, face.B + 1, face.K);
                yield return new EdgeKey(Axis.Y, face.B, face.A, face.K);
                yield return new EdgeKey(Axis.Y, face.B, face.A + 1, face.K);
                break;
            default:
                yield break;
        }
    }

    private static IEnumerable<Triangle> EnumerateTriangles(VoxelSolid solid)
    {
        foreach (var face in solid.BoundaryFaces)
        {
            var sign = GetFaceNormalSign(solid, face);
            var vertices = GetFaceVertices(face);
            if (sign > 0)
            {
                yield return new Triangle(vertices.v0, vertices.v1, vertices.v2, face.Axis, sign);
                yield return new Triangle(vertices.v0, vertices.v2, vertices.v3, face.Axis, sign);
            }
            else if (sign < 0)
            {
                // Reversed winding for negative normals.
                yield return new Triangle(vertices.v0, vertices.v2, vertices.v1, face.Axis, sign);
                yield return new Triangle(vertices.v0, vertices.v3, vertices.v2, face.Axis, sign);
            }
        }
    }

    // +X face (outward normal +X): (K,A,B) -> (K,A,B+1) -> (K,A+1,B+1); second triangle uses (K,A+1,B).
    // +Y face (outward normal +Y): (A,K,B) -> (A,K,B+1) -> (A+1,K,B+1); second triangle uses (A+1,K,B).
    // +Z face (outward normal +Z): (A,B,K) -> (A+1,B,K) -> (A+1,B+1,K); second triangle uses (A,B+1,K).
    // Negative normals reuse the same corners with reversed winding.
    private static (Int3 v0, Int3 v1, Int3 v2, Int3 v3) GetFaceVertices(FaceKey face)
    {
        return face.Axis switch
        {
            Axis.X => (
                new Int3(face.K, face.A, face.B),
                new Int3(face.K, face.A, face.B + 1),
                new Int3(face.K, face.A + 1, face.B + 1),
                new Int3(face.K, face.A + 1, face.B)
            ),
            Axis.Y => (
                new Int3(face.A, face.K, face.B),
                new Int3(face.A, face.K, face.B + 1),
                new Int3(face.A + 1, face.K, face.B + 1),
                new Int3(face.A + 1, face.K, face.B)
            ),
            Axis.Z => (
                new Int3(face.A, face.B, face.K),
                new Int3(face.A + 1, face.B, face.K),
                new Int3(face.A + 1, face.B + 1, face.K),
                new Int3(face.A, face.B + 1, face.K)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(face))
        };
    }

    private static int GetFaceNormalSign(VoxelSolid solid, FaceKey face)
    {
        switch (face.Axis)
        {
            case Axis.X:
            {
                var positive = new Int3(face.K, face.A, face.B);
                if (solid.Voxels.Contains(positive))
                {
                    return -1;
                }

                var negative = new Int3(face.K - 1, face.A, face.B);
                if (solid.Voxels.Contains(negative))
                {
                    return 1;
                }

                break;
            }
            case Axis.Y:
            {
                var positive = new Int3(face.A, face.K, face.B);
                if (solid.Voxels.Contains(positive))
                {
                    return -1;
                }

                var negative = new Int3(face.A, face.K - 1, face.B);
                if (solid.Voxels.Contains(negative))
                {
                    return 1;
                }

                break;
            }
            case Axis.Z:
            {
                var positive = new Int3(face.A, face.B, face.K);
                if (solid.Voxels.Contains(positive))
                {
                    return -1;
                }

                var negative = new Int3(face.A, face.B, face.K - 1);
                if (solid.Voxels.Contains(negative))
                {
                    return 1;
                }

                break;
            }
        }

        throw new InvalidOperationException("Boundary face is not adjacent to a filled voxel.");
    }

    private static Int3 GetNormal(Axis axis, int sign)
    {
        return axis switch
        {
            Axis.X => sign > 0 ? new Int3(1, 0, 0) : new Int3(-1, 0, 0),
            Axis.Y => sign > 0 ? new Int3(0, 1, 0) : new Int3(0, -1, 0),
            Axis.Z => sign > 0 ? new Int3(0, 0, 1) : new Int3(0, 0, -1),
            _ => new Int3(0, 0, 0)
        };
    }
}
