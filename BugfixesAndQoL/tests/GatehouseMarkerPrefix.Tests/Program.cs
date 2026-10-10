using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using BugfixesAndQoL;

// Controlled publisher/API fixtures; the actual production prefix and policy are source-linked.
// The input adapter is the audited mode-8 21-short record, not the old filter's assumptions.
unsafe class Program
{
    static int cases;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main(string[] args)
    {
        IntPtr flags = Marshal.AllocHGlobal(20);
        var buildings = SHCDESE.API.GameBuildingManagerAPI.Instance;
        try
        {
            for (int i = 0; i < 20; i++) Marshal.WriteByte(flags, i, 0);
            Marshal.WriteInt32(flags, 0, 1);
            var settings = new BugfixesAndQoLViewModel();
            if (args.Length != 0) Shared.DebugLogHelper.CurrentNativeSha256 = "unknown";
            var context = new SHCDESE.API.LowLevel.CrusaderLibraryLoadContext { ModuleHandle = flags - 0x60AD43C };
            GatehouseTargetMarkerHeightHook.Install(context, new BepInEx.Logging.ManualLogSource(), settings);
            if (args.Length != 0)
            {
                Require(HarmonyLib.Harmony.Patches == 0, "unknown build installed hook");
                Require(Shared.DebugLogHelper.Logs.Count == 1, "unknown build missing fail-open log");
                Console.WriteLine("PASS: unknown native build fails open before publication.");
                return 0;
            }
            Require(HarmonyLib.Harmony.Patches == 1, "prefix not installed once");
            foreach (var parameter in HarmonyLib.Harmony.Prefix.GetParameters())
                if (parameter.Name != "__instance")
                {
                    var target = Array.Find(typeof(GameMap).GetMethod("addUpdatePixie").GetParameters(), p => p.Name == parameter.Name);
                    Require(target != null && target.ParameterType == (parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType() : parameter.ParameterType),
                        "prefix binding differs from real target signature: " + parameter.Name);
                }
            var map = new GameMap();
            var tiles = SHCDESE.API.GameTileManagerAPI.Instance;
            Action reset = () => {
                settings.EnableClientFeatures = true; settings.EnableGatehouseTargetMarkerHeightFix = true; settings.Changed();
                map.Tile = new GameMapTile { gameMapX = 31, gameMapY = 47 }; map.Throw = false;
                SHCDESE.API.GameTileManagerAPI.Instance = tiles; SHCDESE.API.GameBuildingManagerAPI.Instance = buildings;
                tiles.InBounds = true; tiles.ValidTile = true; tiles.Flags = 0x100; tiles.BuildingId = 7;
                buildings.Valid = true; buildings.Found = true; buildings.Null = false; buildings.Building->r_BuildingType = 45;
                Marshal.WriteInt32(flags, 0, 1); Marshal.WriteInt32(flags, 8, 0); Marshal.WriteInt32(flags, 16, 1);
            };
            Action<short[], bool> check = (record, adjusted) => {
                short[] before = (short[])record.Clone();
                int output = map.RenderRecord(record);
                Require(output == -before[9] + (adjusted ? 20 : 0), "wrong prefix height/sign/double adjustment");
                for (int i = 0; i < record.Length; i++) Require(record[i] == before[i], "native record mutated");
                Require(map.LastHorizontal == before[8] && map.LastDelay == before[12] && map.LastTransparency == before[11], "horizontal/sort/transparency changed");
                cases++;
            };
            reset();
            // Each frame, both gates, independent sorting delays, fresh and cached updates.
            foreach (short horizontal in new short[] {12, 10})
            foreach (int type in new[] {45, 46})
                for (int image = horizontal == 12 ? 82 : 90; image <= (horizontal == 12 ? 89 : 105); image++)
                    foreach (short delay in new short[] {-20, 0, 4, 12, 127})
                    foreach (short transparency in new short[] {0, 4, 16, 28, 32})
                    {
                        buildings.Building->r_BuildingType = type;
                        var record = Record(image, horizontal, delay); record[11] = transparency;
                        check(record, true); check(record, true);
                    }
            Require(buildings.LastId == 7, "one-based building ID changed");
            Require(tiles.LastX == 31 && tiles.LastY == 47, "managed tile coordinates ignored");
            // All other types include five keeps, five towers and destroyed tower records.
            foreach (short horizontal in new short[] {12, 10})
            {
            Func<short[]> familyRecord = () => Record(horizontal == 12 ? 82 : 90, horizontal);
            for (int type = 0; type <= 109; type++) if (type != 45 && type != 46)
            { reset(); buildings.Building->r_BuildingType = type; check(familyRecord(), false); }
            foreach (uint value in new uint[] {0, 0x400, 0x10000000, 0x10000100})
            { reset(); tiles.Flags = value; check(familyRecord(), false); }
            for (int detailed = 0; detailed < 2; detailed++)
                for (int flattened = 0; flattened < 2; flattened++)
                    for (int allowed = 0; allowed < 2; allowed++)
                    {
                        reset(); Marshal.WriteInt32(flags, 0, detailed); Marshal.WriteInt32(flags, 8, flattened); Marshal.WriteInt32(flags, 16, allowed);
                        check(familyRecord(), detailed == 1 && !(flattened == 1 && allowed == 1));
                    }
            foreach (Action exclude in new Action[] {
                () => map.Tile = null, () => tiles.InBounds = false, () => tiles.ValidTile = false,
                () => tiles.BuildingId = 0, () => tiles.BuildingId = -1, () => buildings.Valid = false,
                () => buildings.Found = false, () => buildings.Null = true,
                () => SHCDESE.API.GameTileManagerAPI.Instance = null, () => SHCDESE.API.GameBuildingManagerAPI.Instance = null,
                () => settings.EnableClientFeatures = false, () => settings.EnableGatehouseTargetMarkerHeightFix = false
            }) { reset(); exclude(); settings.Changed(); check(familyRecord(), false); }
            reset(); var wrongFile = familyRecord(); wrongFile[3] = 106; check(wrongFile, false);
            reset(); var hiMode = familyRecord(); hiMode[0] = 9; check(hiMode, false);
            }
            foreach (int image in new[] {81, 90, 91}) { reset(); check(Record(image), false); }
            foreach (int image in new[] {82, 89, 106}) { reset(); check(Record(image, 10), false); }
            foreach (int image in new[] {90, 105}) { reset(); check(Record(image, 12), false); }
            // Old filter would accept these because layerDelay == 12; tile_x is authoritative.
            foreach (short horizontal in new short[] {-1, 0, 10, 13}) { reset(); check(Record(83, horizontal, 12), false); }
            // The first frame (82) and a delay other than 12 must be accepted.
            reset(); check(Record(82, 12, 0), true);
            GatehouseTargetMarkerHeightHook.Install(context, new BepInEx.Logging.ManualLogSource(), settings);
            Require(HarmonyLib.Harmony.Patches == 1, "duplicate hook installation");
            Require(Shared.DebugLogHelper.Logs.Count == 4, "logs are missing or unbounded");
            Require(Shared.DebugLogHelper.Logs[0].Contains("INSTALLED") && Shared.DebugLogHelper.Logs[0].Contains("enabled=True"), "activation log missing");
            Require(Shared.DebugLogHelper.Logs[1].Contains("POST_STARTUP") && Shared.DebugLogHelper.Logs[1].Contains("layerDelay=-20"), "first observed cursor log wrong");
            Require(Shared.DebugLogHelper.Logs[2].Contains("APPLIED") && Shared.DebugLogHelper.Logs[2].Contains("104 -> 124"), "applied height evidence wrong");
            Require(Shared.DebugLogHelper.Logs[2].Contains("family=Persistent") && Shared.DebugLogHelper.Logs[3].Contains("family=ClickAnimation") &&
                Shared.DebugLogHelper.Logs[3].Contains("104 -> 124"), "per-family height evidence missing");
            // A failed lookup permanently fails open; settings never republish or remove a hook.
            reset(); map.Throw = true; check(Record(), false); reset(); check(Record(), false);
            check(Record(90, 10), false);
            Require(HarmonyLib.Harmony.Patches == 1 && Shared.DebugLogHelper.Logs.Count == 5, "failure lifetime/log contract");
            Console.WriteLine("PASS: " + cases + " production-prefix native-record cases; bounded logs and permanent lifetime.");
            return 0;
        }
        finally { Marshal.FreeHGlobal(flags); Marshal.FreeHGlobal((IntPtr)buildings.Building); }
    }
    static short[] Record(int image = 82, short horizontal = 12, short delay = 4)
    {
        var record = new short[21];
        record[0] = 8; record[3] = 107; record[4] = (short)image;
        record[1] = 3; record[2] = 4; record[8] = horizontal; record[9] = -104; record[12] = delay;
        return record;
    }
}

public class GameMapTile { public int gameMapX, gameMapY; }
public class GameMap
{
    public GameMapTile Tile;
    public bool Throw;
    public int LastHeight, LastHorizontal, LastDelay, LastTransparency;
    public GameMapTile getMapTile(int x, int y) { if (Throw) throw new Exception("controlled lookup failure"); return Tile; }
    public void addUpdatePixie(int _objectID, int x, int y, int tile_x, int tile_y, float heightAboveGround,
        int file, int image, bool hiMode, int _transparency, int _layerDelay, int _colour)
    { LastHeight = tile_y; LastHorizontal = tile_x; LastDelay = _layerDelay; LastTransparency = _transparency; }
    public int RenderRecord(short[] r)
    {
        // Map-coordinate projection is supplied by the controlled getMapTile fixture.
        object[] args = {this, (int)r[1], (int)r[2], (int)r[8], -(int)r[9], (int)r[3], (int)r[4], r[0] == 9, (int)r[12]};
        HarmonyLib.Harmony.Prefix.Invoke(null, args);
        addUpdatePixie(r[10], r[1], r[2], r[8], (int)args[4], -r[5], r[3], r[4], r[0] == 9, r[11], r[12], r[7]);
        return LastHeight;
    }
}
namespace BepInEx.Logging { public class ManualLogSource {} }
namespace HarmonyLib
{
    public class HarmonyMethod
    {
        internal MethodInfo Method;
        public HarmonyMethod(Type type, string name) { Method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static); }
    }
    public class Harmony
    {
        public static int Patches; public static MethodInfo Prefix;
        public Harmony(string name) {}
        public void Patch(MethodInfo target, HarmonyMethod prefix) { Patches++; Prefix = prefix.Method; }
    }
}
namespace SHCDESE.Interop { public struct GameBuilding { public int r_BuildingType; } }
namespace SHCDESE.API.LowLevel { public class CrusaderLibraryLoadContext { public IntPtr ModuleHandle; } }
namespace SHCDESE.API
{
    public class GameTileManagerAPI
    {
        public static GameTileManagerAPI Instance = new GameTileManagerAPI();
        public bool InBounds, ValidTile; public uint Flags; public int BuildingId, LastX, LastY;
        public bool IsTileInsideMapBounds(int x, int y) { return InBounds; }
        public int GetTileId(int x, int y) { LastX = x; LastY = y; return 11; }
        public bool IsValidTileId(int id) { return ValidTile; }
        public uint GetTilePropertyFlag(int id) { return Flags; }
        public int GetTileBuildingId(int id) { return BuildingId; }
    }
    public unsafe class GameBuildingManagerAPI
    {
        public static GameBuildingManagerAPI Instance = new GameBuildingManagerAPI();
        public SHCDESE.Interop.GameBuilding* Building = (SHCDESE.Interop.GameBuilding*)Marshal.AllocHGlobal(sizeof(SHCDESE.Interop.GameBuilding));
        public bool Valid, Found, Null; public int LastId;
        public bool IsValidId(int id) { return Valid && id > 0; }
        public bool TryGetBuildingById(int id, out SHCDESE.Interop.GameBuilding* result) { LastId = id; result = Null ? null : Building; return Found; }
    }
}
namespace BugfixesAndQoL
{
    public class BugfixesAndQoLViewModel
    {
        public bool EnableClientFeatures = true, EnableGatehouseTargetMarkerHeightFix = true;
        public event Action<string> SettingChanged;
        public void Changed() { SettingChanged?.Invoke(nameof(EnableGatehouseTargetMarkerHeightFix)); }
    }
}
namespace Shared
{
    public static class DebugLogHelper
    {
        public static string CurrentNativeSha256 = GatehouseTargetMarkerHeightPolicy.NativeSha256;
        public static List<string> Logs = new List<string>();
        public static bool IsCurrentNativeLibraryVersion() { return true; }
        public static void LogInfo(BepInEx.Logging.ManualLogSource log, string text) { Logs.Add(text); }
        public static void LogError(BepInEx.Logging.ManualLogSource log, string text) { Logs.Add(text); }
    }
}
