using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Automation;
using HarmonyLib;

namespace CrenelatedWallAcceptance
{
    // Emulated map mouse without OS input: the editor reads the hovered map tile from
    // MainControls.getMouseMapTilePosition and the left button from getMouseStateForEngine once per
    // simulation run, then performs its own tool action (here the bulldozer) like for a real click.
    internal sealed partial class Fixture
    {
        private static Harmony mouseHarmony;
        private static readonly object mouseSync = new object();
        private static bool mouseActive;
        private static float mouseMapX, mouseMapY;
        private static readonly Queue<int> mouseStates = new Queue<int>();
        private static int mouseReads;

        private static void InstallMouse()
        {
            if (mouseHarmony != null) return;
            mouseHarmony = new Harmony(Plugin.Guid + ".mouse");
            mouseHarmony.Patch(AccessTools.Method(typeof(MainControls), "getMouseMapTilePosition"),
                postfix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(AfterMouseTile), BindingFlags.Static | BindingFlags.NonPublic)));
            mouseHarmony.Patch(AccessTools.Method(typeof(EditorDirector), "getMouseStateForEngine"),
                postfix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(AfterMouseState), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        private static void AfterMouseTile(ref float mapX, ref float mapY)
        {
            lock (mouseSync) { if (!mouseActive) return; mapX = mouseMapX; mapY = mouseMapY; }
        }

        // The hidden real cursor may rest over the HUD; an emulated map click is not over the UI.
        private static readonly FieldInfo overNoesisUi = AccessTools.Field(typeof(EditorDirector), "overNoesisUI");

        private static void AfterMouseState(EditorDirector __instance, ref int __result)
        {
            lock (mouseSync)
            {
                if (!mouseActive) return;
                overNoesisUi.SetValue(__instance, false);
                __result = mouseStates.Count > 0 ? mouseStates.Dequeue() : 0;
                mouseReads++;
            }
        }

        // Render-map coordinates of a game tile (the editor maps them back through GameMap.getMapTile).
        private static bool MapTileOf(int x, int y, out int mx, out int my)
        {
            for (mx = 1; mx < GameMap.tilemapSize; mx++)
                for (my = 1; my < GameMap.tilemapSize; my++)
                {
                    var tile = GameMap.instance.getMapTile(mx, my);
                    if (tile != null && tile.gameMapX == x && tile.gameMapY == y) return true;
                }
            mx = my = -1; return false;
        }

        // Hover, press, hold and release on one game tile with the currently selected editor tool.
        private IEnumerable<AutomationWait> MouseClick(int x, int y, params int[] states)
        {
            InstallMouse();
            Require(MapTileOf(x, y, out int mx, out int my), "no render-map tile for " + x + "," + y);
            int target;
            lock (mouseSync)
            {
                mouseMapX = mx + 0.5f; mouseMapY = my + 0.5f;
                mouseStates.Clear(); mouseStates.Enqueue(0); mouseStates.Enqueue(0);
                foreach (int s in states) mouseStates.Enqueue(s);
                mouseStates.Enqueue(0); mouseStates.Enqueue(0);
                target = mouseReads + mouseStates.Count;
                mouseActive = true;
            }
            yield return AutomationWait.Until("emulated mouse consumed", () => { lock (mouseSync) return mouseReads >= target; });
            lock (mouseSync) { mouseActive = false; mouseStates.Clear(); }
            log("MOUSE tile=" + x + "," + y + " map=" + mx + "," + my + " states=" + string.Join(",", states));
        }
    }
}
