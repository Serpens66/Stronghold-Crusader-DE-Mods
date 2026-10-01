using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using System;
using System.Reflection;

namespace SpectatorEditorBuildTest
{
    [BepInDependency("000shcdese", "2.12.0")]
    [BepInDependency("APIShared_Serp", "0.4.7")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class SpectatorEditorBuildTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "SpectatorEditorBuildTest_Serp";
        public const string Name = "Spectator Editor Build Test";
        public const string Version = "0.1.0";

        // The plugin component is destroyed during startup. Hooks, callbacks, and the logger
        // therefore remain rooted in static fields for the entire process lifetime.
        private static Harmony harmony;
        private static ManualLogSource log;
        private static HUD_Main activeHud;
        private static int lastOwner = int.MinValue;
        private static int lastScreen = int.MinValue;
        private static FieldInfo buildIconListsField;
        private static FieldInfo buildButtonsField;
        private static bool runtimeConfirmed;
        private static bool failureLogged;
        private static IDisposable tribeSubscription;
        private static int unitDiagnostics;
        private static int unitRequests;
        [ThreadStatic] private static int placingUnitOwner;

        private void Awake()
        {
            if (harmony != null) return;
            log = Logger;
            var candidate = new Harmony(Guid);
            IDisposable candidateTribeSubscription = null;
            try
            {
                ValidateHudFields();
                Patch(candidate, typeof(HUD_Main), nameof(HUD_Main.SetupModeDependantUI),
                    nameof(AfterSetupModeDependantUI), postfix: true);
                Patch(candidate, typeof(HUD_Main), nameof(HUD_Main.SetupNewBuildScreen),
                    nameof(AfterSetupNewBuildScreen), postfix: true, typeof(int));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.CanPlaceMapper),
                    nameof(AfterCanPlaceMapper), postfix: true, typeof(object));
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.reportSubModeTabChange),
                    nameof(BeforeReportSubModeTabChange), postfix: false, typeof(int));
                Patch(candidate, typeof(EngineInterface), nameof(EngineInterface.PlaceMapperItem),
                    nameof(BeforePlaceMapperItem), postfix: false,
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(bool), typeof(bool), typeof(int));
                Patch(candidate, typeof(EngineInterface), nameof(EngineInterface.PlaceMapperItem),
                    nameof(AfterPlaceMapperItem), postfix: true,
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(bool), typeof(bool), typeof(int));
                candidateTribeSubscription = TribeR3EventHooks.OnTribeAssignUnit.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnTribeAssigned);
                tribeSubscription = candidateTribeSubscription;
                harmony = candidate;
                UnityEngine.Application.onBeforeRender += OnBeforeRender;
                log.LogInfo("SPECTATOR_EDITOR_HOOKS_INSTALLED");
            }
            catch (Exception error)
            {
                // This candidate has not been published; a failed installation may be rolled back.
                candidateTribeSubscription?.Dispose();
                candidate.UnpatchSelf();
                log.LogError("SPECTATOR_EDITOR_INIT_FAILED: " + error);
            }
        }

        private static void Patch(Harmony candidate, Type targetType, string targetName,
            string patchName, bool postfix, params Type[] arguments)
        {
            MethodInfo target = AccessTools.Method(targetType, targetName, arguments);
            MethodInfo patch = AccessTools.Method(typeof(SpectatorEditorBuildTestPlugin), patchName);
            if (target == null || patch == null)
                throw new MissingMethodException(targetType.FullName, targetName);
            candidate.Patch(target, prefix: postfix ? null : new HarmonyMethod(patch),
                postfix: postfix ? new HarmonyMethod(patch) : null);
        }

        private static void ValidateHudFields()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            buildIconListsField = typeof(HUD_Main).GetField("BuildIconLists", flags);
            buildButtonsField = typeof(HUD_Main).GetField("buildButtons", flags);
            if (buildIconListsField?.FieldType != typeof(int[,]) ||
                buildButtonsField?.FieldType != typeof(Button[]))
                throw new MissingFieldException("HUD_Main", "BuildIconLists/buildButtons");
        }

        private static bool IsSpectatorSession()
        {
            try
            {
                var state = GameData.Instance?.lastGameState;
                var director = Director.instance;
                var editor = EditorDirector.instance;
                var view = MainViewModel.Instance;
                return state != null && director != null && editor != null && view != null &&
                    state.game_type == 3 && state.spectatorMode != 0 &&
                    editor.ActivePlayerID <= 0 && !view.IsMapEditorMode &&
                    director.SkirmishModeGame && !director.MultiplayerGame &&
                    !Shared.GameModeHelper.IsRealMultiplayer();
            }
            catch { return false; }
        }

        private static int CurrentAiOwner()
        {
            if (!IsSpectatorSession()) return -1;
            try
            {
                int owner = PlayerPerspectiveAPI.GetViewedPlayerId();
                return owner >= 1 && owner <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(owner) ? owner : -1;
            }
            catch { return -1; }
        }

        private static void OnBeforeRender()
        {
            try
            {
                if (!IsSpectatorSession())
                {
                    activeHud = null;
                    lastOwner = int.MinValue;
                    lastScreen = int.MinValue;
                    return;
                }
                MainViewModel view = MainViewModel.Instance;
                HUD_Main hud = view.HUDmain;
                if (hud == null || !view.Show_HUD_Main) return;
                int owner = CurrentAiOwner();
                int screen = view.buildScreenID;
                if (!ReferenceEquals(hud, activeHud) || owner != lastOwner || screen != lastScreen)
                {
                    if (screen <= 0 || screen == 15 || screen == 17 ||
                        screen == 18 || screen == 21 || screen >= 23)
                        hud.SetupNewBuildScreen(1);
                    else
                        RefreshBuildIcons(hud, owner, "session");
                }
                if (!runtimeConfirmed)
                {
                    runtimeConfirmed = true;
                    log.LogInfo("SPECTATOR_EDITOR_RUNTIME_AFTER_STARTUP: HUD active; owner=" +
                        owner + " screen=" + view.buildScreenID);
                }
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_HUD_ERROR: " + error);
            }
        }

        private static void AfterSetupModeDependantUI(HUD_Main __instance)
        {
            if (!IsSpectatorSession()) return;
            try
            {
                SetVisible(__instance, "MainFrameBuildings", true);
                SetVisible(__instance, "MainFrameTerrain", false);
                SetVisible(__instance, "BuildMenuGrid", true);
                SetVisible(__instance, "MEMenuGrid", false);
                SetVisible(__instance, "BottomTabs1", true);
                SetVisible(__instance, "RadioButtonMETroops", true);
                SetVisible(__instance, "RadioButtonMEArabTroops", true);
                SetVisible(__instance, "RadioButtonMESiege", true);
                SetVisible(__instance, "RadioButtonMEBedouin", true);
                SetVisibility(__instance, "RadioButtonMERuins", Visibility.Collapsed);
                SetVisibility(__instance, "ButtonBuildModeTerrain", Visibility.Collapsed);
            }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UI_ERROR: " + error); }
        }

        private static void SetVisible(HUD_Main hud, string name, bool visible)
        {
            SetVisibility(hud, name, visible ? Visibility.Visible : Visibility.Hidden);
        }

        private static void SetVisibility(HUD_Main hud, string name, Visibility visibility)
        {
            var element = hud.FindName(name) as FrameworkElement;
            if (element == null) throw new MissingMemberException("HUD_Main", name);
            element.Visibility = visibility;
        }

        private static void AfterSetupNewBuildScreen(HUD_Main __instance)
        {
            if (!IsSpectatorSession()) return;
            try { RefreshBuildIcons(__instance, CurrentAiOwner(), "tab"); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_ICONS_ERROR: " + error); }
        }

        private static void RefreshBuildIcons(HUD_Main hud, int owner, string reason)
        {
            int[,] lists = (int[,])buildIconListsField.GetValue(hud);
            Button[] buttons = (Button[])buildButtonsField.GetValue(hud);
            int screen = MainViewModel.Instance.buildScreenID;
            if (lists == null || lists.GetLength(0) != 23 || lists.GetLength(1) != 17 ||
                buttons == null || buttons.Length != 588 || screen < 1 || screen >= 23)
                throw new InvalidOperationException("Unexpected HUD build-list layout or screen " + screen);

            bool[] known = new bool[buttons.Length];
            bool[] selected = new bool[buttons.Length];
            for (int alternate = 583; alternate <= 587; alternate++) known[alternate] = true;
            for (int page = 0; page < lists.GetLength(0); page++)
            {
                for (int slot = 0; slot < lists.GetLength(1); slot++)
                {
                    int index = lists[page, slot];
                    if (index <= 0) break;
                    if (index >= buttons.Length) throw new IndexOutOfRangeException("HUD build button " + index);
                    known[index] = true;
                    int alternate = AlternateBuildingButton(index);
                    if (alternate != index) known[alternate] = true;
                    if (page == screen) selected[alternate] = true;
                }
            }

            // Validate the current page before changing any visibility.
            for (int index = 1; index < buttons.Length; index++)
                if (selected[index] && buttons[index] == null)
                    throw new MissingMemberException("HUD_Main", "buildButtons[" + index + "]");

            int selectedCount = 0;
            int visibleCount = 0;
            int renderedCount = 0;
            int enabledCount = 0;
            for (int index = 1; index < buttons.Length; index++)
            {
                if (!known[index] || buttons[index] == null) continue;
                Button button = buttons[index];
                if (selected[index])
                {
                    selectedCount++;
                    button.IsEnabled = owner > 0 &&
                        MainViewModel.Instance.CanPlaceMapper(button.CommandParameter);
                    button.Visibility = Visibility.Visible;
                    if (button.Visibility == Visibility.Visible) visibleCount++;
                    if (button.IsVisible) renderedCount++;
                    if (button.IsEnabled) enabledCount++;
                }
                else
                {
                    button.IsEnabled = false;
                    button.Visibility = Visibility.Hidden;
                }
            }

            bool changed = !ReferenceEquals(hud, activeHud) || owner != lastOwner || screen != lastScreen;
            activeHud = hud;
            lastOwner = owner;
            lastScreen = screen;
            if (changed)
                log.LogInfo("SPECTATOR_EDITOR_ICONS reason=" + reason + " screen=" + screen +
                    " owner=" + owner + " selected=" + selectedCount + " visible=" +
                    visibleCount + " rendered=" + renderedCount + " enabled=" + enabledCount);
        }

        private static int AlternateBuildingButton(int index)
        {
            if (index != 87 && index != 88 && index != 89 && index != 417 && index != 172)
                return index;
            var state = GameData.Instance?.lastGameState;
            if (state == null || (state.lord_Type != 1 && state.lord_Type != 2 &&
                state.lord_Type != 6 && state.lord_Type != 7))
                return index;
            switch (index)
            {
                case 87: return 583;
                case 88: return 584;
                case 89: return 585;
                case 417: return 586;
                default: return 587;
            }
        }

        private static void AfterCanPlaceMapper(MainViewModel __instance, object parameter, ref bool __result)
        {
            if (!IsSpectatorSession()) return;
            string key = parameter as string;
            if (string.IsNullOrEmpty(key)) return;
            if (key == "STRUCT_MENU_RETURN_KEEPS")
            {
                __result = CurrentAiOwner() > 0;
                return;
            }
            eMappers mapper = (eMappers)(int)__instance.getStructToMapperEnum(key);
            if (!IsAllowedMapper(mapper)) return;
            __result = CurrentAiOwner() > 0;
        }

        private static bool BeforeReportSubModeTabChange(int newMode)
        {
            // Vanilla editor tab handlers already set MainViewModel.SubMode. In a live game,
            // passing editor submodes to SetAppMode would alter unrelated native game state.
            return !IsSpectatorSession();
        }

        private static bool BeforePlaceMapperItem(int item, int x, int y, int size,
            ref int player, ref bool inGameNotEditor, bool constructingOnly, int mouseState)
        {
            if (!IsSpectatorSession()) return true;
            eMappers mapper = (eMappers)item;
            if (!IsAllowedMapper(mapper)) return false;
            int owner = CurrentAiOwner();
            if (owner < 1) return false;
            player = owner;
            inGameNotEditor = false;
            if (IsUnitMapper(mapper))
            {
                if (mouseState == 3 && !constructingOnly)
                {
                    placingUnitOwner = owner;
                    if (unitRequests++ < 32)
                        log.LogInfo("SPECTATOR_EDITOR_UNIT_REQUEST owner=" + owner + " mapper=" +
                            mapper + " tile=" + x + "," + y);
                }
                return true;
            }
            bool buildingClick = mouseState == 1 ||
                (mapper == eMappers.MAPPER_ARAB_BALLISTA && mouseState == 3);
            if (constructingOnly || !buildingClick)
                return true;

            try
            {
                int scale = BuildingScales.GetScale(mapper);
                if (scale < 0) return false;
                long result = GameBuildingManagerAPI.Instance.CreatePrefab(
                    owner, x, y, mapper, scale, 0, true, false);
                log.LogInfo("SPECTATOR_EDITOR_BUILD owner=" + owner + " mapper=" + mapper +
                    " tile=" + x + "," + y + " result=" + result);
            }
            catch (Exception error)
            {
                log.LogError("SPECTATOR_EDITOR_BUILD_ERROR: " + error);
            }
            return false;
        }

        private static void AfterPlaceMapperItem()
        {
            placingUnitOwner = 0;
        }

        private static unsafe void OnTribeAssigned(TribeAssignUnitEventArgs args)
        {
            int owner = placingUnitOwner;
            if (owner <= 0 || unitDiagnostics >= 32) return;
            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(args.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_ControllableForPlayerId != owner)
                    return;
                unitDiagnostics++;
                log.LogInfo("SPECTATOR_EDITOR_UNIT_GROUP owner=" + owner + " unitId=" +
                    args.UnitId + " tribeId=" + args.TribeId + " aiRole=" + unit->r_AITribeRole);
            }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UNIT_DIAGNOSTIC_ERROR: " + error); }
        }

        private static bool IsAllowedMapper(eMappers mapper)
        {
            if (IsUnitMapper(mapper)) return true;
            string name = mapper.ToString();
            return BuildingScales.GetScale(mapper) >= 0 &&
                !name.Contains("RUINS") && !name.Contains("POND") &&
                !name.Contains("FLAG") && !name.Contains("SIGNPOST") &&
                !name.Contains("DELETE") && !name.Contains("ASSEMBLY") &&
                !name.Contains("MARKER");
        }

        private static bool IsUnitMapper(eMappers mapper)
        {
            int value = (int)mapper;
            return (value >= 0x10e && value <= 0x11e) ||
                   (value >= 0x15e && value <= 0x165) ||
                   (value >= 400 && value <= 407);
        }

        private static void LogOnce(string message)
        {
            if (failureLogged) return;
            failureLogged = true;
            log?.LogError(message);
        }
    }
}
