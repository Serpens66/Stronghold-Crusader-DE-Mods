using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace ForeignTroopHudTest
{
    internal sealed class ForeignTroopEntry
    {
        internal int Owner;
        internal int Type;
        internal int ColorId;
        internal int Count;
        internal ulong CurrentHealth;
        internal ulong MaxHealth;
    }

    internal static class ForeignTroopHudRuntime
    {
        private static readonly Dictionary<int, ForeignTroopEntry> grouped = new Dictionary<int, ForeignTroopEntry>();
        private static readonly List<ForeignTroopEntry> entries = new List<ForeignTroopEntry>();
        private static readonly List<ForeignTroopEntry> entryPool = new List<ForeignTroopEntry>();
        private static ManualLogSource log;
        private static ForeignTroopHudView view;
        private static IMissionLifecycleCapability lifecycle;
        private static MissionContext activeSession;
        private static bool initialized;
        private static bool layoutValidated;
        private static bool readyLogged;
        private static bool lordIconWarningLogged;
        private static string lastExceptionKey;
        private static string lastHideExceptionKey;
        private static bool resetPending;
        private static int lastFrame = -1;
        private static float nextRefreshAt;
        private static EngineInterface.PlayState stateBeforeReady;
        private static MainViewModel ownedTroopHud;
        private static string lastDiagnosticKey;
        private static float lastDiagnosticAt = -100f;
        private static string lastSpectatorSelectionKey;
        private static string lastCameraErrorKey;
        private static MarkedUnitSelectionSnapshot markedSnapshot;
        private static int selectionDirty;

        internal static void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            log = logger;
            view = new ForeignTroopHudView();
            view.CenterOnGroup = CenterOnGroup;
            if (!ApiShared.Current.TryGetMissionLifecycle(ForeignTroopHudPlugin.Guid, out lifecycle, out NativeCapabilityDiagnostic diagnostic))
                throw new InvalidOperationException("APIShared mission lifecycle unavailable: " + diagnostic?.Reason);
            if (!lifecycle.TryRegisterObserver("ForeignTroopHudTest.Runtime", OnSessionStarted, OnSessionEnded, null, out diagnostic))
                throw new InvalidOperationException("APIShared mission observer registration failed: " + diagnostic?.Reason);
            if (!MarkedUnitSelectionAPI.TryRegisterObserver(ForeignTroopHudPlugin.Guid, OnMarkedSelectionChanged))
                throw new InvalidOperationException("APIShared marked-unit selection observer registration failed.");
            Application.onBeforeRender += OnBeforeRender;
            initialized = true;
            log.LogInfo("FOREIGN_TROOP_HUD_INITIALIZED: static render publisher and APIShared mission events registered.");
        }

        private static void OnSessionStarted(MissionLifecycleNotification notification)
        {
            activeSession = notification.Context;
            Volatile.Write(ref markedSnapshot, null);
            Interlocked.Exchange(ref selectionDirty, 1);
            stateBeforeReady = GameData.Instance?.lastGameState;
            resetPending = true;
            nextRefreshAt = 0f;
            lastDiagnosticKey = null;
            lastExceptionKey = null;
            lastHideExceptionKey = null;
            lordIconWarningLogged = false;
            lastSpectatorSelectionKey = null;
            lastCameraErrorKey = null;
            log.LogInfo("FOREIGN_TROOP_HUD_SESSION_READY: session=" + activeSession.SessionId + ", mode=" + activeSession.Mode.Kind);
        }

        private static void OnSessionEnded(MissionLifecycleNotification notification)
        {
            HideForeignHud();
            activeSession = null;
            Volatile.Write(ref markedSnapshot, null);
            Interlocked.Exchange(ref selectionDirty, 0);
            stateBeforeReady = null;
            resetPending = true;
            nextRefreshAt = 0f;
            lastDiagnosticKey = null;
            lastExceptionKey = null;
            lastHideExceptionKey = null;
            lordIconWarningLogged = false;
            lastSpectatorSelectionKey = null;
            lastCameraErrorKey = null;
        }

        private static void OnMarkedSelectionChanged(MarkedUnitSelectionSnapshot selection)
        {
            // This may run inside EngineInterface.run. No Noesis or HUD work belongs here.
            Volatile.Write(ref markedSnapshot, selection);
            Interlocked.Exchange(ref selectionDirty, 1);
        }

        private static void OnBeforeRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            float now = Time.realtimeSinceStartup;
            bool changed = Interlocked.Exchange(ref selectionDirty, 0) != 0;
            if (!resetPending && !changed && now < nextRefreshAt) return;
            nextRefreshAt = now + (activeSession == null ? 5f : 0.1f);
            try
            {
                if (resetPending)
                {
                    HideForeignHud();
                    grouped.Clear();
                    entries.Clear();
                    entryPool.Clear();
                    view.ResetForMap();
                    resetPending = false;
                }
                Refresh();
            }
            catch (Exception error)
            {
                string key = error.GetType().FullName + ":" + error.Message;
                try { HideForeignHud(); }
                catch (Exception hideError)
                {
                    string hideKey = "hide:" + hideError.GetType().FullName + ":" + hideError.Message;
                    if (hideKey != lastHideExceptionKey) log.LogError("FOREIGN_TROOP_HUD_HIDE_ERROR: " + hideError);
                    lastHideExceptionKey = hideKey;
                }
                ReportStatus("exception:" + key, GameData.Instance?.lastGameState, MainViewModel.Instance);
                if (key == lastExceptionKey) return;
                lastExceptionKey = key;
                log.LogError("FOREIGN_TROOP_HUD_ERROR: " + error);
            }
        }

        private static void Refresh()
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            MissionContext session = activeSession;
            string readiness = session == null ? "waiting-session" :
                state == null ? "waiting-play-state" :
                stateBeforeReady != null && ReferenceEquals(state, stateBeforeReady) ? "waiting-new-play-state" :
                !MainViewModel.viewModelLoaded ? "waiting-main-view-model" : null;
            if (readiness != null)
            {
                HideForeignHud();
                ReportStatus(readiness, state, MainViewModel.Instance);
                return;
            }
            MainViewModel main = MainViewModel.Instance;
            bool vanillaTroopSelection = state.spectatorMode == 0 && state.app_mode == 14 &&
                (state.app_sub_mode == 61 || state.app_sub_mode == 62);
            string hudBlocker = main == null ? "missing-MainViewModel.Instance" :
                main.HUDmain == null ? "missing-HUDmain" :
                main.HUDTroopPanel == null ? "missing-HUDTroopPanel" :
                vanillaTroopSelection ? "vanilla-troop-selection" :
                main.Show_HUD_Troops && !ReferenceEquals(main, ownedTroopHud) ? "vanilla-troop-hud-owned-elsewhere" :
                main.Show_HUD_Building ? "vanilla-building-hud" :
                main.Show_HUD_Briefing ? "vanilla-briefing-hud" :
                main.Show_HUD_MissionOver ? "vanilla-mission-over-hud" :
                !main.Show_InGameUI ? "in-game-ui-hidden" : null;
            if (hudBlocker != null)
            {
                HideForeignHud(vanillaTroopSelection);
                ReportStatus(hudBlocker, state, main);
                return;
            }
            if (!readyLogged)
            {
                readyLogged = true;
                log.LogInfo("FOREIGN_TROOP_HUD_RUNTIME_ALIVE: static publisher ran after startup cleanup and map load.");
            }
            if (!layoutValidated)
            {
                ValidateInteropLayout();
                layoutValidated = true;
            }
            bool spectator = state.spectatorMode != 0;
            bool editor = session.IsEditor || session.Mode.IsMapEditor;
            int ownPlayerId = spectator ? 0 : editor
                ? EditorDirector.instance?.ActivePlayerID ?? 0
                : PlayerPerspectiveAPI.GetControlledPlayerId();
            if (ownPlayerId < 1 || ownPlayerId > 8) ownPlayerId = 0;
            MarkedUnitSelectionSnapshot selection = Volatile.Read(ref markedSnapshot);
            if (selection == null || selection.SessionId != session.SessionId)
            {
                if (!MarkedUnitSelectionAPI.TryCapture(session.SessionId, out selection))
                {
                    HideForeignHud();
                    ReportStatus("waiting-marked-selection", state, main, ownPlayerId);
                    return;
                }
                Volatile.Write(ref markedSnapshot, selection);
            }
            grouped.Clear();
            entries.Clear();
            int usedEntries = 0;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            bool invalidCachedId = false;
            for (int index = 0; index < selection.Count; index++)
            {
                int id = selection[index];
                if (id < 1 || id > units.Length || units[id - 1].r_UnitHover == 0)
                { invalidCachedId = true; break; }
            }
            if (invalidCachedId)
            {
                MarkedUnitSelectionAPI.RequestResync(session.SessionId);
                if (!MarkedUnitSelectionAPI.TryCapture(session.SessionId, out selection)) return;
                Volatile.Write(ref markedSnapshot, selection);
            }
            int hoveredCount = 0;
            int foreignCount = 0;
            ulong spectatorSignature = 1469598103934665603UL;
            for (int index = 0; index < selection.Count; index++)
            {
                int spanIndex = selection[index] - 1;
                if (spanIndex < 0 || spanIndex >= units.Length) continue;
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitHover == 0) continue;
                hoveredCount++;
                int owner = unit.r_ControllableForPlayerId;
                if (spectator)
                {
                    unchecked
                    {
                        spectatorSignature = (spectatorSignature ^ (uint)(spanIndex + 1)) * 1099511628211UL;
                        spectatorSignature = (spectatorSignature ^ (uint)owner) * 1099511628211UL;
                        spectatorSignature = (spectatorSignature ^ unit.r_SpritePlayerColorId) * 1099511628211UL;
                        spectatorSignature = (spectatorSignature ^ unit.r_SpawnedForPlayerIndex) * 1099511628211UL;
                    }
                }
                if (owner == ownPlayerId && ownPlayerId != 0 || owner > 8) continue;
                int type = (int)unit.r_UnitChimp;
                if (type <= 0 || type >= 89) continue;
                foreignCount++;
                int key = (owner << 8) | type;
                ForeignTroopEntry entry;
                if (!grouped.TryGetValue(key, out entry))
                {
                    if (usedEntries == entryPool.Count) entryPool.Add(new ForeignTroopEntry());
                    entry = entryPool[usedEntries++];
                    entry.Owner = owner;
                    entry.Type = type;
                    entry.ColorId = (int)unit.r_SpritePlayerColorId;
                    entry.Count = 0;
                    entry.CurrentHealth = 0;
                    entry.MaxHealth = 0;
                    grouped.Add(key, entry);
                    entries.Add(entry);
                }
                entry.Count++;
                entry.CurrentHealth += unit.r_CurrentHealth;
                entry.MaxHealth += unit.r_MaxHealth;
            }
            if (spectator)
            {
                int perspectivePlayer = PlayerPerspectiveAPI.GetViewedPlayerId();
                int editorPlayer = EditorDirector.instance?.ActivePlayerID ?? 0;
                string selectionKey = activeSession.SessionId + ":" + perspectivePlayer + ":" + editorPlayer +
                    ":" + hoveredCount + ":" + spectatorSignature;
                if (selectionKey != lastSpectatorSelectionKey)
                {
                    lastSpectatorSelectionKey = selectionKey;
                    log.LogInfo("FOREIGN_TROOP_HUD_SPECTATOR_SELECTION: perspectivePlayer=" + perspectivePlayer +
                        ", editorPlayer=" + editorPlayer + ", selectedUnits=" + hoveredCount +
                        ", groups=" + entries.Count + ", samples=" + BuildSpectatorSamples(units, selection));
                }
            }
            else lastSpectatorSelectionKey = null;
            if (entries.Count == 0)
            {
                HideForeignHud();
                ReportStatus("no-foreign-marked-units", state, main, ownPlayerId, hoveredCount, foreignCount, 0);
                return;
            }
            entries.Sort((a, b) => a.Owner != b.Owner ? a.Owner.CompareTo(b.Owner) : a.Type.CompareTo(b.Type));
            string missingElement;
            if (!view.ActivateVanilla(main, out missingElement))
            {
                HideForeignHud();
                ReportStatus("missing-vanilla-element:" + missingElement, state, main, ownPlayerId, hoveredCount, foreignCount, entries.Count);
                return;
            }
            if (!view.Show(main, entries, out missingElement))
            {
                HideForeignHud();
                ReportStatus("missing-mod-element:" + missingElement, state, main, ownPlayerId, hoveredCount, foreignCount, entries.Count);
                return;
            }
            if (view.LordIconMissing && !lordIconWarningLogged)
            {
                lordIconWarningLogged = true;
                log.LogWarning("FOREIGN_TROOP_HUD_RESOURCE_MISSING: BugfixesAndQoL-LordIcon; Lord entry uses its type label.");
            }
            if (!main.Show_HUD_Troops)
            {
                ownedTroopHud = main;
                main.Show_HUD_Troops = true;
            }
            ReportStatus("shown", state, main, ownPlayerId, hoveredCount, foreignCount, entries.Count);
        }

        private static StringBuilder BuildSpectatorSamples(Span<GameUnit> units, MarkedUnitSelectionSnapshot selection)
        {
            var samples = new StringBuilder(320);
            int count = 0;
            for (int index = 0; index < selection.Count && count < 12; index++)
            {
                int spanIndex = selection[index] - 1;
                if (spanIndex < 0 || spanIndex >= units.Length) continue;
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitHover == 0) continue;
                count++;
                samples.Append(" [unit=").Append(spanIndex + 1)
                    .Append(",type=").Append((int)unit.r_UnitChimp)
                    .Append(",owner=").Append(unit.r_ControllableForPlayerId)
                    .Append(",color=").Append(unit.r_SpritePlayerColorId)
                    .Append(",spawnedFor=").Append(unit.r_SpawnedForPlayerIndex).Append(']');
            }
            return samples;
        }

        private static void CenterOnGroup(int owner, int type)
        {
            try
            {
                MissionContext session = activeSession;
                EngineInterface.PlayState state = GameData.Instance?.lastGameState;
                MainViewModel main = MainViewModel.Instance;
                if (session == null || state == null || main == null ||
                    !ReferenceEquals(main, ownedTroopHud) || !main.Show_HUD_Troops)
                    return;
                bool spectator = state.spectatorMode != 0;
                bool editor = session.IsEditor || session.Mode.IsMapEditor;
                int ownPlayerId = spectator ? 0 : editor
                    ? EditorDirector.instance?.ActivePlayerID ?? 0
                    : PlayerPerspectiveAPI.GetControlledPlayerId();
                if (!spectator && owner == ownPlayerId && ownPlayerId != 0) return;
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
                if (unitApi == null || playerApi == null) return;
                MarkedUnitSelectionSnapshot selection = Volatile.Read(ref markedSnapshot);
                if (selection == null || selection.SessionId != session.SessionId) return;
                Span<GameUnit> units = unitApi.GetUnitsAsSpan();
                for (int index = 0; index < selection.Count; index++)
                {
                    int spanIndex = selection[index] - 1;
                    if (spanIndex < 0 || spanIndex >= units.Length) continue;
                    ref GameUnit unit = ref units[spanIndex];
                    if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitHover == 0 ||
                        unit.r_ControllableForPlayerId != owner || (int)unit.r_UnitChimp != type)
                        continue;
                    playerApi.SetScreenCenterToUnit(spanIndex + 1);
                    return;
                }
            }
            catch (Exception error)
            {
                string key = error.GetType().FullName + ":" + error.Message;
                if (key == lastCameraErrorKey) return;
                lastCameraErrorKey = key;
                log.LogError("FOREIGN_TROOP_HUD_CAMERA_ERROR: " + error);
            }
        }

        private static void ReportStatus(string reason, EngineInterface.PlayState state, MainViewModel main,
            int activePlayer = 0, int hovered = 0, int foreign = 0, int groups = 0)
        {
            bool hidden = reason != "shown";
            string key = reason + ":" + activeSession?.SessionId + ":" + activeSession?.Mode.Kind +
                ":" + state?.app_mode + ":" + state?.app_sub_mode + ":" + state?.spectatorMode +
                ":" + main?.Show_HUD_Troops + ":" + main?.Show_HUD_Main + ":" + main?.Show_InGameUI +
                ":" + activePlayer + ":" + hovered + ":" + foreign + ":" + groups;
            float now = Time.realtimeSinceStartup;
            if (key == lastDiagnosticKey && (!hidden || now - lastDiagnosticAt < 5f)) return;
            lastDiagnosticKey = key;
            lastDiagnosticAt = now;
            log.LogInfo("FOREIGN_TROOP_HUD_DIAGNOSTIC: reason=" + reason +
                ", session=" + (activeSession == null ? "none" : activeSession.SessionId.ToString()) +
                ", mode=" + (activeSession == null ? "none" : activeSession.Mode.Kind.ToString()) +
                ", activePlayer=" + activePlayer +
                ", appMode=" + (state == null ? "none" : state.app_mode.ToString()) +
                ", appSubMode=" + (state == null ? "none" : state.app_sub_mode.ToString()) +
                ", spectator=" + (state == null ? "none" : state.spectatorMode.ToString()) +
                ", hovered=" + hovered + ", foreign=" + foreign + ", groups=" + groups +
                ", showTroops=" + (main == null ? "none" : main.Show_HUD_Troops.ToString()) +
                ", showMain=" + (main == null ? "none" : main.Show_HUD_Main.ToString()) +
                ", showInGameUI=" + (main == null ? "none" : main.Show_InGameUI.ToString()));
        }

        private static void HideForeignHud(bool vanillaTroopTakeover = false)
        {
            view?.Hide();
            MainViewModel main = ownedTroopHud;
            ownedTroopHud = null;
            if (!vanillaTroopTakeover && main != null &&
                ReferenceEquals(main, MainViewModel.Instance) && main.Show_HUD_Troops)
                main.Show_HUD_Troops = false;
        }

        private static void ValidateInteropLayout()
        {
            if (Marshal.SizeOf<GameUnit>() != 0x490 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpritePlayerColorId)).ToInt32() != 0x0C ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpawnedForPlayerIndex)).ToInt32() != 0x7C ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitHover)).ToInt32() != 0x30 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_MaxHealth)).ToInt32() != 0x3C8)
                throw new InvalidOperationException("Installed GameUnit interop layout differs from the audited native selection layout.");
        }
    }
}
