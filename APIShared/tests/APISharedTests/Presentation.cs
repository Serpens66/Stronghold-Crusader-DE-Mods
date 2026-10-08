using APIShared.ModSettings;
using APIShared;
using CrusaderDE;
using Iced.Intel;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyLocalSelectionSnapshots() => TestLocalSelectionSnapshots();

        private static void TestLocalSelectionSnapshots()
        {
            Assert(!CaptureSelectionState(1, (EngineInterface.PlayState)null, out LocalSelectionSnapshot missing) && missing == null,
                "missing game state invalidates the shared selection cache");

            var empty = new EngineInterface.PlayState();
            Assert(CaptureSelectionState(1, empty, out LocalSelectionSnapshot first) && first.Count == 0,
                "empty selections are valid snapshots");
            var sameEmpty = new EngineInterface.PlayState();
            Assert(CaptureSelectionState(1, sameEmpty, out LocalSelectionSnapshot reusedEmpty) &&
                ReferenceEquals(first, reusedEmpty), "unchanged selection is shared across completed ticks");

            var selected = new EngineInterface.PlayState { numSelectedChimps = 2 };
            selected.selectedChimps[0] = 11;
            selected.selectedChimps[1] = 22;
            selected.selectedChimpTypes[0] = 3;
            selected.selectedChimpTypes[1] = 4;
            Assert(CaptureSelectionState(1, selected, out LocalSelectionSnapshot beforeAction) &&
                beforeAction.Count == 2 && beforeAction[1].UnitId == 22 && beforeAction[1].UnitType == 4,
                "action reads selection from the latest completed state");

            var nextTick = new EngineInterface.PlayState { numSelectedChimps = 2 };
            nextTick.selectedChimps[0] = 11;
            nextTick.selectedChimps[1] = 23;
            nextTick.selectedChimpTypes[0] = 3;
            nextTick.selectedChimpTypes[1] = 4;
            Assert(CaptureSelectionState(1, nextTick, out LocalSelectionSnapshot afterAction) &&
                !ReferenceEquals(beforeAction, afterAction) && afterAction[1].UnitId == 23 &&
                beforeAction[1].UnitId == 22, "selection changes are visible immediately and prior snapshots stay immutable");

            var unchangedTick = new EngineInterface.PlayState { numSelectedChimps = 2 };
            unchangedTick.selectedChimps[0] = 11;
            unchangedTick.selectedChimps[1] = 23;
            unchangedTick.selectedChimpTypes[0] = 3;
            unchangedTick.selectedChimpTypes[1] = 4;
            bool reused = true;
            for (int tick = 0; tick < 100; tick++)
            {
                reused &= CaptureSelectionState(1, unchangedTick, out LocalSelectionSnapshot current) &&
                    ReferenceEquals(afterAction, current);
            }
            Assert(reused, "unchanged selection does not allocate across repeated calls");

            var parallel = new LocalSelectionSnapshot[32];
            Parallel.For(0, parallel.Length, index =>
                CaptureSelectionState(1, unchangedTick, out parallel[index]));
            Assert(parallel.All(snapshot => ReferenceEquals(afterAction, snapshot)),
                "concurrent mod callers receive the same selection snapshot");

            var olderState = new EngineInterface.PlayState { numSelectedChimps = 1 };
            olderState.selectedChimps[0] = 31;
            var newerState = new EngineInterface.PlayState { numSelectedChimps = 1 };
            newerState.selectedChimps[0] = 32;
            LocalSelectionSnapshot olderResult = null;
            LocalSelectionSnapshot newerResult = null;
            using (var olderReaderEntered = new ManualResetEventSlim())
            using (var releaseOlderReader = new ManualResetEventSlim())
            using (var newerCallStarted = new ManualResetEventSlim())
            using (var newerReaderEntered = new ManualResetEventSlim())
            {
                Task olderCall = Task.Run(() => CaptureSelectionState(1, () =>
                {
                    olderReaderEntered.Set();
                    releaseOlderReader.Wait(5000);
                    return olderState;
                }, out olderResult));
                bool olderStarted = olderReaderEntered.Wait(5000);
                Task newerCall = Task.Run(() =>
                {
                    newerCallStarted.Set();
                    CaptureSelectionState(1, () =>
                    {
                        newerReaderEntered.Set();
                        return newerState;
                    }, out newerResult);
                });
                bool newerStarted = newerCallStarted.Wait(5000);
                bool newerReadBeforeLockReleased = newerReaderEntered.Wait(100);
                releaseOlderReader.Set();
                bool completed = Task.WaitAll(new[] { olderCall, newerCall }, 5000);
                Assert(olderStarted && newerStarted && !newerReadBeforeLockReleased && completed &&
                    olderResult != null && newerResult != null &&
                    olderResult[0].UnitId == 31 && newerResult[0].UnitId == 32 &&
                    CaptureSelectionState(1, newerState, out LocalSelectionSnapshot currentState) &&
                    ReferenceEquals(newerResult, currentState),
                    "concurrent callers read completed states in cache-lock order");
            }

            Assert(CaptureSelectionState(2, unchangedTick, out LocalSelectionSnapshot anotherPlayer) &&
                !ReferenceEquals(afterAction, anotherPlayer), "player changes invalidate shared selection identity");
            unchangedTick.numSelectedChimps = 3;
            unchangedTick.selectedChimps = new int[2];
            Assert(!CaptureSelectionState(2, unchangedTick, out LocalSelectionSnapshot truncated) && truncated == null,
                "mismatched selection arrays fail closed");

            var full = new EngineInterface.PlayState { numSelectedChimps = 10000 };
            full.selectedChimps[9999] = 9876;
            full.selectedChimpTypes[9999] = 77;
            Assert(CaptureSelectionState(1, full, out LocalSelectionSnapshot maximum) &&
                maximum.Count == 10000 && maximum[9999].UnitId == 9876 && maximum[9999].UnitType == 77,
                "Vanilla's maximum selection capacity is supported");
            full.numSelectedChimps = 10001;
            Assert(!CaptureSelectionState(1, full, out LocalSelectionSnapshot oversized) && oversized == null,
                "selection count above Vanilla capacity fails closed");
            Assert(!CaptureSelectionState(1, (EngineInterface.PlayState)null, out LocalSelectionSnapshot afterMapChange) && afterMapChange == null,
                "map unload clears the selection snapshot");
        }
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyUnitHudActivation() => TestUnitHudActivation();

        private static void TestUnitHudActivation()
        {
            Type type = typeof(UnitHudPresentationService);
            object service = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke(new object[] { "test", null, null, false });
            Func<string, object> field = name => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(service);
            Action<string, object> set = (name, value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, value);
            Func<string, IUnitHudPresentationCapability> bind = owner => ((UnitHudPresentationService)service).Bind(owner);
            var passive = bind("read-only");
            passive.RequestRefresh();
            Assert(!(bool)field("pendingPresentation") && !(bool)field("refreshRequested"), "capability lookup and passive refresh do not enable presentation");
            // Render entry order is checked below against source: standalone CLR cannot JIT Unity ECalls.
            Assert((UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None && field("recruitmentLease") == null,
                "passive owner satisfies the render idle guard");

            var a = bind("a"); var b = bind("b");
            var aa = (IUnitHudActivationCapability)a; var ba = (IUnitHudActivationCapability)b;
            aa.SetOwnerActive(false);
            int calls = 0;
            Assert(a.TryRegisterCategory(new UnitHudCategoryDefinition("lord", "Lord", 55, UnitHudSurface.All), u => { calls++; return true; }, out _), "inactive registration succeeds");
            Assert((UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None && !(bool)field("pendingPresentation"), "inactive registration does not wake HUD");
            a.RequestRefresh();
            Assert(!(bool)field("refreshRequested") && a.GetSelectedCategories().Count == 0 && calls == 0, "inactive refresh and queries perform no classification");
            aa.SetOwnerActive(true);
            Assert(((UnitHudSurface)field("activeSurfaces") & UnitHudSurface.ArmyReport) != 0 &&
                ((UnitHudSurface)field("activeSurfaces") & UnitHudSurface.Recruitment) == 0, "All category does not enable recruitment without handler");
            Assert(b.TryRegisterCategory(new UnitHudCategoryDefinition("archer", "Archer", 22, UnitHudSurface.TroopSelection | UnitHudSurface.Recruitment), u => false, out _), "legacy registrations remain active");
            Assert(b.TryRegisterRecruitment("archer", ticket => true, out _), "recruitment attaches to owner category");
            Assert((bool)field("activeRecruitmentHandlers"), "active handler enables recruitment");
            aa.SetOwnerActive(false);
            Assert(((UnitHudSurface)field("activeSurfaces") & UnitHudSurface.ArmyReport) == 0 && (bool)field("activeRecruitmentHandlers"), "disabling one owner preserves another");
            Assert(!aa.SetCategoryActive("archer", false) && ba.SetCategoryActive("archer", false), "activation is owner-bound");
            Assert(!(bool)field("activeRecruitmentHandlers") && (UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None, "category disable also disables recruitment");
            Assert((UnitHudSurface)field("restoreSurfaces") != UnitHudSurface.None && (bool)field("pendingPresentation"), "last disable retains restoration work");
            Assert(ba.SetCategoryActive("archer", true) && (bool)field("activeRecruitmentHandlers"), "category reactivation works without re-registration");
            ba.SetOwnerActive(false);
            var image = bind("image");
            Assert(image.TryRegisterImageOverride(new UnitHudImageOverrideDefinition("skin", UnitHudImageSlot.UIBuildingsO011), ctx => null, out _), "image-only registration succeeds");
            Assert((bool)field("activeImages") && (UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None, "image-only owner enables no unit surfaces");
            // Simulate completed restoration/refresh; the image hook will wake only for real sprite changes.
            set("pendingPresentation", false); set("refreshRequested", false);
            Assert((UnitHudSurface)field("activeSurfaces") == UnitHudSurface.None && field("recruitmentLease") == null,
                "image-only owner satisfies the render idle guard after refresh");
            Assert(((IUnitHudActivationCapability)image).SetImageOverrideActive("skin", false) && !(bool)field("activeImages"), "individual image override disables");
            Assert((bool)field("restoreImages"), "image disable requests Vanilla restoration");
        }
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyUnitHudSnapshotImmutability() => TestUnitHudSnapshotImmutability();

        private static void TestUnitHudSnapshotImmutability()
        {
            var original = new List<UnitHudUnitSnapshot>
            {
                new UnitHudUnitSnapshot(1, 10, 2, 1, true)
            };
            var category = new UnitHudCategorySnapshot("owner", "category", "Category", original);
            var group = new UnitHudControlGroupSnapshot(0, original);
            original.Clear();
            Assert(category.Units.Count == 1 && group.Units.Count == 1,
                "unit-HUD snapshots defensively copy caller-owned membership lists");
            Assert(!(category.Units is UnitHudUnitSnapshot[]) && !(group.Units is UnitHudUnitSnapshot[]),
                "unit-HUD snapshot membership is not exposed as a mutable array");
        }
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyUnitHudVariantContracts() => TestUnitHudVariantContracts();

        private static void TestUnitHudVariantContracts()
        {
            var oldCategory = new UnitHudCategoryDefinition("old", "Old", 1, UnitHudSurface.TroopSelection);
            Assert(oldCategory.TextProfile.DisplayNameFallback == "Old" && oldCategory.TextProfile.DescriptionFallback == string.Empty,
                "legacy category constructor must retain deterministic text fallbacks");
            var text = new UnitHudTextProfile("Desert Archer", "DA", "Description", kind => kind == UnitHudTextKind.DisplayName ? "Localized" : null);
            var category = new UnitHudCategoryDefinition("desert", "Desert Archer", 1,
                UnitHudSurface.All, null, new UnitHudTint(220, 240, 255, 255), 0, text);
            Assert(category.TextProfile == text && (category.Surfaces & UnitHudSurface.Recruitment) != 0 &&
                (category.Surfaces & UnitHudSurface.UnitDetails) != 0,
                "variant category does not expose recruitment and unit-detail presentation");
            var ticket = new UnitHudRecruitmentTicket(7, "owner", "desert", 1, 2, 5);
            Assert(ticket.TicketId == 7 && ticket.PlayerId == 1 && ticket.BaseUnitType == 2 && ticket.RequestedAmount == 5,
                "recruitment ticket does not preserve immutable Vanilla request data");
        }
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyUnitHudLiveSelectionCounts() => TestUnitHudLiveSelectionCounts();

        private static void TestUnitHudLiveSelectionCounts()
        {
            var lord = new UnitHudUnitSnapshot(100, 1000, 55, 1, true);
            var spearman = new UnitHudUnitSnapshot(200, 2000, 24, 1, true);
            var secondSpearman = new UnitHudUnitSnapshot(201, 2001, 24, 1, true);
            var archer = new UnitHudUnitSnapshot(300, 3000, 22, 1, true);
            var unsupported = new UnitHudUnitSnapshot(400, 4000, 1, 1, true);
            var claimed = new HashSet<int> { lord.GameId };
            int[] emptyVanilla = new int[89];

            int[] lordFirst = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, new[] { lord, spearman, secondSpearman, archer, unsupported }, claimed, true);
            int[] troopsFirst = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, new[] { spearman, secondSpearman, archer, lord, unsupported }, claimed, true);
            Assert(ArraysEqual(lordFirst, troopsFirst) && lordFirst[24] == 2 && lordFirst[22] == 1,
                "live troop counts must be independent of Lord selection order and preserve multiplicity");
            Assert(lordFirst[55] == 0 && lordFirst[1] == 0,
                "claimed and unsupported unit types must not become Vanilla troop slots");

            int visibleTypes = 0;
            var manyTypes = new List<UnitHudUnitSnapshot>();
            int[] types = { 5, 22, 23, 24, 25, 26, 27, 28, 30 };
            for (int i = 0; i < types.Length; i++)
                manyTypes.Add(new UnitHudUnitSnapshot(500 + i, (uint)(5000 + i), types[i], 1, true));
            int[] manyCounts = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, manyTypes, new HashSet<int>(), true);
            for (int i = 0; i < manyCounts.Length; i++) if (manyCounts[i] > 0) visibleTypes++;
            Assert(visibleTypes == 9 && (visibleTypes + 7) / 8 == 2,
                "live troop counts must retain enough distinct types for Vanilla-compatible paging");

            int[] vanillaFallback = new int[89];
            vanillaFallback[24] = 3;
            int[] incomplete = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                vanillaFallback, new[] { lord }, claimed, false);
            Assert(incomplete[24] == 3 && incomplete[55] == 0,
                "an incomplete live selection must preserve Vanilla troop counts");

            int[] afterRemoval = UnitHudSelectionPolicy.ResolveVanillaTroopCounts(
                emptyVanilla, new[] { archer }, new HashSet<int>(), true);
            Assert(afterRemoval[22] == 1 && afterRemoval[24] == 0 && afterRemoval[55] == 0,
                "removed Lord and troop members must disappear from live counts");
        }
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyUnitHudSelectionIdentity() => TestUnitHudSelectionIdentity();

        private static void TestUnitHudSelectionIdentity()
        {
            int[] ids = { 10, 20 };
            int[] types = { 55, 24 };
            Assert(UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10, 20, 0 }, new[] { 55, 24, 0 }, 2),
                "an unchanged troop selection must retain the same identity");
            Assert(!UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10, 20, 30 }, new[] { 55, 24, 22 }, 3) &&
                !UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10 }, new[] { 55 }, 1) &&
                !UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 10, 20 }, new[] { 55, 22 }, 2) &&
                !UnitHudSelectionPolicy.SelectionIdentityEquals(ids, types, new[] { 20, 10 }, new[] { 24, 55 }, 2),
                "added, removed, retyped, or reordered units must invalidate the rendered selection identity");
        }
        [TestMethod]
        [TestCategory("Presentation")]
        public void VerifyBriefingGoldPresentation() => TestBriefingGoldPresentation();

        private static void TestBriefingGoldPresentation()
        {
            MethodInfo briefing = typeof(CrusaderDE.MainViewModel).GetMethod(
                "ButtonGotoBriefing",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(object) },
                null);
            Assert(briefing != null && briefing.ReturnType == typeof(void),
                "installed managed baseline retains ButtonGotoBriefing(object)");

            var service = new BriefingGoldPresentationService(null);
            var calls = new List<string>();
            IBriefingGoldPresentationCapability zOwner = service.Bind("z.owner");
            IBriefingGoldPresentationCapability aOwner = service.Bind("a.owner");

            Assert(zOwner.TryRegisterAdjustment(
                "mod",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => { calls.Add("mod"); return context.CurrentGold + 100; },
                out _), "briefing-gold mod adjustment registers");
            Assert(aOwner.TryRegisterAdjustment(
                "vanilla-b",
                BriefingGoldAdjustmentStage.VanillaCorrection,
                context => { calls.Add("vanilla-b"); return context.EffectiveVanillaGold; },
                out _), "briefing-gold Vanilla correction registers");
            Assert(aOwner.TryRegisterAdjustment(
                "vanilla-a",
                BriefingGoldAdjustmentStage.VanillaCorrection,
                context => { calls.Add("vanilla-a"); return context.CurrentGold + 1; },
                out _), "briefing-gold same-owner correction registers");
            Assert(aOwner.TryRegisterAdjustment(
                "failure",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => throw new InvalidOperationException("expected"),
                out _), "briefing-gold throwing adjustment registers");
            Assert(aOwner.TryRegisterAdjustment(
                "negative",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => -1,
                out _), "briefing-gold invalid-result adjustment registers");
            Assert(!aOwner.TryRegisterAdjustment(
                "negative",
                BriefingGoldAdjustmentStage.ModAdjustment,
                context => 0,
                out NativeCapabilityDiagnostic duplicate) &&
                duplicate.State == NativeCapabilityState.Conflict,
                "briefing-gold duplicate owner-local IDs fail closed");

            int human = service.EvaluateForTests(2, true, 2000, true, true);
            Assert(human == 100 && calls.SequenceEqual(new[]
            {
                "vanilla-a", "vanilla-b", "mod"
            }), "briefing-gold adjustments use stage, owner and ID order with failure isolation");

            calls.Clear();
            int ai = service.EvaluateForTests(3, false, 2000, true, true);
            Assert(ai == 2100,
                "No Starting Gold affects the effective human base but leaves AI gold unchanged");
            calls.Clear();
            int unresolved = service.EvaluateForTests(0, true, 4000, false, true);
            Assert(unresolved == 4100,
                "unresolved No Starting Gold state retains Vanilla's displayed base");

            var context = new BriefingGoldContext(4, true, 8000, 0, 0, true, true);
            Assert(context.SlotIndex == 4 && context.IsHuman &&
                context.VanillaDisplayedGold == 8000 && context.EffectiveVanillaGold == 0 &&
                context.CurrentGold == 0 && context.HasNoStartingGoldState &&
                context.NoStartingGoldEnabled,
                "briefing-gold context exposes immutable audited inputs");
        }

    }
}
