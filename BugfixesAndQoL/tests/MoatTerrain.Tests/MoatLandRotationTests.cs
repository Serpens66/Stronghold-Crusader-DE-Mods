using System;
using System.Linq;
using System.Runtime.InteropServices;
using BugfixesAndQoL.MoatTerrain;
using BugfixesAndQoL.Multiplayer;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe partial class Program
{
    private const int RotationTestRow = 368;
    private const int RotatedNeighbourIndex = 0x204E66C;
    private static readonly int[] RotationTestDeltas = { -9, -8, -4, -2, 1, 3, 5, 7 };
    private static byte* RotationHeight => (byte*)manager + MoatLandRotationNative.Height;
    private static byte* RotationDirty => (byte*)manager + MoatLandRotationNative.Changed;
    private const uint Land = (uint)TilePropertyFlag.IsLand, Moat = (uint)TilePropertyFlag.IsMoat;

    private static void RunMoatLandRotationRegression()
    {
        *(int*)state = 0;
        ulong entry = C + 0x1000, continuation = entry + MoatLandRotationNative.Length;
        Marshal.Copy(MoatLandRotationNative.Entry, 0, new IntPtr(unchecked((long)entry)), MoatLandRotationNative.Length);
        var finish = new Assembler(64);
        finish.mov(r8, O);
        Snapshot(finish, 128);
        SnapshotSimd(finish, 512);
        Return(finish);
        Write(finish, continuation);

        var baseline = PrepareRotationInput();
        const uint protectedFlags = Moat | (uint)TilePropertyFlag.HasIron;
        PrepareRotationTile(72, 0x40, 1, protectedFlags | Land);
        uint[] originalNeighbours = RotationTestDeltas.Select(delta => Logic[Tile + delta]).ToArray();
        var unchanged = baseline();
        using (var hook = new X64InlineHook(entry, MoatLandRotationNative.Length, name: "MoatLandRotationFixture"))
        {
            Check(hook.DisplacedByteCount == MoatLandRotationNative.Length, "rotation RedBird displacement");
            hook.Generate((assembler, original, _) => MoatLandRotationNative.Emit(assembler, S, original));
            hook.Enable();
            IntPtr entryPointer = new IntPtr(unchecked((long)entry));
            Check(Marshal.ReadByte(entryPointer) == 0xFF && Marshal.ReadByte(entryPointer, 1) == 0x25 &&
                Marshal.ReadInt32(entryPointer, 2) == 0 && Marshal.ReadInt64(entryPointer, 6) == hook.StubAddress.ToInt64() &&
                Marshal.ReadByte(entryPointer, 14) == 0x90, "rotation RedBird patch form");
            Check(unchanged.SequenceEqual(baseline()), "disabled rotation hook replays Vanilla exactly");
            Check(Logic[Tile] == (protectedFlags | Land), "disabled rotation preserves flags");

            *(int*)state = 1;
            Check(unchanged.SequenceEqual(baseline()), "active rotation preserves Vanilla registers and flags");
            Check(Logic[Tile] == (protectedFlags | Land) &&
                originalNeighbours.SequenceEqual(RotationTestDeltas.Select(delta => Logic[Tile + delta])),
                "active rotation leaves neighbouring tiles unchanged");

            foreach (int rotation in new[] { 3, 5, 7, 1 })
            {
                *(int*)((byte*)manager + RotatedNeighbourIndex) = rotation;

                // Antler: view 0 selects a wall and therefore does not scan for the height edge.
                PrepareRotationTile(72, 0x40, 3, protectedFlags);
                Logic[Tile + RotationTestDeltas[0]] = 0;
                RotationHeight[Tile + RotationTestDeltas[0]] = 80;
                Logic[Tile + RotationTestDeltas[3]] = (uint)TilePropertyFlag.IsWall;
                baseline();
                Check(Logic[Tile] == (protectedFlags | Land), "Antler shore agrees across all camera orientations");

                // The canonical neighbour is a moat, and the first qualifying height edge is dry land.
                PrepareRotationTile(72, 0x02, 3, protectedFlags | Land);
                RotationHeight[Tile + RotationTestDeltas[0]] = 80;
                Logic[Tile + RotationTestDeltas[0]] = 0;
                baseline();
                Check(Logic[Tile] == protectedFlags, "elevated moat dry edge clears land");

                // Vanilla stops at the first qualifying edge, even if that edge leaves the bit set.
                PrepareRotationTile(72, 0x04, 1, protectedFlags);
                RotationHeight[Tile + RotationTestDeltas[0]] = 80;
                Logic[Tile + RotationTestDeltas[0]] = (uint)TilePropertyFlag.River;
                RotationHeight[Tile + RotationTestDeltas[1]] = 80;
                Logic[Tile + RotationTestDeltas[1]] = 0;
                baseline();
                Check(Logic[Tile] == (protectedFlags | Land), "river first edge stops the ordered scan");

                PrepareRotationTile(8, 0, 1, protectedFlags | Land);
                RotationHeight[Tile + RotationTestDeltas[0]] = 7;
                Logic[Tile + RotationTestDeltas[0]] = 0;
                RotationHeight[Tile + RotationTestDeltas[1]] = 0;
                Logic[Tile + RotationTestDeltas[1]] = 0;
                baseline();
                Check(Logic[Tile] == protectedFlags, "low moat only considers a zero-height edge");

                PrepareRotationTile(9, 0x20, 1, protectedFlags | Land);
                baseline();
                Check(Logic[Tile] == protectedFlags, "elevated moat with no land soil flags is cleared");

                PrepareRotationTile(0, 0, 1, protectedFlags);
                baseline();
                Check(Logic[Tile] == protectedFlags, "zero-height moats keep Vanilla");

                PrepareRotationTile(72, 0x40, 0, protectedFlags);
                baseline();
                Check(Logic[Tile] == protectedFlags, "clean moats keep Vanilla");

                uint bordered = protectedFlags | (uint)TilePropertyFlag.MapBorder;
                PrepareRotationTile(72, 0x40, 1, bordered);
                baseline();
                Check(Logic[Tile] == bordered, "border tiles keep Vanilla");

                uint wall = (uint)TilePropertyFlag.IsWall;
                PrepareRotationTile(72, 0x40, 1, wall);
                baseline();
                Check(Logic[Tile] == wall, "ordinary walls keep Vanilla");

                PrepareRotationTile(72, 0x40, 1, protectedFlags | Land);
                RotationHeight[Tile + RotationTestDeltas[0]] = 0;
                Logic[Tile + RotationTestDeltas[0]] = 0;
                ushort* structure = (ushort*)((byte*)manager + NativeDefinition.Structure);
                structure[Tile] = 71;
                baseline();
                Check(Logic[Tile] == protectedFlags && structure[Tile] == 71 && RotationHeight[Tile] == 72 &&
                    Soil[Tile] == 0x40 && RotationDirty[Tile] == 1, "bridge-edge update changes the land bit alone");

                // Bounds: unlike valid row data, this displacement must never be dereferenced.
                PrepareRotationTile(72, 0x40, 1, protectedFlags);
                ((int*)manager)[RotationTestRow * 8 + 3] = GameTileManagerView.NativePackedTileCapacity;
                baseline();
                Check(Logic[Tile] == (protectedFlags | Land), "out-of-array reference neighbour retains deterministic base result");
            }
        }
        Console.WriteLine("PASS: multiplayer moat land correction, installed RedBird trampoline and camera-independent native cases.");
    }

    private static void PrepareRotationTile(byte height, byte soil, byte dirty, uint flags)
    {
        RotationHeight[Tile] = height;
        Soil[Tile] = soil;
        RotationDirty[Tile] = dirty;
        Logic[Tile] = flags;
        for (int index = 0; index < RotationTestDeltas.Length; index++)
        {
            int delta = RotationTestDeltas[index];
            ((int*)manager)[RotationTestRow * 8 + index] = delta;
            RotationHeight[Tile + delta] = height;
            Logic[Tile + delta] = Moat;
        }
    }

    private static Func<ulong[]> PrepareRotationInput()
    {
        var setup = new Assembler(64);
        Setup(setup, TerrainSiteKind.Excavated, 2, Tile, false);
        setup.mov(r12d, Tile);
        setup.mov(__dword_ptr[rsp + 0x20], RotationTestRow);
        setup.mov(__dword_ptr[rsp + 0x34], 1234);
        setup.jmp(C + 0x1000);
        Write(setup, C);
        var call = Marshal.GetDelegateForFunctionPointer<RunCode>(code);
        return () =>
        {
            call();
            Check(Marshal.ReadInt64(output, 56) == Marshal.ReadInt64(output, 128 + 56), "rotation continuation preserves stack pointer");
            return Enumerable.Range(0, 16).Where(index => index != 7)
                .Select(index => unchecked((ulong)Marshal.ReadInt64(output, 128 + index * 8)))
                .Concat(Enumerable.Range(0, 32).Select(index => unchecked((ulong)Marshal.ReadInt64(output, 512 + index * 8))))
                .ToArray();
        };
    }
}
