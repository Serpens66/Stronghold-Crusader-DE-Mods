using System;
using System.Collections.Generic;
using APIShared.Pathfinding;
using BepInEx.Logging;
using SHCDESE.API;

namespace MoatMove
{
    // Consumer of APIShared route tile rules (option AvoidFire): tiles with a fire effect cost the
    // largest entry penalty for every player, so engine routes avoid fire whenever a detour exists.
    // The replan policy covers units already walking when the fire starts: re-plan 3 steps before
    // the fire; a detour may add 50 % or at least 10 steps, otherwise wait up to about 10 s (400 ticks).
    // Fire on the ground (burning pitch, fire arrows and pots, burning buildings) is a type-9 record
    // of Vanilla's effect manager (UpdateToNewDLL.md, "Fire effects"); a pitch's OnFire state only
    // ignites it. The records are read on the simulation tick; nothing native is written.
    internal sealed unsafe class FireRouteRules
    {
        private const int FirePenalty = RouteTileRules.MaximumPenalty - 1, ScanInterval = 4, MapSize = RouteTileRules.MapSize;
        // FBCB9319 effect manager: records 1..5999 of 0xE8 bytes; +0x3C state (0 free), +0x3E type,
        // +0x60 native tile id. Derived from validated code anchors, never written.
        private const int EffectSlots = 6000, EffectStride = 0xE8, EffectState = 0x3C, EffectType = 0x3E, EffectTile = 0x60, FireType = 9;
        // A2E60 (effect removal pass): lea rdi,[manager]; imul rcx,rax,0E8h; cmp word [rcx+rdi+3Ch],2.
        private const int RemovalRva = 0xA2E60, RemovalLeaOffset = 0x0F;
        private const string RemovalPattern = "48 89 5C 24 08 57 48 83 EC 20 BB 01 00 00 00 48 8D 3D ?? ?? ?? ?? 89 1D ?? ?? ?? ?? 8B C3 66 90 48 98 48 69 C8 E8 00 00 00 66 83 7C 39 3C 02 75 1A";
        // A02D0 (fire of a tile): imul r8,rdx,0E8h; cmp word [r8+rcx+3Eh],9; movsx eax,word [r8+rcx+32h].
        private const int LookupRva = 0xA02D0;
        private const string LookupPattern = "48 63 C2 48 8D 15 ?? ?? ?? ?? 0F BF 04 42 85 C0 74 1D 48 63 D0 4C 69 C2 E8 00 00 00 66 41 83 7C 08 3E 09 74 0C 41 0F BF 44 08 32 85 C0 75 E3 33 C0 C3";

        private byte* effects;
        private RouteTileRuleSet rules;
        // Native tile ids per map (the path component grid has one entry per tile).
        private int tileCount;
        // Marked and this scan's burning nodes; swapped per scan.
        private HashSet<int> marked = new HashSet<int>(), burning = new HashSet<int>();
        internal int Burning => marked.Count;

        internal bool TryBind(ReadOnlySpan<byte> memory, ulong moduleBase, ManualLogSource log, out string reason)
        {
            reason = null;
            try
            {
                int removal = Shared.NativePatternResolver.ResolveUnique(memory, RemovalPattern, RemovalRva, true, "MoatMove fire effect manager", log).Rva;
                int lookup = Shared.NativePatternResolver.ResolveUnique(memory, LookupPattern, LookupRva, true, "MoatMove fire tile lookup", log).Rva;
                int manager = Shared.NativePatternResolver.ResolveRelativeTarget(memory, removal + RemovalLeaOffset + 3, removal + RemovalLeaOffset + 7);
                // The lookup's caller passes the same manager; its own lea addresses the tile heads,
                // which follow the records (second, independent anchor of the record layout).
                int heads = Shared.NativePatternResolver.ResolveRelativeTarget(memory, lookup + 6, lookup + 10);
                if (manager <= 0 || heads <= manager + (long)EffectSlots * EffectStride || manager >= memory.Length || heads >= memory.Length)
                { reason = $"effect layout anchors disagree (manager=0x{manager:X}, heads=0x{heads:X})"; return false; }
                effects = (byte*)(moduleBase + (ulong)manager);
                Shared.DebugLogHelper.LogInfo(log, $"MoatMove fire effects bound: manager=0x{manager:X} tileHeads=0x{heads:X} slots={EffectSlots} stride=0x{EffectStride:X}.");
                return true;
            }
            catch (Exception ex) { reason = ex.Message; return false; }
        }

        internal bool TryRegister(string ownerGuid, out string reason)
        {
            if (!RouteTileRules.TryRegister(ownerGuid, "fire", out rules, out reason)) return false;
            rules.ReplanPolicy = new RouteReplanPolicy(3, 50, 10, 400);
            return true;
        }

        internal void Tick(int tick)
        {
            if (rules == null || effects == null) return;
            if (tick % ScanInterval != 0) return;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tileCount == 0) tileCount = GamePathingManagerAPI.Instance.GetPathComponentGrid().Length;
            burning.Clear();
            for (int slot = 1; slot < EffectSlots; slot++)
            {
                byte* record = effects + slot * EffectStride;
                if (*(short*)(record + EffectState) == 0 || *(short*)(record + EffectType) != FireType) continue;
                int tile = *(int*)(record + EffectTile);
                if ((uint)tile >= (uint)tileCount) continue;
                var position = tiles.GetTileVectorFromId(tile);
                if (position.X >= MapSize || position.Y >= MapSize || tiles.GetTileId(position.X, position.Y) != tile) continue;
                int node = position.Y * MapSize + position.X;
                if (burning.Add(node) && !marked.Contains(node)) rules.Set(node % MapSize, node / MapSize, RouteTileEffect.Penalty, FirePenalty);
            }
            foreach (int node in marked)
                if (!burning.Contains(node)) rules.Clear(node % MapSize, node / MapSize);
            HashSet<int> swap = marked; marked = burning; burning = swap;
        }

        // A new map: the old map's fire and tile count no longer apply.
        internal void Reset() { rules?.ClearAll(); marked.Clear(); burning.Clear(); tileCount = 0; }
    }
}
