namespace SolidBuilder.Api
{
  internal static class Config
  {
    public static double VoxelSize = 0.8;
    public static Mesher MesherKind = Mesher.VoxelFaces;

    public static IMesher CreateMesher() =>
      MesherKind switch
      {
        Mesher.SurfaceNets => new SurfaceNetsMesher(),
        _                  => new VoxelFacesMesher(),
      };
  }
}
