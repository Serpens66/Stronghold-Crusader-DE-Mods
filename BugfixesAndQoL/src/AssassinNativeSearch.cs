using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    // C ABI of native/AssassinSearch.cpp (BfqolAssassinSearch.dll, contract 0x01000001): the bucket A* of
    // AssassinPathfindingRuntime.TryBuildWeightedRoute with identical results. The DLL is loaded once by
    // full path from the plugin folder and stays loaded for the process; without it the managed search runs.
    internal static unsafe class AssassinNativeSearch
    {
        internal const string Library = "BfqolAssassinSearch.dll";
        internal const int Contract = 0x01000001;
        internal const int StatusNone = 0, StatusFound = 1, StatusOverflow = 2, StatusRejectedRoute = 3;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Tables
        {
            internal IntPtr ValidCoordinates, RowLookup, TileFlags, Occupancy, Building, Height, DirectionMasks,
                SpecialTile, SpecialContext, DirectGatehouseEndpoint, MaskedTransition, DirectionAllowed;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Request
        {
            internal int StartX, StartY, TargetX, TargetY, MaximumNodes, CardinalTicks, DiagonalTicks, AllowClimbing, AllowReserved;
            internal IntPtr PolicyMask;
            internal int PolicyMaskLength, UsePolicyCallback;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Result { internal int Status, Expanded, TotalCost, RouteLength, GroundEdges, ClimbEdges; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int TileCallback(int tile);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int MaskedTransitionCallback(int from, int to, int direction, int allowClimbing, int allowReserved);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int DirectionCallback(int tile, int direction);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int bfq_assassin_contract();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int bfq_assassin_search(Tables* tables, Request* request, int* routeNodes, int* routeCosts, int routeCapacity, Result* result);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        internal static bool Available { get; private set; }

        // Throws when the file is missing or another version is installed; callers keep the managed search.
        internal static void Load(string directory)
        {
            if (Available) return;
            string path = Path.Combine(directory ?? throw new ArgumentNullException(nameof(directory)), Library);
            if (!File.Exists(path)) throw new FileNotFoundException("Native Assassin search missing.", path);
            if (LoadLibraryEx(path, IntPtr.Zero, LoadWithAlteredSearchPath) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Loading " + path + " failed");
            int contract = bfq_assassin_contract();
            if (contract != Contract) throw new InvalidOperationException($"Native Assassin search contract 0x{contract:X8}, expected 0x{Contract:X8}.");
            Available = true;
        }
    }
}
