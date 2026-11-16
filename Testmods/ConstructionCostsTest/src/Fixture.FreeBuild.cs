using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Buildings;
using CrusaderDE;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Path = System.IO.Path;

namespace ConstructionCostsTest
{
    internal sealed partial class Fixture
    {
        // Canonical FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
        // These native guards are separate from the managed GameData classification.
        // Acceptance-only reads of the exact audited image; never mode overrides.
        private enum UndoNativeField
        {
            WorldKind = 0x3665F28,
            Mode = 0x8574B90,
            DecodedActor = 0x88E3D6C,
            ViewActor = 0x88E3D70
        }
        private const string AuditedUndoNativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        // Enter FreeBuild through its genuine menu and observe whether native Undo is supported.
        // Never infer native eligibility from the managed mode or change globals to obtain it.
        private IEnumerable<AutomationStep> FreeBuildSteps()
        {
            yield return new AutomationStep("genuine-freebuild-ui", TimeSpan.FromSeconds(240), StartFreeBuild);
            yield return new AutomationStep("freebuild-human-setup", TimeSpan.FromSeconds(60), SetupFreeBuild, true);
            yield return new AutomationStep("freebuild-brazier-payment", TimeSpan.FromSeconds(60), Brazier, true);
            yield return new AutomationStep("freebuild-brazier-real-undo", TimeSpan.FromSeconds(60), UndoFreeBuildBrazier, true);
            yield return new AutomationStep("freebuild-ballista-real-undo", TimeSpan.FromSeconds(90), run => FreeBuildTurret(run, eChimps.CHIMP_TYPE_BALLISTA), true);
            yield return new AutomationStep("freebuild-mangonel-real-undo", TimeSpan.FromSeconds(90), run => FreeBuildTurret(run, eChimps.CHIMP_TYPE_MANGONEL), true);
            yield return new AutomationStep("restore-native-costs", TimeSpan.FromSeconds(20), Restore, true);
        }

        private IEnumerable<AutomationWait> StartFreeBuild(AutomationRun run)
        {
            yield return AutomationWait.Until("ready main menu for genuine FreeBuild", MainMenuReady);
            long previous = run.CurrentMission?.SessionId ?? 0;
            MainViewModel.Instance.ButtonMainMenuFunction("Freebuild");
            yield return AutomationWait.Until("FreeBuild existing map rows", () =>
                MainViewModel.Instance.FRONTStandaloneMission?.FindName("MapList") is ListView list &&
                list.ItemsSource != null && ((IEnumerable)list.ItemsSource).OfType<FileRow>().Any());
            var front = MainViewModel.Instance.FRONTStandaloneMission;
            var maps = (ListView)front.FindName("MapList");
            // Prefer shipped maps so an unrelated user map cannot silently control this test.
            // These are existing Vanilla rows, not manufactured FileHeaders or new files.
            var rows = ((IEnumerable)maps.ItemsSource).OfType<FileRow>().Where(row =>
                row.fileHeader != null && !string.IsNullOrWhiteSpace(row.fileHeader.filePath) &&
                (row.fileHeader.builtinMap || File.Exists(row.fileHeader.filePath))).ToArray();
            Require(rows.Length > 0, "FreeBuild has no existing playable map row");
            FileRow selected = rows.FirstOrDefault(row => row.fileHeader.builtinMap) ?? rows[0];
            string path = Path.GetFullPath(selected.fileHeader.filePath);
            FieldInfo selectedHeader = typeof(FRONT_StandaloneMission).GetField("selectedHeader", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(selectedHeader != null && selectedHeader.FieldType == typeof(FileHeader), "Genuine FreeBuild selection contract changed");
            maps.SelectedItem = selected;
            yield return AutomationWait.Until("Vanilla accepted FreeBuild map selection", () =>
                ReferenceEquals(selectedHeader.GetValue(front), selected.fileHeader));
            log("genuine FreeBuild selected existing row=" + selected.fileHeader.display_filename + " path=" + path + " builtin=" + selected.fileHeader.builtinMap);
            // Play preserves FreeBuild; the similarly placed Next button changes mission type.
            front.ButtonClicked("Play");
            yield return AutomationWait.Until("managed genuine FreeBuild start", () =>
                run.CurrentMission != null && run.CurrentMission.SessionId != previous &&
                !string.IsNullOrWhiteSpace(run.CurrentMission.FilePath) &&
                string.Equals(Path.GetFullPath(run.CurrentMission.FilePath), path, StringComparison.OrdinalIgnoreCase));
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("FreeBuild briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => {
                Require(GameData.Instance.lastGameState.game_type == (int)SHCDESE.Interop.Enums.eGameTypeModes.GAMETYPE_MAP &&
                    GameData.Instance.mapType == Enums.GameModes.BUILD && !run.CurrentMission.Mode.IsMapEditor,
                    "Real FreeBuild game required: type=" + GameData.Instance.lastGameState.game_type + " mapType=" + GameData.Instance.mapType);
                Require(ReadAuditedUndoMode() == 0, "Genuine FreeBuild must naturally enter native Undo mode0");
                owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                Require(owner > 0 && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "FreeBuild requires the actual local Human owner");
                Require(ReceiptCount() == 0, "FreeBuild must start without paid construction receipts");
                log("PASS genuine FreeBuild nativeSnapshotType=" + GameData.Instance.lastGameState.game_type + " mapType=" + GameData.Instance.mapType +
                    " nativeUndoMode=0 human=" + owner + "; Undo execution will be proved by authenticated Chore75");
                return true;
            });
            foreach (var wait in EnsureFreeBuildKeep(run)) yield return wait;
        }

        private IEnumerable<AutomationWait> EnsureFreeBuildKeep(AutomationRun run)
        {
            if (run.InSimulation(run.CurrentMission, () => TryFindKeep(owner, out keepX, out keepY)))
            { log("FreeBuild already has a live Human Keep at " + keepX + "," + keepY); yield break; }
            int size = run.InSimulation(run.CurrentMission, () => GameTileManagerAPI.Instance.GetCurrentMapSize());
            int scale = BuildingScales.GetScale(eMappers.MAPPER_KEEP1);
            Require(scale > 0 && size > scale * 2, "FreeBuild needs valid map dimensions and Keep scale");
            int center = (size - scale) / 2, placedId = 0, attempts = 0;
            uint placedGeneration = 0;
            // FreeBuild normally starts before the first Keep. Use ordinary public free
            // placement; native terrain/occupancy rules decide which existing tile is legal.
            const int spacing = 8;
            for (int radius = 0; radius <= size && placedId == 0; radius += spacing)
            {
                for (int dy = -radius; dy <= radius && placedId == 0; dy += spacing)
                for (int dx = -radius; dx <= radius && placedId == 0; dx += spacing)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                    int x = center + dx, y = center + dy;
                    if (!run.InSimulation(run.CurrentMission, () =>
                        GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y) &&
                        GameTileManagerAPI.Instance.IsTileInsideMapBounds(x + scale - 1, y + scale - 1))) continue;
                    attempts++;
                    run.TrySpawnBuilding(run.CurrentMission, owner, x, y, eMappers.MAPPER_KEEP1, 0, out placedId, out placedGeneration);
                }
                if (placedId == 0) yield return AutomationWait.NextFrame;
            }
            Require(placedId > 0 && placedGeneration != 0, "No legal native free Keep placement after " + attempts + " attempts");
            int id = placedId; uint generation = placedGeneration;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("genuine FreeBuild Keep initialized", () => run.InSimulation(run.CurrentMission, () => FreeBuildKeepReady(id, generation)));
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => {
                FindKeep(owner, out keepX, out keepY);
                Require(ReceiptCount() == 0, "Free Keep setup must not charge Human extras or create a receipt");
                log("PASS FreeBuild public free Keep setup attempts=" + attempts + " identity=" + id + ":" + generation + " actualKeep=" + keepX + "," + keepY);
                return true;
            });
        }

        private unsafe bool FreeBuildKeepReady(int id, uint generation) =>
            BuildingAccess.TryGetById(id, out GameBuilding* building, out _) && building->r_GlobalId == generation &&
            building->r_PlayerIdOwner == owner && building->r_BuildingType == eStructs.STRUCT_KEEP_ONE && building->r_AliveState == AliveState.IsAlive;

        private IEnumerable<AutomationWait> SetupFreeBuild(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                SaveAndSet(well, 20); SaveAndSet(tower, 0);
                SaveAndSet(ConstructionCostTarget.FromStructure(eStructs.STRUCT_BALLISTA), 50);
                SaveAndSet(ConstructionCostTarget.FromStructure(eStructs.STRUCT_MANGONEL), 50);
                GamePlayerManagerAPI.Instance.AddPlayerGold(owner, 1000);
                Require(PlaceNear(owner, eMappers.MAPPER_TOWER4, true, keepX, keepY, out towerId, out _, out towerX, out towerY),
                    "No legal FreeBuild tower for real Brazier Chore69");
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        private IEnumerable<AutomationWait> UndoFreeBuildBrazier(AutomationRun run)
        {
            int slot = brazierSlot, actor = owner; uint generation = brazierGlobal;
            var target = ConstructionCostTarget.FromMapper(eMappers.MAPPER_BRAZIER);
            int percent = run.InSimulation(run.CurrentMission, () => RefundPercent(target, 100));
            float multiplier = run.InSimulation(run.CurrentMission, () => (float)GameBuildingManagerAPI.Instance.GoldRefundMultiplier);
            int expected = ReferenceMaterialRefund(12, multiplier, percent);
            int nativeKind = run.InSimulation(run.CurrentMission, () => ReadAuditedUndoField(UndoNativeField.WorldKind));
            int nativeMode = run.InSimulation(run.CurrentMission, ReadAuditedUndoMode);
            log(run.InSimulation(run.CurrentMission, () => BrazierUndoState("before Undo")));
            using (var witness = new GoldPayoutWitness(75, actor, () => BrazierMarked(slot, generation, actor)))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                // HUD's public command invokes the real native Undo producer (Chore75).
                MainViewModel.Instance.HUDmain.UndoLastAction(null, null);
                yield return run.WaitForTicks(run.CurrentMission, 4);
                log(run.InSimulation(run.CurrentMission, () => BrazierUndoState("four ticks after Undo")) +
                    " payoutPairs=" + witness.PreCount + "/" + witness.PostCount + " payout=" + witness.ActualDelta + " witnessFailure=" + witness.Failure);
                Require(nativeKind != 3 || nativeMode == 99,
                    "UNSUPPORTED_NATIVE_UNDO: native worldKind=3 and mode=" + nativeMode + "; Vanilla history writers reject this genuine UI mode. No adapter failure inferred.");
                yield return AutomationWait.Until("FreeBuild Brazier removed by real Undo", () => run.InSimulation(run.CurrentMission, () => BrazierRemoved() && ReceiptCount() == 0));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                Require(witness.Failure == null && witness.PreCount == 1 && witness.PostCount == 1 && witness.ActualDelta == expected,
                    "Brazier real Undo payout: expected=" + expected + " actual=" + witness.ActualDelta + " callbacks=" + witness.PreCount + "/" + witness.PostCount + " " + witness.Failure);
                log("PASS FreeBuild Brazier real Chore75 refund=" + witness.ActualDelta + " percent=" + percent + " multiplier=" + multiplier + " receipts=0 identity=" + slot + ":" + generation);
            }
        }

        private IEnumerable<AutomationWait> FreeBuildTurret(AutomationRun run, eChimps type)
        {
            eMappers mapper = type == eChimps.CHIMP_TYPE_BALLISTA ? eMappers.MAPPER_BALLISTA : eMappers.MAPPER_MANGONEL;
            int x = 0, y = 0;
            run.InSimulation(run.CurrentMission, () => {
                Require(PlaceNear(owner, eMappers.MAPPER_TOWER4, true, keepX, keepY, out _, out _, out x, out y), "No legal FreeBuild tower for " + type);
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            turretId = 0;
            int before = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
            int charged = 0;
            using (var charge = new GoldPayoutWitness(42, owner, () => true, subtract: true))
            {
                using (UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(args => {
                    if (args.PlayerOwnerId != owner || args.UnitType != type) return;
                    if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue)
                        turretId = (int)args.ReturnValue;
                }))
                {
                    foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                    Require(EngineInterface.StartMapperItem((int)mapper) >= 0, "FreeBuild turret mapper refused: " + mapper);
                    int center = BuildingScales.GetScale(eMappers.MAPPER_TOWER4) / 2;
                    EngineInterface.PlaceMapperItem((int)mapper, x + center, y + center, 0, owner, true, false, 1);
                    yield return AutomationWait.Until("FreeBuild actual Chore42 turret spawn " + type, () => turretId > 0);
                    yield return AutomationWait.Until("FreeBuild initialized turret " + type, () => run.InSimulation(run.CurrentMission, () => TurretAlive(turretId)));
                    foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                }
                Require(charge.Failure == null && charge.PreCount == 2 && charge.PostCount == 2,
                    "FreeBuild turret payment needs two native/Human debit pairs in authenticated Chore42: " + charge.PreCount + "/" + charge.PostCount + " " + charge.Failure);
                charged = charge.ActualDelta;
                int outerCharge = run.InSimulation(run.CurrentMission, () => before - GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
                log("FreeBuild actualChore42 gold=" + charged + " outerDebit=" + outerCharge + " outsideIncome=" + (charged - outerCharge));
            }
            int id = turretId, actor = owner;
            uint generation = run.InSimulation(run.CurrentMission, () => UnitGeneration(id));
            Require(charged == 62 && ReceiptCount() == 1, "FreeBuild turret requires actual 50 native +12 Human payment: " + type + " charged=" + charged);
            var target = ConstructionCostTarget.FromMapper(mapper);
            int percent = run.InSimulation(run.CurrentMission, () => RefundPercent(target, 100));
            int expected = (int)(50 * (percent / 100.0)) + (int)(12 * (percent / 100.0));
            using (var witness = new GoldPayoutWitness(75, actor, () => FreeBuildTurretMatches(id, generation, actor, type)))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                MainViewModel.Instance.HUDmain.UndoLastAction(null, null);
                yield return AutomationWait.Until("FreeBuild turret removed by real Undo " + type, () => run.InSimulation(run.CurrentMission, () => FreeBuildTurretRemoved(id, generation) && ReceiptCount() == 0));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                Require(witness.Failure == null && witness.PreCount == 2 && witness.PostCount == 2 && witness.ActualDelta == expected,
                    "Turret real Undo must refund native and Human payment once: " + type + " expected=" + expected + " actual=" + witness.ActualDelta +
                    " callbacks=" + witness.PreCount + "/" + witness.PostCount + " " + witness.Failure);
                log("PASS FreeBuild turret=" + type + " Chore42 charge=" + charged + " real Chore75 combinedRefund=" + witness.ActualDelta + " percent=" + percent + " receipts=0 identity=" + id + ":" + generation);
            }
        }

        private static unsafe bool FreeBuildTurretMatches(int id, uint generation, int player, eChimps type) =>
            UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId == generation &&
            unit->r_ControllableForPlayerId == player && unit->r_UnitChimp == type &&
            (unit->r_AliveState == AliveState.IsAlive || unit->r_AliveState == AliveState.MarkedForDeletion);

        private static unsafe bool FreeBuildTurretRemoved(int id, uint generation) =>
            !UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit->r_GlobalId != generation ||
            unit->r_AliveState == AliveState.MarkedForDeletion || unit->r_AliveState == AliveState.None;

        private unsafe string BrazierUndoState(string stage)
        {
            var state = GameData.Instance.lastGameState;
            string result = "Undo snapshot " + stage + " nativeWorldKind=" + ReadAuditedUndoField(UndoNativeField.WorldKind) +
                " nativeMode=" + ReadAuditedUndoMode() + " decodedActor=" + ReadAuditedUndoField(UndoNativeField.DecodedActor) +
                " nativeView=" + ReadAuditedUndoField(UndoNativeField.ViewActor) + " snapshotKind=" + state.game_type +
                " undoAvailable=" + state.undoAvailable + " spectator=" + state.spectatorMode + " receipts=" + ReceiptCount() +
                " expectedBrazier=" + brazierSlot + ":" + brazierGlobal + " removed=" + BrazierRemoved();
            var span = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            if (brazierSlot >= 25 && brazierSlot < span.Length)
            {
                ref var projectile = ref span[brazierSlot];
                result += " actualGlobal=" + projectile.r_GlobalId + " type=" + projectile.r_ProjectileType +
                    " owner=" + (ushort)projectile.r_PlayerSourceId + " state=" + projectile.r_AliveState;
            }
            return result;
        }

        private static int ReadAuditedUndoMode() => ReadAuditedUndoField(UndoNativeField.Mode);

        private static int ReadAuditedUndoField(UndoNativeField fieldName)
        {
            // ConstructionCosts initialization validates the loaded native hash before exposing
            // IsAvailable. Recheck its pinned contract so a future SDK cannot broaden this read.
            var hash = typeof(ConstructionCosts).GetField("NativeHash", BindingFlags.Static | BindingFlags.NonPublic);
            Require(ConstructionCosts.IsAvailable && hash != null && (string)hash.GetRawConstantValue() == AuditedUndoNativeHash,
                "Undo mode reader requires the exact hash-validated native construction contract");
            var field = typeof(CrusaderLibrary).GetField("LibraryModuleHandle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Require(field != null && field.FieldType == typeof(IntPtr), "Extender library handle contract changed");
            var module = (IntPtr)field.GetValue(CrusaderLibrary.Instance);
            Require(module != IntPtr.Zero, "Native library is not loaded");
            return System.Runtime.InteropServices.Marshal.ReadInt32(module + (int)fieldName);
        }
    }
}
