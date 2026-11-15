using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Input;
using APIShared;
using APIShared.Automation;
using BepInEx.Bootstrap;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CastlePlannerRotationTest
{
    // Plugin.active and the public automation runner retain the fixture after startup cleanup.
    // Only public settings and UI commands are written. Internal production state is read only.
    internal sealed partial class Fixture
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly string map, scenario;
        private readonly Guid runId;
        private readonly Action<string> log;
        private object preview, settings;
        private int expectedRotation, player, previewCount;

        internal Fixture(string map, string scenario, Guid runId, Action<string> log)
        { this.map = map; this.scenario = scenario; this.runId = runId; this.log = log; }

        internal static bool Owns(string value) => new[] {
            "castle-0", "castle-90", "castle-180", "castle-270", "keep-90", "unchanged-east", "castle-90-save", "editor-east"
        }.Contains(value);

        internal IEnumerable<AutomationStep> Steps()
        {
            if (scenario == "editor-east")
            {
                yield return new AutomationStep("editor-rotated-keep-and-blueprint", TimeSpan.FromSeconds(180), EditorEast);
                yield break;
            }
            yield return new AutomationStep("real-preview-and-restart", TimeSpan.FromSeconds(240), StartAndSelect);
            yield return new AutomationStep("actual-keep-and-castle", TimeSpan.FromSeconds(90), Verify, true);
            yield return new AutomationStep("distribution-deadline-guard", TimeSpan.FromSeconds(20), VerifyDistributionDeadline, true);
            if (scenario == "castle-90-save")
                yield return new AutomationStep("save-reload-and-blueprint", TimeSpan.FromSeconds(180), SaveReload);
        }

        private IEnumerable<AutomationWait> StartAndSelect(AutomationRun run)
        {
            object plugin = Chainloader.PluginInfos["CastlePlanner_Serp"].Instance;
            settings = Get(plugin, "Settings");
            foreach (string name in new[] { "EnableMod", "SpawnCastle", "AllowRotation", "SpawnBuildings", "SpawnFortifications", "SpawnDefensiveGroundFeatures", "SpawnFearFactorBuildings", "SpawnSiegeEngines", "SpawnBraziersAndFlags", "EnableClientFeatures", "Blueprints" })
                Set(settings, name, true);
            preview = plugin.GetType().GetField("previewRuntime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (scenario == "unchanged-east")
            {
                Require(Chainloader.PluginInfos.TryGetValue("fixes", out var fixes), "unchanged-east requires Fixes");
                object fixesSettings = Get(fixes.Instance, "LobbySettingsViewModel");
                var property = fixesSettings.GetType().GetProperty("PlayerKeepRotation");
                property.SetValue(fixesSettings, Enum.ToObject(property.PropertyType, 2), null);
            }
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT })) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
            yield return AutomationWait.Until("CastlePlanner selectable preview", () => {
                CloseBriefing();
                return Convert.ToBoolean(Get(preview, "IsPreviewActive")) && Convert.ToBoolean(Get(preview, "CanConfirm"));
            });
            player = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            previewCount = run.InSimulation(run.CurrentMission, CountOwned);
            string[] choices = ((IEnumerable)Get(preview, "CastleChoices")).Cast<object>().Select(Convert.ToString).ToArray();
            string castle = choices.FirstOrDefault(c => c.IndexOf("Community_wolf1.aivjson", StringComparison.OrdinalIgnoreCase) >= 0);
            Require(castle != null, "Vanilla Community_wolf1 fixture absent from actual choices");
            Set(preview, "SelectedChoice", scenario == "keep-90" ? Get(preview, "KeepOnlyText") : castle);
            int degrees = scenario == "unchanged-east" ? -1 : int.Parse(scenario.Split('-')[1]);
            if (degrees >= 0)
            {
                string suffix = "(" + degrees + "°)";
                string rotation = ((IEnumerable)Get(preview, "RotationChoices")).Cast<object>().Select(Convert.ToString)
                    .Single(c => c.EndsWith(suffix, StringComparison.Ordinal));
                Set(preview, "SelectedRotation", rotation);
            }
            expectedRotation = scenario == "unchanged-east" ? 2 : degrees / 45;
            Require(Convert.ToInt32(Get(preview, "SelectedNativeRotation")) == expectedRotation, "preview resolved unexpected direction");
            Require(Convert.ToBoolean(Get(preview, "PreserveSelectedKeepRotation")) == (scenario == "unchanged-east"), "preserve intent mismatch");
            log("confirmed scenario=" + scenario + " player=" + player + " choice=" + Get(preview, "SelectedChoice") + " direction=" + expectedRotation + " baselineBuildings=" + previewCount);
            long before = run.CurrentMission.SessionId;
            ((ICommand)Get(preview, "ConfirmCommand")).Execute(null);
            yield return AutomationWait.Until("CastlePlanner committed restarted mission", () => run.CurrentMission != null && run.CurrentMission.SessionId != before);
        }

        private IEnumerable<AutomationWait> Verify(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            CloseBriefing();
            yield return AutomationWait.Until("preview finished", () => !Convert.ToBoolean(Get(preview, "IsPreviewActive")));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 24);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            int count = run.InSimulation(run.CurrentMission, VerifyNativeKeep);
            Require(scenario == "keep-90" ? count == previewCount : count > previewCount + 20,
                "castle outcome mismatch; buildings=" + count + " preview=" + previewCount);
            // Invoke the production shared reader as well as checking native state independently.
            MethodInfo reader = preview.GetType().GetMethod("TryReadKeepRotation", BindingFlags.Static | BindingFlags.NonPublic);
            object[] parameters = { player, 0 };
            Require((bool)reader.Invoke(null, parameters) && (int)parameters[1] == expectedRotation, "production reader differs from actual Keep");
            log("verified scenario=" + scenario + " direction=" + expectedRotation + " ownedBuildings=" + count + " delta=" + (count - previewCount));
        }

        private unsafe int VerifyNativeKeep()
        {
            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(player);
            Require(BuildingAccess.TryGetById(keepId, out GameBuilding* keep, out _) && keep != null, "no authoritative Keep");
            Require(keep->r_PlayerIdOwner == player && keep->r_AliveState == AliveState.IsAlive, "Keep owner or life state mismatch");
            int actual = keep->N00012042_2 == 15 ? 0 : keep->N00012042_2;
            Require(actual == expectedRotation, "Keep direction=" + actual + " expected=" + expectedRotation);
            log("keep=" + keepId + " global=" + keep->r_GlobalId + " actualDirection=" + actual + " sprite=" + keep->r_SpriteVariationIndex);
            return CountOwned();
        }

        private IEnumerable<AutomationWait> VerifyDistributionDeadline(AutomationRun run)
        {
            // An isolated clone exercises the real callback without subscribing it. Deliberately
            // unequal decision/roster counts keep the old erroneous timeout path from sending,
            // restarting or touching the actual game, while its added None decision is observable.
            object clone = typeof(object).GetMethod("MemberwiseClone", Instance).Invoke(preview, null);
            Type type = clone.GetType();
            var none = new HashSet<int>();
            var acks = new HashSet<ulong> { 111, 222 };
            object manifest = Field(clone, "committedSelections");
            var decisions = (IDictionary)Activator.CreateInstance(Field(clone, "decisions").GetType());
            decisions.Add(7, null);
            decisions.Add(8, null);
            SetField(clone, "decisions", decisions);
            SetField(clone, "roster", new HashSet<int> { 1 });
            SetField(clone, "noneDecisions", none);
            SetField(clone, "manifestAcks", acks);
            SetField(clone, "state", Enum.Parse(Field(clone, "state").GetType(), "Distributing"));
            SetField(clone, "realMultiplayer", false);
            SetField(clone, "lastFrame", -1);
            SetField(clone, "countdownStarted", Stopwatch.GetTimestamp() - 600L * Stopwatch.Frequency);
            SetField(clone, "activeTimeoutSeconds", 5);
            SetField(clone, "lastDisplayedTimerSeconds", 0);
            SetField(clone, "PropertyChanged", null);
            type.GetMethod("OnBeforeRender", Instance).Invoke(clone, null);
            Require(none.Count == 0, "expired selection deadline changed a distributed decision set");
            Require(Convert.ToString(Field(clone, "state")) == "Distributing", "expired deadline changed distribution state");
            Require(acks.SetEquals(new ulong[] { 111, 222 }) && ReferenceEquals(Field(clone, "committedSelections"), manifest), "expired deadline reset acknowledgements or manifest");
            // A stale finalizer in Distributing must return before reading selection prerequisites.
            // Nulling the clone's roster makes an accidental re-entry fail before any native work.
            SetField(clone, "roster", null);
            type.GetMethod("TryFinalizeAsHost", Instance).Invoke(clone, null);
            Require(acks.SetEquals(new ulong[] { 111, 222 }) && ReferenceEquals(Field(clone, "committedSelections"), manifest), "stale finalizer reset distribution");
            log("distribution callback and stale finalizer preserved manifest and ACKs after expired selection deadline");
            yield break;
        }

        private static object Field(object value, string name) => value.GetType().GetField(name, Instance).GetValue(value);
        private static void SetField(object value, string name, object replacement) => value.GetType().GetField(name, Instance).SetValue(value, replacement);

        private int CountOwned()
        {
            int count = 0;
            foreach (GameBuilding b in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
                if (b.r_PlayerIdOwner == player && (b.r_AliveState == AliveState.IsAlive || b.r_AliveState == AliveState.NeedsInit)) count++;
            return count;
        }
        private static void CloseBriefing()
        { if (MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null); }
        private static object Get(object value, string name) => value.GetType().GetProperty(name, Instance)?.GetValue(value, null) ??
            throw new MissingMemberException(value.GetType().FullName, name);
        private static void Set(object value, string name, object replacement) =>
            value.GetType().GetProperty(name, Instance).SetValue(value, replacement, null);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
