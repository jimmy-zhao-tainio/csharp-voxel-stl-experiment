# csharp-voxel-stl-experiment

## SolidBuilder Voxels

This workspace contains:

- `SolidBuilder.Voxels` — voxel kernel class library with watertight boundary tracking, STL export, and SBVX dense/sparse serialization.
- `SolidBuilder.Voxels.Tests` — xUnit tests covering voxel invariants and SBVX round-trips.
- `SolidBuilder.TestBuild` — console app that builds the keyboard column plate scenario and writes SBVX/STL assets to its `bin/<configuration>/net8.0` directory.
- `SolidBuilder.TestLoad` — console app that reloads the SBVX asset, verifies invariants, and re-exports STL.

### Prerequisites

- .NET 8 SDK on PATH.

### Running tests

```bash
dotnet test SolidBuilder.sln
```

### Building geometry and assets

```bash
dotnet run --project SolidBuilder.TestBuild
```

Outputs:

- `plate_auto.sbvx`
- `plate_dense.sbvx`
- `plate_sparse.sbvx`
- `plate.stl`

All files are emitted in `SolidBuilder.TestBuild/bin/<config>/net8.0/`.

### Reloading and verifying

```bash
dotnet run --project SolidBuilder.TestLoad
```

The loader searches for the latest `plate_auto.sbvx` produced by `SolidBuilder.TestBuild`, checks watertightness and volume, and writes `plate_roundtrip.stl` beside the executable.
