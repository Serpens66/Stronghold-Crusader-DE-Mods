using APIShared.GameModes;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Iced.Intel;
using Noesis;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Security.Cryptography;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static void AfterSetupModeDependantUI(HUD_Main __instance)
        {
            if (!IsPlacementSession() || IsEditorToolsSession()) return;
            try { EnsurePlacementHudLayout(__instance); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UI_ERROR: " + error); }
        }

        private static bool EnsurePlacementHudLayout(HUD_Main hud)
        {
            bool changed = false;
            changed |= EnsureVisibility(hud, "MainFrameBuildings", Visibility.Visible);
            changed |= EnsureVisibility(hud, "MainFrameTerrain", Visibility.Hidden);
            changed |= EnsureVisibility(hud, "BuildMenuGrid", Visibility.Visible);
            changed |= EnsureVisibility(hud, "MEMenuGrid", Visibility.Hidden);
            changed |= EnsureVisibility(hud, "BottomTabs1", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMETroops", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMEArabTroops", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMESiege", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMEBedouin", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMERuins", Visibility.Collapsed);
            changed |= EnsureVisibility(hud, "ButtonBuildModeTerrain", Visibility.Collapsed);
            return changed;
        }

        private static bool EnsureVisibility(HUD_Main hud, string name, Visibility visibility)
        {
            var element = hud.FindName(name) as FrameworkElement;
            if (element == null) throw new MissingMemberException("HUD_Main", name);
            if (element.Visibility == visibility) return false;
            element.Visibility = visibility;
            return true;
        }

        private static string EditorTabVisibility(HUD_Main hud)
        {
            string[] names = { "RadioButtonMETroops", "RadioButtonMESiege",
                "RadioButtonMEArabTroops", "RadioButtonMEBedouin" };
            string result = "";
            foreach (string name in names)
            {
                var tab = hud.FindName(name) as FrameworkElement;
                result += (result.Length == 0 ? "" : ",") + name + "=" +
                    (tab == null ? "missing" : tab.Visibility + "/" + tab.IsVisible);
            }
            return result;
        }

        private static void AfterSetupNewBuildScreen(HUD_Main __instance)
        {
            if (!IsPlacementSession() || IsEditorToolsSession()) return;
            try { RefreshBuildIcons(__instance, CurrentAiOwner(), "tab"); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_ICONS_ERROR: " + error); }
        }

        private static bool BeforeNewBuildScreenBlank()
        {
            if (!IsPlacementSession() || IsEditorToolsSession()) return true;
            if (++suppressedBlankCalls <= 3)
                log.LogInfo("SPECTATOR_EDITOR_BLANK_SUPPRESSED screen=" +
                    MainViewModel.Instance.buildScreenID);
            return false;
        }

        private static MainViewModel placementFreezeView;
        private static long placementFreezeSession;
        private static void RememberPlacementFreeze()
        {
            placementFreezeView = MainViewModel.Instance;
            placementFreezeSession = CurrentSessionId;
        }
        private static void RestorePlacementFreeze()
        {
            var previous = placementFreezeView;
            long session = placementFreezeSession;
            placementFreezeView = null;
            placementFreezeSession = 0;
            if (previous != null && MainViewModel.viewModelLoaded && ReferenceEquals(previous, MainViewModel.Instance) &&
                IsCurrentSession(session) && IsSpectatorSession()) previous.buildControlsFreeze(true);
        }

        private static bool BeforeBuildControlsFreeze(bool Mode)
        {
            if (!Mode || !IsSpectatorSession() || !IsPlacementSession()) return true;
            RememberPlacementFreeze();
            if (++suppressedFreezeCalls <= 3)
                log.LogInfo("SPECTATOR_EDITOR_FREEZE_SUPPRESSED frozen=" +
                    MainViewModel.Instance.FreezeMainControls);
            return false;
        }

        private static void LogVisibilityAfterAnimation(HUD_Main hud)
        {
            if (MainViewModel.Instance.buildScreenID != pendingVisibilityScreen ||
                CurrentAiOwner() != pendingVisibilityOwner)
                return;
            int[,] lists = (int[,])buildIconListsField.GetValue(hud);
            Button[] buttons = (Button[])buildButtonsField.GetValue(hud);
            int selected = 0;
            int visible = 0;
            int rendered = 0;
            int enabled = 0;
            for (int slot = 0; slot < lists.GetLength(1); slot++)
            {
                int index = lists[pendingVisibilityScreen, slot];
                if (index <= 0) break;
                Button button = buttons[AlternateBuildingButton(index)];
                if (button == null) continue;
                selected++;
                if (button.Visibility == Visibility.Visible) visible++;
                if (button.IsVisible) rendered++;
                if (button.IsEnabled) enabled++;
            }
            visibilityDiagnostics++;
            log.LogInfo("SPECTATOR_EDITOR_ICONS_SETTLED screen=" + pendingVisibilityScreen +
                " owner=" + pendingVisibilityOwner + " selected=" + selected +
                " visible=" + visible + " rendered=" + rendered + " enabled=" +
                enabled + " frozen=" + MainViewModel.Instance.FreezeMainControls +
                " blankSuppressed=" + suppressedBlankCalls);
        }

        private static void RefreshBuildIcons(HUD_Main hud, int owner, string reason)
        {
            if (IsEditorToolsSession()) return;
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
            {
                log.LogInfo("SPECTATOR_EDITOR_ICONS reason=" + reason + " screen=" + screen +
                    " owner=" + owner + " selected=" + selectedCount + " visible=" +
                    visibleCount + " rendered=" + renderedCount + " enabled=" + enabledCount);
                if (visibilityDiagnostics < 32)
                {
                    pendingVisibilityHud = hud;
                    pendingVisibilityScreen = screen;
                    pendingVisibilityOwner = owner;
                    pendingVisibilityAt = DateTime.UtcNow.AddMilliseconds(650);
                }
            }
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
            if (!IsPlacementSession() || IsEditorToolsSession()) return;
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
            return !IsPlacementSession() || IsEditorToolsSession();
        }

        private static void AfterGetMouseStateForEngine(ref int __result, ref bool rightDown, ref bool rightUp)
        {
            // Empty map areas may have no Noesis hit target, so their native input notification
            // complements the root preview event. It neither consumes nor replays the click.
            if ((__result == 1 || rightDown) && EditorDirector.instance != null && !EditorDirector.instance.overUI())
                ReleaseAmountForMapClick();
            try { ObserveToolMouse(ref __result, ref rightDown, ref rightUp); }
            catch (Exception error) { CancelPowerSelection(); LogOnce("TEST_POWER_INPUT_ERROR: " + error); }
            lastEditorMouseState = __result;
            if (activeWall != null && __result == 3 && wallReleaseDiagnostics++ < 16)
                log.LogInfo("SPECTATOR_EDITOR_WALL_MOUSE state=" + __result +
                    " owner=" + activeWall.Owner + " mapper=" + activeWall.Mapper +
                    " overUI=" + EditorDirector.instance.overUI());
        }

        private static void BeforeUpdateLeftMouseStateForEngine(int state)
        {
            if (state == 3 && (activeWall != null || activeSpecial != null))
                rawMouseReleasePending = true;
        }

        private static bool BeforeClearMouseStateForEngine()
        {
            return !IsPlacementSession() ||
                (activeWall == null && activeSpecial == null) ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 5 ||
                (activeWall != null &&
                    (eMappers)MainControls.instance.CurrentSubAction != activeWall.Mapper) ||
                (activeSpecial != null &&
                    (eMappers)MainControls.instance.CurrentSubAction != activeSpecial.Mapper);
        }

        private static void AfterPreDLLCallActions(ref int mouseOverX, ref int mouseOverY)
        {
            try { lock (engineLock) CompleteToolMouse(mouseOverX, mouseOverY); }
            catch (Exception error) { CancelPowerSelection(); LogOnce("TEST_TOOL_SELECTION_ERROR: " + error); }
            WallRequest wall = activeWall;
            int state = lastEditorMouseState;
            lastEditorMouseState = 0;
            if ((wall != null || activeSpecial != null) && state == 0 &&
                rawMouseReleasePending &&
                EditorDirector.instance != null && !EditorDirector.instance.overUI())
            {
                state = 3;
                if (wallReleaseDiagnostics++ < 16)
                    log.LogInfo("SPECTATOR_EDITOR_RELEASE_RECOVERED tile=" +
                        mouseOverX + "," + mouseOverY);
            }
            rawMouseReleasePending = false;
            if (wall != null && (state == 1 || state == 3) &&
                wallInputDiagnostics++ < 24)
                log.LogInfo("SPECTATOR_EDITOR_WALL_FORWARD state=" + state +
                    " tile=" + mouseOverX + "," + mouseOverY +
                    " action=" + MainControls.instance?.CurrentAction);
            ForwardSpecialDrag(state, mouseOverX, mouseOverY);
            if (wall == null || (state != 2 && state != 3)) return;
            if (EditorDirector.instance != null && EditorDirector.instance.overUI())
            {
                if (state == 3) activeWall = null;
                return;
            }
            if (!IsPlacementSession() || MainControls.instance == null ||
                MainControls.instance.CurrentAction != 5 ||
                (eMappers)MainControls.instance.CurrentSubAction != wall.Mapper ||
                !IsCurrentSession(wall.SessionId))
            {
                activeWall = null;
                return;
            }
            int tileX = mouseOverX;
            int tileY = mouseOverY;
            if (tileX < 0 || tileY < 0)
            {
                if (state != 3) return;
                tileX = wall.EndX;
                tileY = wall.EndY;
            }
            if (state == 2)
            {
                wall.EndX = tileX;
                wall.EndY = tileY;
            }
            if (state == 3)
            {
                wall.EndX = tileX;
                wall.EndY = tileY;
                wall.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5);
                pendingWall = wall;
                activeWall = null;
            }
            EngineInterface.PlaceMapperItem((int)wall.Mapper, tileX, tileY,
                0, wall.Owner, false, state == 2, state);
            if (state == 3)
            {
                wallQueueDiagnostics++;
                log.LogInfo("SPECTATOR_EDITOR_WALL_REQUEST session=" + wall.SessionId +
                    " owner=" + wall.Owner +
                    " mapper=" + wall.Mapper + " start=" + wall.StartX + "," + wall.StartY +
                    " end=" + wall.EndX + "," + wall.EndY);
            }
        }

        private static void ForwardSpecialDrag(int state, int tileX, int tileY)
        {
            SpecialDragRequest special = activeSpecial;
            if (special == null || (state != 1 && state != 2 && state != 3)) return;
            if (!IsPlacementSession() || !IsCurrentSession(special.SessionId) ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 5 ||
                (eMappers)MainControls.instance.CurrentSubAction != special.Mapper ||
                EditorDirector.instance == null || EditorDirector.instance.overUI() ||
                tileX < 0 || tileY < 0)
            {
                if (state == 3 || !IsPlacementSession()) activeSpecial = null;
                return;
            }
            if (special.Mapper == eMappers.MAPPER_MOAT)
            {
                // The original EditorDirector call already reaches the in-game moat
                // chore path. Unlike pitch, it must not be issued a second time here.
                if (state == 3) activeSpecial = null;
                return;
            }
            EngineInterface.PlaceMapperItem((int)special.Mapper, tileX, tileY, 0,
                special.Owner, false, false, state);
            if (specialInputDiagnostics++ < 24 || state == 3)
                log.LogInfo("SPECTATOR_EDITOR_SPECIAL_FORWARD owner=" + special.Owner +
                    " mapper=" + special.Mapper + " tile=" + tileX + "," + tileY +
                    " state=" + state);
            if (state == 3) activeSpecial = null;
        }

        private static bool BeforePlaceMapperItem(int item, int x, int y, int size,
            ref int player, ref bool inGameNotEditor, bool constructingOnly, int mouseState,
            out WallStoneScope __state)
        {
            __state = null;
            if (!IsPlacementSession()) return true;
            eMappers mapper = (eMappers)item;
            // The real editor already owns free placement, input state and visual registration.
            // Only route its building/unit owner; keep its complete native editor path.
            if (IsEditorToolsSession())
            {
                if (IsAllowedMapper(mapper) && CurrentAiOwner() > 0) player = CurrentAiOwner();
                return true;
            }
            if (!IsAllowedMapper(mapper)) return false;
            WallRequest wall = mouseState == 3 ? pendingWall : activeWall;
            SpecialDragRequest special = activeSpecial;
            int owner = IsWallMapper(mapper) && (mouseState == 2 || mouseState == 3) &&
                wall != null && wall.Mapper == mapper ? wall.Owner : CurrentAiOwner();
            if (IsSpecialDragMapper(mapper) && special != null &&
                special.Mapper == mapper && IsCurrentSession(special.SessionId))
                owner = special.Owner;
            if (owner < 1) return false;
            player = owner;
            // Moat drawing is a live-game dig order. Keep Vanilla's in-game flag;
            // editor placement semantics are only needed by the other mappers.
            inGameNotEditor = mapper == eMappers.MAPPER_MOAT;
            if (IsWallMapper(mapper))
            {
                if (!TryRaiseStone(owner, out __state)) return false;
                if (mouseState != 0 ? wallInputDiagnostics++ < 40 :
                    wallPreviewDiagnostics++ < 12)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_NATIVE state=" + mouseState +
                        " owner=" + owner + " mapper=" + mapper + " tile=" + x + "," + y +
                        " stone=" + __state.OriginalStone);
                if (mouseState == 1 && constructingOnly)
                {
                    activeWall = new WallRequest { Owner = owner, Mapper = mapper,
                        StartX = x, StartY = y, EndX = x, EndY = y,
                        SessionId = CurrentSessionId };
                    if (wallPressDiagnostics++ < 16)
                        log.LogInfo("SPECTATOR_EDITOR_WALL_PRESS owner=" + owner +
                            " mapper=" + mapper + " tile=" + x + "," + y);
                }
                return true;
            }
            if (IsSpecialDragMapper(mapper))
            {
                if (mouseState == 1 && constructingOnly)
                    activeSpecial = new SpecialDragRequest { Owner = owner, Mapper = mapper,
                        SessionId = CurrentSessionId };
                if (mapper == eMappers.MAPPER_MOAT && constructingOnly &&
                    (mouseState == 1 || mouseState == 2 || mouseState == 3))
                    TrackMoatTile(owner, x, y, mouseState);
                if (!constructingOnly && mapper == eMappers.MAPPER_PITCH_DITCH &&
                    (mouseState == 1 || mouseState == 2))
                {
                    PitchRequest pitch = null;
                    lock (pitchLock)
                    {
                        foreach (PitchRequest existing in pendingPitch)
                            if (PitchMatches(existing, owner, x, y))
                            {
                                pitch = existing;
                                break;
                            }
                        if (pitch == null)
                        {
                            pitch = new PitchRequest { Owner = owner, X = x, Y = y,
                                SessionId = CurrentSessionId,
                                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5) };
                            pendingPitch.Add(pitch);
                        }
                    }
                    pitchClickScope = pitch;
                    if (specialInputDiagnostics++ < 24)
                        log.LogInfo("SPECTATOR_EDITOR_PITCH_REQUEST owner=" + owner +
                            " tile=" + x + "," + y + " state=" + mouseState);
                }
                return true;
            }
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
            if (availabilityDetour == null || !availabilityDetour.IsInstalled)
            {
                if (buildingDiagnostics++ < 8)
                    log.LogWarning("SPECTATOR_EDITOR_BUILD_BLOCKED: audited cost-check hook unavailable");
                return false;
            }
            int scale = BuildingScales.GetScale(mapper);
            if (scale < 0) return false;
            var request = new BuildingRequest { Owner = owner, Mapper = mapper,
                X = x, Y = y, Scale = scale,
                SessionId = CurrentSessionId,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5) };
            buildingClickScope = request;
            pendingBuilding = request;
            log.LogInfo("SPECTATOR_EDITOR_BUILD_REQUEST owner=" + owner + " mapper=" + mapper +
                " cursor=" + x + "," + y + " origin=" + (x - scale / 2) + "," +
                (y - scale / 2));
            return true;
        }

        private static bool BeforeDeleteBuilding(int x, int y, ref int player,
            ref bool inGameNotEditor, int mouseState)
        {
            if (!humanPlacementMode || !IsHumanSingleplayerSession() ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 6)
                return true;
            int owner = CurrentAiOwner();
            if (owner < 1)
            {
                if (deleteDiagnostics++ < 16)
                    log.LogInfo("SPECTATOR_EDITOR_DELETE_BLOCKED tile=" + x + "," + y +
                        " state=" + mouseState + " owner=" + owner);
                return false;
            }
            player = owner;
            if (!IsEditorToolsSession()) inGameNotEditor = true;
            if (deleteDiagnostics++ < 32)
                log.LogInfo("SPECTATOR_EDITOR_DELETE_REQUEST session=" + CurrentSessionId +
                    " owner=" + owner + " tile=" + x + "," + y +
                    " state=" + mouseState);
            return true;
        }

        private static void AfterPlaceMapperItem()
        {
            placingUnitOwner = 0;
            pitchClickScope = null;
            BuildingRequest request = buildingClickScope;
            if (request != null)
            {
                request.ClickAccepted = true;
                buildingClickScope = null;
            }
        }

        private static Exception FinalizePlaceMapperItem(Exception __exception,
            WallStoneScope __state)
        {
            if (__state != null) RestoreStone(__state.Owner, __state.OriginalStone);
            if (__exception != null && pitchClickScope != null)
                lock (pitchLock) pendingPitch.Remove(pitchClickScope);
            pitchClickScope = null;
            placingUnitOwner = 0;
            if (__exception != null && buildingClickScope != null &&
                ReferenceEquals(pendingBuilding, buildingClickScope))
                pendingBuilding = null;
            buildingClickScope = null;
            return __exception;
        }

        private static bool IsWallMapper(eMappers mapper)
        {
            return mapper == eMappers.MAPPER_WALL ||
                mapper == eMappers.MAPPER_CRENAL ||
                mapper == eMappers.MAPPER_WOODWALL;
        }

        private static bool IsSpecialDragMapper(eMappers mapper) =>
            mapper == eMappers.MAPPER_PITCH_DITCH || mapper == eMappers.MAPPER_MOAT;

        private static unsafe bool TryRaiseStone(int owner, out WallStoneScope scope)
        {
            scope = null;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,
                out GamePlayerResources* resources) || resources == null) return false;
            scope = new WallStoneScope { Owner = owner,
                OriginalStone = resources->r_TotalGoodsStoneBlocks };
            if (resources->r_TotalGoodsStoneBlocks < 125)
                resources->r_TotalGoodsStoneBlocks = 125;
            return true;
        }

        private static unsafe void RestoreStone(int owner, uint original)
        {
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,
                out GamePlayerResources* resources) && resources != null)
                resources->r_TotalGoodsStoneBlocks = original;
        }

        private static bool WallMatches(WallRequest request, BuildWallEventArgs args)
        {
            return request != null && request.Mapper == args.WallType &&
                DateTime.UtcNow <= request.ExpiresAtUtc &&
                IsCurrentSession(request.SessionId) &&
                WallCoordinatesMatch(request, args);
        }

        private static bool WallCoordinatesMatch(WallRequest request, BuildWallEventArgs args)
        {
            return request != null && request.Mapper == args.WallType &&
                ((NearTile(request.StartX, request.StartY, args.TileXBegin, args.TileYBegin) &&
                  NearTile(request.EndX, request.EndY, args.TileXEnd, args.TileYEnd)) ||
                 (NearTile(request.StartX, request.StartY, args.TileXEnd, args.TileYEnd) &&
                  NearTile(request.EndX, request.EndY, args.TileXBegin, args.TileYBegin)));
        }

        private static bool NearTile(int x1, int y1, int x2, int y2) =>
            Math.Abs(x1 - x2) <= 1 && Math.Abs(y1 - y2) <= 1;

        private static bool PitchMatches(PitchRequest request, int owner, int x, int y) =>
            request != null && request.Owner == owner && request.X == x && request.Y == y &&
            IsCurrentSession(request.SessionId) && DateTime.UtcNow <= request.ExpiresAtUtc;

        private static bool PitchChoreMatches(PitchRequest request)
        {
            if (request == null || nativeImageBase == 0) return false;
            return Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FF4))) == 10 &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FEC))) == 0 &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C132C))) == request.X &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1330))) == request.Y &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1334))) ==
                    (int)eMappers.MAPPER_PITCH_DITCH;
        }

        private static unsafe void RestoreWallResources(WallBuildScope scope)
        {
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(scope.Request.Owner,
                out GamePlayerResources* resources) && resources != null)
            {
                resources->r_TotalGoodsStoneBlocks = scope.OriginalStone;
                resources->r_LastBoughtWallStoneCost = scope.OriginalCost;
            }
            if (scope.HasNativeOwnerResources && scope.NativeOwner != scope.Request.Owner &&
                GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(scope.NativeOwner,
                    out GamePlayerResources* nativeResources) && nativeResources != null)
            {
                nativeResources->r_TotalGoodsStoneBlocks = scope.NativeOwnerStone;
                nativeResources->r_LastBoughtWallStoneCost = scope.NativeOwnerCost;
            }
        }

        private static unsafe void OnBuildWall(BuildWallEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                WallRequest request = pendingWall;
                if (wallOrderDiagnostics++ < 40)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_EVENT phase=Pre owner=" + args.PlayerId +
                        " mapper=" + args.WallType + " start=" + args.TileXBegin + "," +
                        args.TileYBegin + " end=" + args.TileXEnd + "," + args.TileYEnd +
                        " pending=" + (request == null ? "none" : request.Owner + "/" +
                            request.Mapper + "/" + request.StartX + "," + request.StartY + "-" +
                            request.EndX + "," + request.EndY));
                int nativeOwner = args.PlayerId;
                if (!WallMatches(request, args) ||
                    (nativeOwner != request.Owner &&
                     nativeOwner != PlayerPerspectiveAPI.GetControlledPlayerId()) ||
                    !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(request.Owner,
                        out GamePlayerResources* resources) || resources == null) return;
                uint nativeStone = 0, nativeCost = 0;
                bool hasNativeResources = false;
                if (nativeOwner != request.Owner &&
                    GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(nativeOwner,
                        out GamePlayerResources* nativeResources) && nativeResources != null)
                {
                    nativeStone = nativeResources->r_TotalGoodsStoneBlocks;
                    nativeCost = nativeResources->r_LastBoughtWallStoneCost;
                    hasNativeResources = true;
                }
                pendingWall = null;
                buildingWall = new WallBuildScope { Request = request,
                    OriginalStone = resources->r_TotalGoodsStoneBlocks,
                    OriginalCost = resources->r_LastBoughtWallStoneCost,
                    NativeOwner = nativeOwner, NativeOwnerStone = nativeStone,
                    NativeOwnerCost = nativeCost,
                    HasNativeOwnerResources = hasNativeResources };
                args.PlayerId = request.Owner;
                log.LogInfo("SPECTATOR_EDITOR_WALL_MATCH session=" + request.SessionId +
                    " owner=" + request.Owner + " nativeOwner=" + nativeOwner +
                    " mapper=" + request.Mapper + " stone=" + resources->r_TotalGoodsStoneBlocks);
                if (resources->r_TotalGoodsStoneBlocks < 125)
                    resources->r_TotalGoodsStoneBlocks = 125;
                return;
            }
            WallBuildScope scope = buildingWall;
            if (scope == null || !WallCoordinatesMatch(scope.Request, args)) return;
            RestoreWallResources(scope);
            buildingWall = null;
            log.LogInfo("SPECTATOR_EDITOR_WALL_BUILT owner=" + args.PlayerId +
                " mapper=" + args.WallType + " start=" + args.TileXBegin + "," +
                args.TileYBegin + " end=" + args.TileXEnd + "," + args.TileYEnd +
                " stone=" + scope.OriginalStone + " skippedDeductions=" +
                scope.Subtractions + " remainder=" + scope.OriginalCost);
        }

        private static void OnSubtractResource(PlayerSubtractResourceEventArgs args)
        {
            if (args.Good == eGoods.STORED_PITCH_RAW)
            {
                lock (pitchLock)
                    foreach (PitchRequest pitch in pendingPitch)
                        if (PitchMatches(pitch, args.PlayerId, pitch.X, pitch.Y) &&
                            PitchChoreMatches(pitch))
                        {
                            args.SkipOriginalFunction = true;
                            if (pitchCostDiagnostics++ < 24)
                                log.LogInfo("SPECTATOR_EDITOR_PITCH_COST_SKIPPED owner=" +
                                    args.PlayerId + " tile=" + pitch.X + "," + pitch.Y +
                                    " amount=" + args.Amount);
                            return;
                        }
            }
            WallBuildScope scope = buildingWall;
            if (scope == null || args.PlayerId != scope.Request.Owner ||
                args.Good != eGoods.STORED_STONE_BLOCKS) return;
            args.SkipOriginalFunction = true;
            scope.Subtractions++;
            if (wallCostDiagnostics++ < 40)
                log.LogInfo("SPECTATOR_EDITOR_WALL_COST_SKIPPED owner=" + args.PlayerId +
                    " good=" + args.Good + " amount=" + args.Amount);
        }

        private static void OnBuildPitchDitch(BuildPitchDitchEventArgs args)
        {
            lock (pitchLock)
                foreach (PitchRequest pitch in pendingPitch)
                    if (PitchMatches(pitch, args.PlayerId, args.TileX, args.TileY) &&
                        PitchChoreMatches(pitch))
                    {
                        if (args.Phase == EventHookPhase.Post) pitch.Built = args.ReturnValue > 0;
                        if (pitchCostDiagnostics++ < 24)
                            log.LogInfo("SPECTATOR_EDITOR_PITCH_BUILD phase=" + args.Phase +
                                " owner=" + args.PlayerId + " tile=" + args.TileX + "," +
                                args.TileY + " result=" + args.ReturnValue);
                        return;
                    }
        }

        private static unsafe Exception FinalizeBuildWall(Exception __exception)
        {
            WallBuildScope scope = buildingWall;
            if (scope == null) return __exception;
            RestoreWallResources(scope);
            buildingWall = null;
            log.LogWarning("SPECTATOR_EDITOR_WALL_SCOPE_RECOVERED owner=" +
                scope.Request.Owner + " error=" + __exception);
            return __exception;
        }

        private static unsafe void TrackMoatTile(int owner, int x, int y, int mouseState)
        {
            if (x < 0 || x >= 800 || y < 0 || y >= 800) return;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tiles == null) return;
            int tileId = tiles.GetTileId(x, y);
            if (!tiles.IsValidTileId(tileId)) return;
            lock (moatLock)
            {
                if (moatInputDiagnostics >= 32) return;
                foreach (MoatProbe probe in pendingMoat)
                    if (probe.Owner == owner && probe.X == x && probe.Y == y &&
                        IsCurrentSession(probe.SessionId)) return;
                var probeNew = new MoatProbe {
                    Owner = owner, X = x, Y = y, SessionId = CurrentSessionId,
                    BeforeIndex = tiles.GetMoatWorkTaskIndexAtTile(x, y),
                    BeforeFlags = (uint)tiles.GetTilePropertyFlag(tileId),
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(3)
                };
                pendingMoat.Add(probeNew);
                moatInputDiagnostics++;
                log.LogInfo("SPECTATOR_EDITOR_MOAT_INPUT session=" + probeNew.SessionId +
                    " owner=" + owner + " tile=" + x + "," + y +
                    " state=" + mouseState + " priorTask=" + probeNew.BeforeIndex +
                    " priorFlags=0x" + probeNew.BeforeFlags.ToString("X8") +
                    " activeTasks=" + tiles.GetMoatWorkTaskActiveCount());
            }
        }

        private static unsafe void CheckMoatProbes(int tick)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tiles == null) return;
            lock (moatLock)
            {
                for (int index = pendingMoat.Count - 1; index >= 0; index--)
                {
                    MoatProbe probe = pendingMoat[index];
                    if (!IsCurrentSession(probe.SessionId))
                    {
                        pendingMoat.RemoveAt(index);
                        continue;
                    }
                    int tileId = tiles.GetTileId(probe.X, probe.Y);
                    if (!tiles.IsValidTileId(tileId))
                    {
                        pendingMoat.RemoveAt(index);
                        continue;
                    }
                    int taskIndex = tiles.GetMoatWorkTaskIndexAtTile(probe.X, probe.Y);
                    uint flags = (uint)tiles.GetTilePropertyFlag(tileId);
                    int taskOwner = 0;
                    Span<MoatWorkTask> tasks = tiles.GetMoatWorkTasks();
                    if (taskIndex > 0 && taskIndex < tasks.Length &&
                        tasks[taskIndex].r_TileId == tileId)
                        taskOwner = tasks[taskIndex].r_OwnerPlayerId;
                    probe.Samples++;
                    bool completed = taskIndex != probe.BeforeIndex ||
                        ((flags ^ probe.BeforeFlags) & (uint)TilePropertyFlag.PlannedMoat) != 0;
                    if (!completed && probe.Samples < 5 &&
                        DateTime.UtcNow < probe.ExpiresAtUtc) continue;
                    if (moatOutcomeDiagnostics++ < 32)
                        log.LogInfo("SPECTATOR_EDITOR_MOAT_OUTCOME tick=" + tick +
                            " session=" + probe.SessionId + " owner=" + probe.Owner +
                            " tile=" + probe.X + "," + probe.Y +
                            " task=" + taskIndex + " taskOwner=" + taskOwner +
                            " flags=0x" + flags.ToString("X8") +
                            " activeTasks=" + tiles.GetMoatWorkTaskActiveCount() +
                            " samples=" + probe.Samples);
                    pendingMoat.RemoveAt(index);
                    if (completed && taskOwner == probe.Owner &&
                        humanPlacementMode && IsHumanSingleplayerSession())
                        tiles.RefreshTileAreaVisuals(probe.X, probe.Y);
                }
            }
        }

        private static void OnSimulationTick(int tick)
        {
            try { CheckMoatProbes(tick); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_MOAT_DIAGNOSTIC_ERROR: " + error); }
            lock (visualSamplesLock)
            {
                for (int index = visualSamples.Count - 1; index >= 0; index--)
                {
                    BuildingVisualSample sample = visualSamples[index];
                    if (tick < sample.NextTick) continue;
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_VISUAL_LATER tick=" + tick +
                        " source=" + sample.Source + " buildingId=" + sample.BuildingId +
                        " " + DescribeBuildingVisual(sample.BuildingId, sample.X, sample.Y));
                    sample.Samples++;
                    if (sample.Samples >= 3) visualSamples.RemoveAt(index);
                    else sample.NextTick = tick + (sample.Samples == 1 ? 4 : 16);
                }
            }
            BuildingRequest request = pendingBuilding;
            if (request != null && DateTime.UtcNow > request.ExpiresAtUtc)
            {
                pendingBuilding = null;
                if (buildingDiagnostics++ < 20)
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_EXPIRED tick=" + tick +
                        " owner=" + request.Owner + " mapper=" + request.Mapper +
                        " spawned=" + request.Spawned);
            }
            WallRequest wall = pendingWall;
            if (wall != null && DateTime.UtcNow > wall.ExpiresAtUtc)
            {
                pendingWall = null;
                if (wallOrderDiagnostics++ < 40)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_EXPIRED tick=" + tick +
                        " owner=" + wall.Owner + " mapper=" + wall.Mapper);
            }
            lock (pitchLock)
                pendingPitch.RemoveAll(pitch => DateTime.UtcNow > pitch.ExpiresAtUtc ||
                    !IsCurrentSession(pitch.SessionId));
        }

        private static void OnBuildStructure(BuildStructureEventArgs args)
        {
            BuildingRequest request = pendingBuilding;
            if (request == null || !request.ClickAccepted ||
                DateTime.UtcNow > request.ExpiresAtUtc ||
                args.PlayerId != request.Owner || args.Mappers != request.Mapper ||
                args.BuildingScaleUnknown != request.Scale ||
                args.TileX != request.X - request.Scale / 2 ||
                args.TileY != request.Y - request.Scale / 2 ||
                !IsCurrentSession(request.SessionId)) return;
            if (args.Phase == EventHookPhase.Pre)
            {
                request.BuildStarted = true;
                args.IsFree = true;
                if (buildingDiagnostics++ < 24)
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_NATIVE owner=" + args.PlayerId +
                        " mapper=" + args.Mappers + " tile=" + args.TileX + "," +
                        args.TileY + " scale=" + args.BuildingScaleUnknown +
                        " variant=" + args.Unknown1 + " free=True");
            }
            else if (request.BuildStarted)
            {
                pendingBuilding = null;
                if (buildingDiagnostics++ < 24)
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_FINISHED owner=" + args.PlayerId +
                        " mapper=" + args.Mappers + " spawned=" + request.Spawned);
            }
        }

        private static unsafe void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Building != eStructs.STRUCT_WELL && pendingBuilding == null) return;
            BuildingRequest request = pendingBuilding;
            bool ours = request != null && request.BuildStarted &&
                request.Owner == args.PlayerId &&
                request.X - request.Scale / 2 == args.TileX &&
                request.Y - request.Scale / 2 == args.TileY;
            if (!ours && (args.Building != eStructs.STRUCT_WELL ||
                vanillaWellDiagnostics++ >= 8)) return;
            if (args.Phase == EventHookPhase.Post && ours)
                request.Spawned = args.ReturnValue > 0;
            log.LogInfo("SPECTATOR_EDITOR_BUILD_VISUAL source=" + (ours ? "spectator" : "vanilla") +
                " phase=" + args.Phase + " owner=" + args.PlayerId +
                " building=" + args.Building + " tile=" + args.TileX + "," + args.TileY +
                " buildingId=" + args.ReturnValue + " visualOwner=" + args.VisualPlayerId +
                " variation=" + args.SpriteVariationIndex + " " +
                DescribeBuildingVisual((int)args.ReturnValue, args.TileX, args.TileY));
            if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0 &&
                args.Building == eStructs.STRUCT_WELL)
                lock (visualSamplesLock)
                    visualSamples.Add(new BuildingVisualSample {
                        BuildingId = (int)args.ReturnValue, X = args.TileX, Y = args.TileY,
                        Source = ours ? "spectator" : "vanilla"
                    });
        }

        private static unsafe string DescribeBuildingVisual(int buildingId, int x, int y)
        {
            try
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                var gfx = GameTileManagerAPI.Instance.GetGfxLayer();
                var alpha = GameTileManagerAPI.Instance.GetAlphaGfxLayer();
                var structure = GameTileManagerAPI.Instance.GetStructureLayer();
                string tiles = tileId >= 0 && tileId < gfx.Length &&
                    tileId < alpha.Length && tileId < structure.Length ?
                    "gfx=" + gfx[tileId] + " alpha=" + alpha[tileId] +
                    " structure=" + structure[tileId] : "tile=invalid";
                if (buildingId > 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* building) && building != null)
                    return tiles + " record=" + building->r_BuildingType + "/" +
                        building->r_SpriteVariationIndex + "/" +
                        building->r_SpritePlayerColorId + "/" +
                        building->r_TilePositionXBegin + "," + building->r_TilePositionYBegin;
                return tiles + " record=none";
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_VISUAL_DIAGNOSTIC_ERROR: " + error);
                return "visual=unavailable";
            }
        }

        private static unsafe void OnTribeAssigned(TribeAssignUnitEventArgs args)
        {
            int owner = placingUnitOwner;
            if (owner <= 0 || unitDiagnostics >= 32) return;
            try
            {
                if (!APIShared.UnitAccess.TryGetById(args.UnitId, out GameUnit* unit, out _) ||
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
            if (IsUnitMapper(mapper) || IsWallMapper(mapper)) return true;
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

    }
}
