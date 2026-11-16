using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using APIShared.Automation;
using BepInEx.Bootstrap;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CastlePlannerRotationTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> EditorEast(AutomationRun run)
        {
            yield return AutomationWait.Until("ready main menu", MainMenuReady);
            object plugin = Chainloader.PluginInfos["CastlePlanner_Serp"].Instance;
            settings = Get(plugin, "Settings");
            Set(settings, "EnableMod", true);
            Set(settings, "EnableClientFeatures", true);
            Set(settings, "Blueprints", true);
            preview = plugin.GetType().GetField("previewRuntime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            yield return AutomationWait.Until("castle catalog loaded", () =>
                (bool)settings.GetType().GetMethod("EnsureCastleCatalogLoaded", Instance).Invoke(settings, null));
            string castle = ((IEnumerable)Get(settings, "CastleOptions")).Cast<object>().Select(Convert.ToString)
                .FirstOrDefault(c => c.IndexOf("Community_wolf1.aivjson", StringComparison.OrdinalIgnoreCase) >= 0);
            Require(castle != null, "Community_wolf1 missing from editor fixture");
            Set(settings, "SelectedCastle", castle);
            MainViewModel.Instance.ButtonMainMenuFunction("MapEditor");
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            MainViewModel.Instance.MapEditorSetupFunction("Start");
            yield return AutomationWait.Until("managed editor session", () => run.CurrentMission?.IsEditor == true);
            yield return run.WaitForPlayState(run.CurrentMission);
            yield return AutomationWait.Until("editor render state", () => GameData.Instance.lastGameState != null && EditorDirector.instance != null);
            player = EditorDirector.instance.ActivePlayerID;
            Require(player >= 1 && player <= 8, "editor has no controlled player");
            expectedRotation = 2;
            var monitorField = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            Require(monitorField != null && monitorField.FieldType == typeof(object), "engine monitor contract changed");
            object engine = monitorField.GetValue(null);
            Require(engine != null, "engine monitor absent");
            if (!Monitor.TryEnter(engine, 100)) throw new InvalidOperationException("editor engine busy");
            try { CreateEditorKeep(); }
            finally { Monitor.Exit(engine); }
            Require(!Convert.ToBoolean(Get(preview, "IsPreviewActive")), "editor incorrectly opened castle-spawn preview");
            MethodInfo reader = preview.GetType().GetMethod("TryReadKeepRotation", BindingFlags.Static | BindingFlags.NonPublic);
            object[] args = { player, 0 };
            Require((bool)reader.Invoke(null, args) && (int)args[1] == expectedRotation, "editor production direction reader failed");

            object blueprint = plugin.GetType().GetField("blueprintRuntime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            object hud = Get(blueprint, "Hud");
            var toggle = (ICommand)Get(hud, "ToggleCommand");
            yield return AutomationWait.Until("editor Blueprint toggle", () => toggle.CanExecute(null));
            toggle.Execute(null);
            yield return AutomationWait.Until("editor Blueprint layout", () => Field(blueprint, "layout") != null);
            object layout = Field(blueprint, "layout");
            Require(Convert.ToInt32(Get(layout, "CastleRotation")) == 90, "editor Blueprint fallback direction differs");
            log("editor verified real rotatedKeep direction=2 BlueprintRotation=" + Get(layout, "CastleRotation"));
        }

        private unsafe void CreateEditorKeep()
        {
            Require(CountOwned() == 0, "fresh editor fixture unexpectedly contains owned buildings");
            var tiles = GameTileManagerAPI.Instance;
            int foundX = -1, foundY = -1;
            for (int y = 300; y <= 500 && foundX < 0; y += 10)
                for (int x = 300; x <= 500 && foundX < 0; x += 10)
                {
                    bool free = true;
                    for (int dy = -10; dy < 20 && free; dy++)
                        for (int dx = -10; dx < 20 && free; dx++)
                            free = tiles.IsTileInsideMapBounds(x + dx, y + dy) &&
                                tiles.GetTileBuildingId(tiles.GetTileId(x + dx, y + dy)) == 0;
                    if (free) { foundX = x; foundY = y; }
                }
            Require(foundX > 0, "no clear editor region for Keep compound");
            GameBuildingManagerAPI.Instance.CreatePrefab(player, foundX, foundY, eMappers.MAPPER_KEEP2, 7, 2, bIsFree: true);
            bool found = false;
            foreach (GameBuilding building in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
                if (building.r_PlayerIdOwner == player && building.r_BuildingType == eStructs.STRUCT_KEEP_TWO &&
                    building.r_TilePositionXBegin == foundX && building.r_TilePositionYBegin == foundY &&
                    (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit))
                {
                    Require(building.N00012042_2 == 2, "editor native constructor lost requested direction");
                    found = true;
                    log("editor Keep at=(" + foundX + "," + foundY + ") actualDirection=" + building.N00012042_2 + " sprite=" + building.r_SpriteVariationIndex);
                }
            Require(found, "editor ordinary prefab call did not create expected Keep");
        }
    }
}
