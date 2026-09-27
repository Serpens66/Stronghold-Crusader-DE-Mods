using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
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
    }

    internal static class ForeignTroopHudRuntime
    {
        private static readonly Dictionary<int, ForeignTroopEntry> grouped = new Dictionary<int, ForeignTroopEntry>();
        private static readonly List<ForeignTroopEntry> entries = new List<ForeignTroopEntry>();
        private static ManualLogSource log;
        private static ForeignTroopHudView view;
        private static IDisposable postLoadSubscription;
        private static IDisposable startSubscription;
        private static IDisposable unloadSubscription;
        private static bool initialized;
        private static bool mapReady;
        private static bool layoutValidated;
        private static bool readyLogged;
        private static bool failureLogged;
        private static bool resetPending;
        private static int lastFrame = -1;
        private static EngineInterface.PlayState stateBeforeLoad;

        internal static void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            log = logger;
            view = new ForeignTroopHudView();
            postLoadSubscription = MapLoaderR3EventHooks.OnPostLoad.Observable.Subscribe(OnPostLoad);
            startSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
            unloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
            Application.onBeforeRender += OnBeforeRender;
            initialized = true;
            log.LogInfo("FOREIGN_TROOP_HUD_INITIALIZED: static render publisher and map events registered.");
        }

        private static void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            mapReady = false;
            stateBeforeLoad = GameData.Instance?.lastGameState;
            resetPending = true;
        }

        private static void OnPostLoad(MapPostLoadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post) return;
            stateBeforeLoad = GameData.Instance?.lastGameState;
            mapReady = true;
        }

        private static void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            mapReady = false;
            stateBeforeLoad = null;
            grouped.Clear();
            entries.Clear();
            resetPending = true;
        }

        private static void OnBeforeRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (resetPending) { view.ResetForMap(); resetPending = false; }
                Refresh();
            }
            catch (Exception error)
            {
                view.Hide();
                if (failureLogged) return;
                failureLogged = true;
                log.LogError("FOREIGN_TROOP_HUD_ERROR: " + error);
            }
        }

        private static void Refresh()
        {
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (!mapReady || state == null || ReferenceEquals(state, stateBeforeLoad) ||
                state.app_mode != 14 || !MainViewModel.viewModelLoaded)
            {
                view.Hide();
                return;
            }
            MainViewModel main = MainViewModel.Instance;
            if (main == null || main.HUDmain == null || main.Show_HUD_Troops ||
                !main.Show_HUD_Main)
            {
                view.Hide();
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
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (localPlayerId < 1 || localPlayerId > 8)
            {
                view.Hide();
                return;
            }
            grouped.Clear();
            entries.Clear();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitHover == 0) continue;
                int owner = unit.r_ControllableForPlayerId;
                if (owner == localPlayerId || owner < 0 || owner > 8) continue;
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
            }
            if (entries.Count == 0)
            {
                view.Hide();
                return;
            }
            entries.Sort((a, b) => a.Owner != b.Owner ? a.Owner.CompareTo(b.Owner) : a.Type.CompareTo(b.Type));
            view.Show(entries);
        }

        private static void ValidateInteropLayout()
        {
            if (Marshal.SizeOf<GameUnit>() != 0x490 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpritePlayerColorId)).ToInt32() != 0x0C ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitHover)).ToInt32() != 0x30)
                throw new InvalidOperationException("Installed GameUnit interop layout differs from the audited native selection layout.");
        }
    }
}
