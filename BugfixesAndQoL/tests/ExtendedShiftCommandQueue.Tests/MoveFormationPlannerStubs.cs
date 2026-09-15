using System;

namespace SHCDESE.Interop
{
    internal sealed class GameTileManagerView
    {
        internal const int NativePackedTileCapacity = 320800;
        internal readonly byte[] Edges = new byte[NativePackedTileCapacity];
        internal readonly ushort[] Components = new ushort[NativePackedTileCapacity];
        internal readonly int[] Logic = new int[NativePackedTileCapacity];
        public Span<byte> PathEdgeMaskGrid => Edges;
        public Span<ushort> PathConnectionGrid => Components;
        public Span<int> LogicGrid => Logic;
    }
}

namespace SHCDESE.API
{
    using SHCDESE.Interop;

    internal sealed class GameTileManagerAPI
    {
        internal static readonly GameTileManagerAPI Instance =
            new GameTileManagerAPI();
        internal GameTileManagerView TileManager { get; set; }
        internal int GetTileId(int x, int y) => y * 800 + x;
    }
}
