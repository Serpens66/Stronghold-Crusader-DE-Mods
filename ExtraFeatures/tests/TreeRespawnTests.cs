using APIShared.ModSettings;
using SHCDESE.API;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraFeatures.Tests
{
    internal static class TreeRespawnTests
    {
        internal static void Run(Action<bool, string> check)
        {
            Settings(check);
            Timers(check);
            BatchAndArchive(check);
        }

        private static void Settings(Action<bool, string> check)
        {
            string fixture = TestWorkDirectory.New("TreeRespawnPreset");
            ExtraFeaturesViewModel Prepare()
            {
                var model = new ExtraFeaturesViewModel(false);
                model.PreparePresets(null, Path.Combine(fixture, "ExtraFeatures.dll"), "TreeRespawn");
                model.ActivatePresets();
                return model;
            }

            var settings = Prepare();
            check(settings.TreeRespawnMinutes == -1, "Tree Respawn defaults off");
            check(settings.MatureTreesOnly, "Mature tree harvesting defaults on with Tree Respawn off");
            settings.TreeRespawnMinutes = int.MinValue;
            check(settings.TreeRespawnMinutes == -1, "Tree Respawn clamps below off");
            settings.TreeRespawnMinutes = int.MaxValue;
            check(settings.TreeRespawnMinutes == 120 && settings.TreeRespawnValueText == "120 min", "Tree Respawn clamps and displays minutes");
            settings.TreeRespawnMinutes = 0;
            settings.MatureTreesOnly = false;
            settings.System_SavePersonalPreset("tree-zero", "Tree zero", "", new[] {
                new PresetSaveSelection { PropertyName = nameof(settings.TreeRespawnMinutes), Mode = PublishedPresetValueMode.Fixed },
                new PresetSaveSelection { PropertyName = nameof(settings.MatureTreesOnly), Mode = PublishedPresetValueMode.Fixed }
            }, false);
            var restored = Prepare();
            check(restored.TreeRespawnMinutes == 0, "Zero survives persistence as an enabled value");
            restored.System_LoadModDefaults();
            check(restored.TreeRespawnMinutes == -1, "Shared defaults disable Tree Respawn");
            check(restored.MatureTreesOnly, "Shared defaults enable mature tree harvesting");
            restored.System_TestLoadPreset(restored.System_TestPublishedPresets.Single(p => p.Id == "tree-zero").StableId);
            check(restored.TreeRespawnMinutes == 0, "Preset restores zero");
            check(!restored.MatureTreesOnly, "Preset restores an explicitly disabled mature tree rule");
            restored.SetTreeRespawnMissionLocked(true);
            restored.SetMatureTreeHarvestMissionLocked(true);
            restored.TreeRespawnMinutes = 10;
            restored.MatureTreesOnly = true;
            check(restored.TreeRespawnMinutes == 0 && !restored.CanConfigureTreeRespawn, "Mission locks Tree Respawn editing");
            check(!restored.MatureTreesOnly && !restored.CanConfigureMatureTreeHarvest, "Mission locks mature tree editing");
            restored.SetTreeRespawnMissionLocked(false);
            restored.SetMatureTreeHarvestMissionLocked(false);
            GameNetworkAPI.LocalHost = false;
            GameNetworkAPI.Networked = true;
            GameNetworkAPI.MultiplayerGame = true;
            try
            {
                restored.TreeRespawnMinutes = 120;
                restored.MatureTreesOnly = true;
                check(restored.TreeRespawnMinutes == 0 && !restored.CanConfigureTreeRespawn, "Client cannot edit Tree Respawn");
                check(!restored.MatureTreesOnly && !restored.CanConfigureMatureTreeHarvest, "Client cannot edit mature tree harvesting");
                GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () =>
                {
                    restored.TreeRespawnMinutes = 10;
                    restored.MatureTreesOnly = true;
                });
                check(restored.TreeRespawnMinutes == 10, "Client applies the authoritative host delay");
                check(restored.MatureTreesOnly, "Client applies the authoritative mature tree rule");
            }
            finally
            {
                GameNetworkAPI.LocalHost = true;
                GameNetworkAPI.Networked = false;
                GameNetworkAPI.MultiplayerGame = false;
            }
        }

        private static void Timers(Action<bool, string> check)
        {
            const int minute = TreeRespawnState.TicksPerMinute;
            var site = new TreeRespawnState.Site(7, 5, VegetationType.PineTree);
            var state = new TreeRespawnState(new List<TreeRespawnState.Site> { site }, 0);
            var world = new World { Present = { site.Key } };
            state.Advance(minute, 0, world.HasTree, world.Plant);
            check(world.Attempts.Count == 0, "An occupied original site cannot respawn at zero");
            world.Present.Clear();
            state.Removed(site.Key, minute + 4);
            state.Removed(TreeRespawnState.KeyOf(8, 5), minute + 4);
            check(!state.HasWork(2 * minute - 1), "Zero waits for the next scheduled check");
            state.Advance(2 * minute, 0, world.HasTree, world.Plant);
            check(world.Attempts.Count == 1 && world.Attempts[0].Species == VegetationType.PineTree &&
                world.Attempts[0].Key == site.Key, "Zero returns the original species at the original tile only");

            state = new TreeRespawnState(new List<TreeRespawnState.Site> { site }, 0);
            world.Attempts.Clear();
            world.Present.Clear();
            state.Removed(site.Key, 10);
            state.Advance(10 * minute, 10, world.HasTree, world.Plant);
            check(world.Attempts.Count == 0, "Ten minutes may not expire before the deletion timestamp plus ten minutes");
            world.Present.Add(site.Key);
            state.Advance(11 * minute, 10, world.HasTree, world.Plant);
            check(world.Attempts.Count == 0, "A natural tree satisfies the original position");
            world.Present.Clear();
            state.Removed(site.Key, 11 * minute + 5);
            state.Advance(21 * minute, 10, world.HasTree, world.Plant);
            check(world.Attempts.Count == 0, "Removing the natural replacement starts a fresh timer");
            var restored = TreeRespawnState.Decode(state.Encode(), 21 * minute, _ => true);
            check(restored != null && !restored.HasWork(22 * minute - 1), "Saving preserves the current cadence");
            world.Refuse = true;
            restored.Advance(22 * minute, 10, world.HasTree, world.Plant);
            check(world.Attempts.Count == 1 && !restored.HasWork(22 * minute + 1), "An occupied tile or full pool retries at the next check");
            world.Refuse = false;
            restored.Advance(23 * minute, 10, world.HasTree, world.Plant);
            check(world.Attempts.Count == 2 && world.Present.Contains(site.Key), "An overdue site respawns after it becomes free");

            // Loading with current settings can replace the delay in the save.
            world.Refuse = true;
            world.Present.Clear();
            world.Attempts.Clear();
            state = new TreeRespawnState(new List<TreeRespawnState.Site> { site }, 0);
            state.Removed(site.Key, minute - 1);
            state.Advance(minute, 0, world.HasTree, world.Plant);
            restored = TreeRespawnState.Decode(state.Encode(), minute, _ => true);
            restored.ReconcileSettings(minute, 120);
            world.Refuse = false;
            restored.Advance(2 * minute, 120, world.HasTree, world.Plant);
            check(world.Attempts.Count == 1, "A blocked zero-delay site honors a longer delay selected on load");
            restored.ReconcileSettings(2 * minute, TreeRespawnState.Off);
            restored = TreeRespawnState.Decode(restored.Encode(), 10 * minute, _ => true);
            restored.ReconcileSettings(10 * minute, 1);
            restored.Advance(10 * minute, 1, world.HasTree, world.Plant);
            check(world.Attempts.Count == 1, "A save written while off retains the site without an active timer");
            restored.Advance(11 * minute, 1, world.HasTree, world.Plant);
            check(world.Attempts.Count == 2 && world.Present.Contains(site.Key), "Loading with the feature enabled resumes tracking original sites");
        }

        private static void BatchAndArchive(Action<bool, string> check)
        {
            const int minute = TreeRespawnState.TicksPerMinute;
            var sites = Enumerable.Range(1, 33).Reverse()
                .Select(x => new TreeRespawnState.Site((ushort)x, 5, VegetationType.OakTree)).ToList();
            int start = int.MaxValue - 11;
            var state = new TreeRespawnState(sites, start);
            int tick = unchecked(start + minute);
            var world = new World();
            state.Advance(tick, 0, world.HasTree, world.Plant);
            check(world.Attempts.Count == 16 && world.Attempts[0].X == 1 && world.Attempts[15].X == 16,
                "Missed deletion is reconciled in deterministic bounded order across tick wrap");
            byte[] record = state.Encode();
            var restored = TreeRespawnState.Decode(record, tick, _ => true);
            check(restored != null && record.SequenceEqual(restored.Encode()), "Archive preserves the unfinished batch");
            var longer = TreeRespawnState.Decode(record, tick, _ => true);
            longer.ReconcileSettings(tick, 120);
            longer.Advance(tick + 1, 120, world.HasTree, world.Plant);
            check(world.Attempts.Count == 16, "A queued zero-delay batch honors a longer delay selected on load");
            state.Removed(TreeRespawnState.KeyOf(17, 5), tick);
            var stale = TreeRespawnState.Decode(state.Encode(), tick, _ => true);
            stale.ReconcileSettings(tick, 0);
            var staleWorld = new World();
            stale.Advance(tick + 1, 0, staleWorld.HasTree, staleWorld.Plant);
            check(staleWorld.Attempts.Count == 15 && staleWorld.Attempts[0].X == 18,
                "A fresh deletion during a batch stays deferred across save and reload");
            restored.Advance(tick + 1, 0, world.HasTree, world.Plant);
            restored.Advance(tick + 2, 0, world.HasTree, world.Plant);
            check(world.Attempts.Count == 33 && world.Attempts.Last().X == 33, "Reload resumes the remaining sorted batch without duplicates");
            check(TreeRespawnState.Decode(null, tick, _ => true) == null &&
                TreeRespawnState.Decode(record.Take(record.Length - 1).ToArray(), tick, _ => true) == null &&
                TreeRespawnState.Decode(record, tick, _ => false) == null, "Missing, truncated and off-map records fail closed");
        }

        private sealed class World
        {
            internal readonly HashSet<int> Present = new HashSet<int>();
            internal readonly List<TreeRespawnState.Site> Attempts = new List<TreeRespawnState.Site>();
            internal bool Refuse;
            internal bool HasTree(TreeRespawnState.Site site) => Present.Contains(site.Key);
            internal bool Plant(TreeRespawnState.Site site)
            {
                Attempts.Add(site);
                return !Refuse && Present.Add(site.Key);
            }
        }
    }
}
