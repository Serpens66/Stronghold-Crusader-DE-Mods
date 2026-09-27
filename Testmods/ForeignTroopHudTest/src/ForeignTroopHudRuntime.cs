using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        private static ManualLogSource log;
        private static ForeignTroopHudView view;
        private static IMissionLifecycleCapability lifecycle;
        private static MissionContext activeSession;
        private static bool initialized;
        private static bool layoutValidated;
        private static bool readyLogged;
        private static bool failureLogged;
        private static bool resetPending;
        private static int lastFrame = -1;
        private static EngineInterface.PlayState stateBeforeReady;
        private static MainViewModel ownedTroopHud;

        internal static void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            log = logger;
            view = new ForeignTroopHudView();
            if (!ApiShared.Current.TryGetMissionLifecycle(ForeignTroopHudPlugin.Guid, out lifecycle, out NativeCapabilityDiagnostic diagnostic))
                throw new InvalidOperationException("APIShared mission lifecycle unavailable: " + diagnostic?.Reason);
            if (!lifecycle.TryRegisterObserver("ForeignTroopHudTest.Runtime", OnSessionStarted, OnSessionEnded, null, out diagnostic))
                throw new InvalidOperationException("APIShared mission observer registration failed: " + diagnostic?.Reason);
            Application.onBeforeRender += OnBeforeRender;
            initialized = true;
            log.LogInfo("FOREIGN_TROOP_HUD_INITIALIZED: static render publisher and APIShared mission events registered.");
        }

        private static void OnSessionStarted(MissionLifecycleNotification notification)
        {
            activeSession = notification.Context;
            stateBeforeReady = GameData.Instance?.lastGameState;
            resetPending = true;
            log.LogInfo("FOREIGN_TROOP_HUD_SESSION_READY: session=" + activeSession.SessionId + ", mode=" + activeSession.Mode.Kind);
        }

        private static void OnSessionEnded(MissionLifecycleNotification notification)
        {
            HideForeignHud();
            activeSession = null;
            stateBeforeReady = null;
            resetPending = true;
        }

        private static void OnBeforeRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (resetPending)
                {
                    HideForeignHud();
                    grouped.Clear();
                    entries.Clear();
                    view.ResetForMap();
                    resetPending = false;
                }
                Refresh();
            }
            catch (Exception error)
            {
                HideForeignHud();
                if (failureLogged) return;
                failureLogged = true;
                log.LogError("FOREIGN_TROOP_HUD_ERROR: " + error);
            }
        }

        private static void Refresh()
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            MissionContext session = activeSession;
            if (session == null || state == null ||
                (stateBeforeReady != null && ReferenceEquals(state, stateBeforeReady)) ||
                !MainViewModel.viewModelLoaded)
            {
                HideForeignHud();
                return;
            }
            MainViewModel main = MainViewModel.Instance;
            bool vanillaTroopSelection = state.spectatorMode == 0 && state.app_mode == 14 &&
                (state.app_sub_mode == 61 || state.app_sub_mode == 62);
            if (main == null || main.HUDmain == null || main.HUDTroopPanel == null ||
                vanillaTroopSelection || (main.Show_HUD_Troops && !ReferenceEquals(main, ownedTroopHud)) ||
                main.Show_HUD_Building || main.Show_HUD_Briefing ||
                main.Show_HUD_MissionOver || !main.Show_InGameUI)
            {
                HideForeignHud(vanillaTroopSelection);
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
                : GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
            if (ownPlayerId < 1 || ownPlayerId > 8) ownPlayerId = 0;
            grouped.Clear();
            entries.Clear();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitHover == 0) continue;
                int owner = unit.r_ControllableForPlayerId;
                if (owner == ownPlayerId && ownPlayerId != 0 || owner > 8) continue;
                int type = (int)unit.r_UnitChimp;
                if (type <= 0 || type >= 89) continue;
                int key = (owner << 8) | type;
                ForeignTroopEntry entry;
                if (!grouped.TryGetValue(key, out entry))
                {
                    entry = new ForeignTroopEntry { Owner = owner, Type = type, ColorId = (int)unit.r_SpritePlayerColorId };
                    grouped.Add(key, entry);
                    entries.Add(entry);
                }
                entry.Count++;
                entry.CurrentHealth += unit.r_CurrentHealth;
                entry.MaxHealth += unit.r_MaxHealth;
            }
            if (entries.Count == 0)
            {
                HideForeignHud();
                return;
            }
            entries.Sort((a, b) => a.Owner != b.Owner ? a.Owner.CompareTo(b.Owner) : a.Type.CompareTo(b.Type));
            if (!view.ActivateVanilla(main) || !view.Show(entries))
            {
                HideForeignHud();
                return;
            }
            if (!main.Show_HUD_Troops)
            {
                ownedTroopHud = main;
                main.Show_HUD_Troops = true;
            }
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
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitHover)).ToInt32() != 0x30 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_MaxHealth)).ToInt32() != 0x3C8)
                throw new InvalidOperationException("Installed GameUnit interop layout differs from the audited native selection layout.");
        }
    }
}
