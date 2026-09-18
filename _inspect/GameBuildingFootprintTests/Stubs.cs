using System.Runtime.InteropServices;

namespace SHCDESE.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct GameBuilding
    {
        public uint r_OccupyTileGridSize;
        public uint r_OccupiedTileIdsArrayBegin;
        public fixed byte RemainingOccupiedTileIds[35 * sizeof(uint)];
    }

    internal struct UnmanagedVector2<T> where T : unmanaged
    {
        public T X;
        public T Y;
    }
}

namespace SHCDESE.API
{
    using SHCDESE.Interop;

    internal sealed class GameTileManagerAPI
    {
        public static GameTileManagerAPI Instance { get; } = new GameTileManagerAPI();
        public bool IsValidTileId(int tileId) => tileId >= 0 && tileId < 800 * 800;
        public UnmanagedVector2<ushort> GetTileVectorFromId(int tileId) => new UnmanagedVector2<ushort>
        {
            X = (ushort)(tileId % 800),
            Y = (ushort)(tileId / 800)
        };
    }
}
