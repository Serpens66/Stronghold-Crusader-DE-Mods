namespace EnemyGatePathfindingTest
{
    internal enum SparseFootprintStatus
    {
        Valid,
        InvalidGridSize,
        InvalidNonZeroTile,
        Empty
    }

    // Allocation-free common contract for the signature scan and snapshot builder.
    // Vanilla building slots contain at most a 6x6 inline tile array; zero denotes
    // an unused cell and is not an invalid tile ID.
    internal struct SparseFootprintAccumulator
    {
        internal const int MaximumGridSize = 6;
        internal const int MaximumCellCount = MaximumGridSize * MaximumGridSize;
        private const ulong OffsetBasis = 1469598103934665603UL;
        private const ulong Prime = 1099511628211UL;

        private SparseFootprintStatus status;
        private ulong fingerprint;
        private int validTileCount;
        private int emptyCellCount;
        private int invalidCellIndex;
        private uint invalidTileId;
        private int minX;
        private int minY;
        private int maxX;
        private int maxY;

        internal SparseFootprintAccumulator(uint gridSize)
        {
            GridSize = gridSize;
            CellCount = IsSafeGridSize(gridSize) ? checked((int)(gridSize * gridSize)) : 0;
            status = CellCount == 0
                ? SparseFootprintStatus.InvalidGridSize
                : SparseFootprintStatus.Valid;
            fingerprint = Mix(OffsetBasis, gridSize);
            validTileCount = 0;
            emptyCellCount = 0;
            invalidCellIndex = -1;
            invalidTileId = 0;
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;
        }

        internal uint GridSize { get; }
        internal int CellCount { get; }
        internal SparseFootprintStatus Status =>
            status == SparseFootprintStatus.Valid && validTileCount == 0
                ? SparseFootprintStatus.Empty
                : status;
        internal ulong Fingerprint => fingerprint;
        internal int ValidTileCount => validTileCount;
        internal int EmptyCellCount => emptyCellCount;
        internal int InvalidCellIndex => invalidCellIndex;
        internal uint InvalidTileId => invalidTileId;
        internal int MinX => minX;
        internal int MinY => minY;
        internal int MaxX => maxX;
        internal int MaxY => maxY;
        internal bool IsValid => Status == SparseFootprintStatus.Valid;

        internal bool AddCell(int cellIndex, uint rawTileId, bool isValidTile, int x, int y)
        {
            if (status != SparseFootprintStatus.Valid || cellIndex < 0 || cellIndex >= CellCount)
                return false;
            unchecked
            {
                fingerprint = Mix(fingerprint, (uint)cellIndex);
                fingerprint = Mix(fingerprint, rawTileId);
            }
            if (rawTileId == 0)
            {
                emptyCellCount++;
                return true;
            }
            if (!isValidTile)
            {
                status = SparseFootprintStatus.InvalidNonZeroTile;
                invalidCellIndex = cellIndex;
                invalidTileId = rawTileId;
                return false;
            }
            validTileCount++;
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
            return true;
        }

        internal static bool IsSafeGridSize(uint gridSize) =>
            gridSize >= 1 && gridSize <= MaximumGridSize;

        private static ulong Mix(ulong hash, uint value)
        {
            unchecked { return (hash ^ value) * Prime; }
        }
    }
}
