using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace BugfixesAndQoL
{
    internal static partial class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("current format resolves changed AIV and AIC files", ResolvesChangedFiles);
            Run("legacy snapshot migrates to references", MigratesLegacySnapshot);
            Run("missing files fail safely", MissingFilesFailSafely);
            Run("empty custom selection falls back to built-in", EmptyCustomSelectionFallsBack);
            Run("file-backed selections are detected", DetectsFileBackedSelections);
            Run("custom castle fallback preserves chosen AIC and live slot", CustomCastleFallbackPreservesAicAndIdentity);
            Run("custom AIC fallback preserves selected castles", CustomAicFallbackPreservesCastles);
            Run("legacy custom default flags use current package defaults", RepairsCustomDefaultFlags);
            Run("custom Standard AIC repair keeps two selected castles", RepairsOnlyCustomStandardAic);
            Run("empty custom selections are not normalized or remembered", EmptyCustomSelectionIsNotVanilla);
            Run("identity mismatch is rejected before mutation", RejectsIdentityMismatch);
            Run("repair failure is atomic and never skips the package default", RepairFailureIsAtomic);
            Run("valid custom selection keeps ordering and needs no repair", ValidCustomSelectionIsUnchanged);
            Run("workshop identity keeps identical display and asset names separate", WorkshopIdentityDoesNotAlias);
            Run("Unicode and spaced custom names resolve current files", UnicodeAndSpacedNamesResolve);
            Run("named preset missing castles repairs the correct source", PresetCastleFallbacks);
            Run("named preset Standard AIC preserves two castles", PresetAicFallbackKeepsCastles);
            Run("failed named preset leaves live selection intact", PresetFailureIsAtomic);
            Run("personal data migration preserves all four Bugfixes stores", StorageMigration);
            Console.WriteLine(failures == 0
                ? "BugfixesAndQoL AIV memory tests passed."
                : $"BugfixesAndQoL AIV memory tests failed: {failures}.");
            return failures == 0 ? 0 : 1;
        }

        private static void ResolvesChangedFiles()
        {
            FRONT_Multiplayer.MPAIVInfo source = BuildSource(11, 21);
            string encoded = AiAivSelectionCodec.Encode(source);
            Assert(encoded.StartsWith("v3:", StringComparison.Ordinal), "current prefix missing");
            Assert(AiAivSelectionCodec.TryDecode(
                encoded, out AiAivSelectionSnapshot snapshot, out bool legacy, out string error), error);
            Assert(!legacy, "current payload marked legacy");

            CustomisationFileManager.CustomAIV currentAiv = BuildAiv(101, 99);
            CustomisationFileManager.CustomLordConfig currentAic = BuildAic(201, 98);
            var target = new FRONT_Multiplayer.MPAIVInfo { lordType = 4, imageData = new byte[] { 7 } };
            AiAivSelectionApplyResult result = AiAivSelectionCodec.Apply(
                snapshot,
                target,
                new List<CustomisationFileManager.CustomAIV> { currentAiv },
                new List<CustomisationFileManager.CustomLordConfig> { currentAic });

            Assert(ReferenceEquals(currentAiv, target.aivs[0]), "stored AIV object was reused");
            Assert(target.aivs[0].data[0] == 99, "changed AIV data was not loaded");
            Assert(ReferenceEquals(currentAic, target.lordConfig), "stored AIC object was reused");
            Assert(target.lordConfig.lordData.lord_power_display_level == 98, "changed AIC data was not loaded");
            Assert(target.imageData[0] == 7, "current custom-lord image was replaced");
            Assert(result.LoadedAivs == 1 && result.MissingAivs == 0 && !result.MissingAic,
                "unexpected apply result");
        }

        private static void MigratesLegacySnapshot()
        {
            FRONT_Multiplayer.MPAIVInfo source = BuildSource(12, 22);
            string legacy = EncodeLegacy(source);
            Assert(AiAivSelectionCodec.TryDecode(
                legacy, out AiAivSelectionSnapshot snapshot, out bool wasLegacy, out string error), error);
            Assert(wasLegacy, "legacy payload was not detected");
            string migrated = AiAivSelectionCodec.Encode(snapshot);
            Assert(migrated.StartsWith("v3:", StringComparison.Ordinal), "legacy payload was not normalized");

            CustomisationFileManager.CustomAIV currentAiv = BuildAiv(102, 77);
            var target = new FRONT_Multiplayer.MPAIVInfo { lordType = 4 };
            AiAivSelectionCodec.Apply(
                snapshot,
                target,
                new List<CustomisationFileManager.CustomAIV> { currentAiv },
                new List<CustomisationFileManager.CustomLordConfig> { BuildAic(202, 76) });
            Assert(ReferenceEquals(currentAiv, target.aivs[0]), "legacy migration retained embedded AIV data");
        }

        private static void MissingFilesFailSafely()
        {
            string encoded = AiAivSelectionCodec.Encode(BuildSource(13, 23));
            Assert(AiAivSelectionCodec.TryDecode(
                encoded, out AiAivSelectionSnapshot snapshot, out _, out string error), error);
            var target = new FRONT_Multiplayer.MPAIVInfo { lordType = 4 };
            AiAivSelectionApplyResult result = AiAivSelectionCodec.Apply(
                snapshot,
                target,
                new List<CustomisationFileManager.CustomAIV>(),
                new List<CustomisationFileManager.CustomLordConfig>());
            Assert(target.aivs.Count == 0 && target.builtInLord && target.lordConfig == null,
                "missing files did not fall back safely");
            Assert(target.builtIn && !target.community && !target.historical &&
                result.UsedBuiltInFallback,
                "missing AIV files left an unstartable custom AI slot");
            Assert(result.MissingAivs == 1 && result.MissingAic, "missing files were not reported");
            Assert(result.MissingAiv2 == null && ReferenceEquals(result.MissingAiv1, snapshot.Aivs[0]) &&
                ReferenceEquals(result.MissingAicReference, snapshot.Aic), "Support details must name exactly the unresolved saved references.");

            CustomisationFileManager.CustomLordConfig availableAic = BuildAic(23, 12);
            target = new FRONT_Multiplayer.MPAIVInfo { lordType = 4 };
            result = AiAivSelectionCodec.Apply(
                snapshot,
                target,
                new List<CustomisationFileManager.CustomAIV>(),
                new List<CustomisationFileManager.CustomLordConfig> { availableAic });
            Assert(result.UsedBuiltInFallback && !result.MissingAic &&
                  !target.builtInLord && ReferenceEquals(target.lordConfig, availableAic),
                "castle fallback discarded a separately resolved custom AIC");
            Assert(result.MissingAicReference == null, "Available AIC was falsely reported missing.");
        }

        private static void EmptyCustomSelectionFallsBack()
        {
            var snapshot = new AiAivSelectionSnapshot
            {
                LordType = 0,
                LordName = string.Empty,
                BuiltIn = false,
                Community = false,
                Historical = false,
                BuiltInLord = true
            };
            var target = new FRONT_Multiplayer.MPAIVInfo();
            AiAivSelectionApplyResult result = AiAivSelectionCodec.Apply(
                snapshot,
                target,
                new List<CustomisationFileManager.CustomAIV>(),
                new List<CustomisationFileManager.CustomLordConfig>());

            Assert(target.builtIn && target.aivs.Count == 0 && result.UsedBuiltInFallback,
                "empty legacy selection was restored as an unstartable custom AI");
        }

        private static void DetectsFileBackedSelections()
        {
            Assert(AiAivSelectionCodec.TryDecode(
                AiAivSelectionCodec.Encode(BuildSource(14, 24)),
                out AiAivSelectionSnapshot customFiles,
                out _,
                out string error), error);
            Assert(AiAivSelectionCodec.UsesFileBackedAssets(customFiles),
                "custom AIV/AIC paths were not detected");
            Assert(!AiAivSelectionCodec.ShouldRefreshAssetLists(customFiles, filesChanged: false),
                "unchanged files would trigger a full scan");
            Assert(AiAivSelectionCodec.ShouldRefreshAssetLists(customFiles, filesChanged: true),
                "changed custom files would not trigger a refresh");

            var vanilla = new FRONT_Multiplayer.MPAIVInfo
            {
                lordType = 3,
                lordName = string.Empty,
                builtIn = true,
                builtInLord = true
            };
            Assert(AiAivSelectionCodec.TryDecode(
                AiAivSelectionCodec.Encode(vanilla),
                out AiAivSelectionSnapshot vanillaSnapshot,
                out _,
                out error), error);
            Assert(!AiAivSelectionCodec.UsesFileBackedAssets(vanillaSnapshot),
                "Vanilla-only selection would trigger a file scan");
            Assert(!AiAivSelectionCodec.ShouldRefreshAssetLists(vanillaSnapshot, filesChanged: true),
                "Vanilla-only selection would refresh changed custom files");

            vanillaSnapshot.LordName = "custom-lord";
            Assert(AiAivSelectionCodec.UsesFileBackedAssets(vanillaSnapshot),
                "custom-lord package was not detected");
        }

        private static void CustomCastleFallbackPreservesAicAndIdentity()
        {
            var source = BuildCustom(1);
            var missing = Castle("removed-castle", 10);
            var chosenAic = Configuration("chosen-aic", 20);
            source.aivs.Add(missing);
            source.lordConfig = chosenAic;
            var target = BuildCustom(3);
            target.imageData = new byte[] { 9 };
            var defaultCastle = Castle("package-default", 30);
            var defaultAic = Configuration("package-default", 40);
            AiAivSelectionApplyResult result = AiAivSelectionCodec.Apply(Snapshot(source), target,
                new[] { defaultCastle }, new[] { defaultAic, chosenAic });

            Assert(target.lordType == BuildCustom(3).lordType && target.lordName == source.lordName,
                "the remembered slot replaced the live custom-lord identity");
            Assert(!target.builtIn && !target.community && !target.historical && target.aivs.Count == 1 &&
                ReferenceEquals(target.aivs[0], defaultCastle), "missing castle did not use this package's default");
            Assert(!target.builtInLord && ReferenceEquals(target.lordConfig, chosenAic),
                "castle repair discarded the separately resolved AIC");
            Assert(result.MissingAivs == 1 && !result.MissingAic && !result.UsedBuiltInFallback,
                "custom-package fallback was reported as a Vanilla fallback");
            Assert(target.rotation == source.rotation && target.imageData[0] == 9,
                "repair replaced current presentation or remembered rotation");
        }

        private static void CustomAicFallbackPreservesCastles()
        {
            var source = BuildCustom(1);
            var first = Castle("selected-first", 50);
            var second = Castle("selected-second", 51);
            source.aivs.Add(second);
            source.aivs.Add(first);
            source.lordConfig = Configuration("removed-aic", 52);
            var target = BuildCustom(2);
            var defaultAic = Configuration("package-default", 53);
            AiAivSelectionApplyResult result = AiAivSelectionCodec.Apply(Snapshot(source), target,
                new[] { first, second }, new[] { defaultAic });

            Assert(target.aivs.Count == 2 && ReferenceEquals(target.aivs[0], second) &&
                ReferenceEquals(target.aivs[1], first), "AIC repair changed the chosen castle order");
            Assert(!target.builtInLord && ReferenceEquals(target.lordConfig, defaultAic),
                "missing custom AIC did not use the current package default");
            Assert(result.MissingAivs == 0 && result.MissingAic && !result.UsedBuiltInFallback,
                "AIC-only repair reported unrelated missing castles or Vanilla fallback");
        }

        private static void RepairsCustomDefaultFlags()
        {
            var source = BuildCustom(1);
            source.builtIn = true;
            source.builtInLord = true;
            source.aivs.Add(Castle("old-chosen", 60));
            source.aivs.Add(Castle("second-old-chosen", 63));
            var target = BuildCustom(2);
            var defaultCastle = Castle("package-first", 61);
            var defaultAic = Configuration("package-first", 62);
            AiAivSelectionCodec.Apply(Snapshot(source), target,
                new[] { defaultCastle, source.aivs[0], source.aivs[1] }, new[] { defaultAic });
            Assert(!target.builtIn && !target.community && !target.historical && target.aivs.Count == 1 &&
                ReferenceEquals(target.aivs[0], defaultCastle), "custom default flag retained a nonexistent Vanilla castle source");
            Assert(!target.builtInLord && ReferenceEquals(target.lordConfig, defaultAic),
                "custom default AIC flag retained a nonexistent Vanilla configuration");
        }

        private static void EmptyCustomSelectionIsNotVanilla()
        {
            var source = BuildCustom(1);
            source.lordConfig = Configuration("chosen-aic", 70);
            AiAivSelectionSnapshot snapshot = Snapshot(source);
            Assert(!snapshot.BuiltIn, "serializing an empty named custom selection turned on Vanilla castles");
            Assert(!AiSelectionRepair.CanRemember(source), "an incomplete custom selection can overwrite a valid memory");
            source.aivs.Add(Castle("chosen-castle", 71));
            Assert(AiSelectionRepair.CanRemember(source), "a complete explicit custom selection cannot be remembered");
            source.builtInLord = true;
            Assert(!AiSelectionRepair.CanRemember(source), "a custom selection without a valid explicit AIC can be remembered");
        }

        private static void RepairsOnlyCustomStandardAic()
        {
            // This is the supplied Champion memory's shape: explicit castles, Standard AIC, no AIC reference.
            var source = BuildCustom(1);
            var first = Castle("chosen-first", 65);
            var second = Castle("chosen-second", 66);
            source.aivs.Add(first);
            source.aivs.Add(second);
            source.builtInLord = true;
            var defaultAic = Configuration("package-aic", 67);
            var target = BuildCustom(2);
            var result = AiAivSelectionCodec.Apply(Snapshot(source), target,
                new[] { second, first }, new[] { defaultAic });
            Assert(target.aivs.Count == 2 && ReferenceEquals(target.aivs[0], first) &&
                ReferenceEquals(target.aivs[1], second), "Standard AIC repair replaced the complete castle selection");
            Assert(!target.builtIn && !target.builtInLord && ReferenceEquals(target.lordConfig, defaultAic) &&
                !result.UsedBuiltInFallback, "Standard AIC repair did not restore the package configuration independently");
        }

        private static void RejectsIdentityMismatch()
        {
            var source = BuildCustom(1);
            source.lordName = "another-lord";
            source.aivs.Add(Castle("other-castle", 80));
            source.lordConfig = Configuration("other-aic", 81);
            var target = BuildCustom(2);
            var castle = Castle("current-castle", 82);
            var aic = Configuration("current-aic", 83);
            target.aivs.Add(castle);
            target.lordConfig = aic;
            bool rejected = false;
            try { AiAivSelectionCodec.Apply(Snapshot(source), target, new[] { castle }, new[] { aic }); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && target.lordName == "test-custom-lord" && target.lordType == BuildCustom(2).lordType &&
                target.aivs.Count == 1 && ReferenceEquals(target.aivs[0], castle) && ReferenceEquals(target.lordConfig, aic),
                "a mismatched stored Lord partially replaced the current selection");

            var builtinTarget = new FRONT_Multiplayer.MPAIVInfo { lordType = 3 };
            rejected = false;
            try { AiAivSelectionCodec.Apply(Snapshot(BuildSource(84, 85)), builtinTarget,
                new CustomisationFileManager.CustomAIV[0], new CustomisationFileManager.CustomLordConfig[0]); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && builtinTarget.lordType == 3 && builtinTarget.builtIn && builtinTarget.aivs.Count == 0,
                "a mismatched builtin type was restored into the wrong Lord");
        }

        private static void RepairFailureIsAtomic()
        {
            // A later valid file must not silently replace Vanilla's exact first-package-file default.
            var validCastle = Castle("second-castle", 90);
            var validAic = Configuration("second-aic", 91);
            var invalidCastle = Castle("invalid-first", 92);
            invalidCastle.data = null;
            var info = BuildCustom(1);
            info.builtIn = true;
            info.builtInLord = true;
            info.aivs.Add(validCastle);
            info.lordConfig = validAic;
            Assert(!AiSelectionRepair.TryRepair(info, new[] { invalidCastle, validCastle }, new[] { validAic },
                out AiSelectionRepairResult failedCastle) && !string.IsNullOrEmpty(failedCastle.Failure),
                "repair skipped an unusable package-default castle");
            Assert(info.builtIn && info.builtInLord && info.aivs.Count == 1 &&
                ReferenceEquals(info.aivs[0], validCastle) && ReferenceEquals(info.lordConfig, validAic),
                "failed castle repair changed the original selection");

            Assert(!AiSelectionRepair.TryRepair(info, new[] { validCastle },
                new CustomisationFileManager.CustomLordConfig[] { null, validAic }, out AiSelectionRepairResult failedAic) &&
                !string.IsNullOrEmpty(failedAic.Failure), "repair skipped an unusable package-default AIC");
            Assert(info.builtIn && info.builtInLord && ReferenceEquals(info.aivs[0], validCastle),
                "failed AIC repair committed the preceding castle repair");

            var source = BuildCustom(2);
            source.builtIn = true;
            source.builtInLord = true;
            bool rejected = false;
            try { AiAivSelectionCodec.Apply(Snapshot(source), info, new[] { validCastle },
                new CustomisationFileManager.CustomLordConfig[0]); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && info.builtIn && info.builtInLord && info.lordType == BuildCustom(1).lordType &&
                ReferenceEquals(info.aivs[0], validCastle) && ReferenceEquals(info.lordConfig, validAic),
                "failed memory restore partially committed its candidate selection");
        }

        private static void ValidCustomSelectionIsUnchanged()
        {
            var info = BuildCustom(1);
            var first = Castle("first", 100);
            var second = Castle("second", 101);
            var aic = Configuration("chosen", 102);
            info.aivs.Add(second);
            info.aivs.Add(first);
            info.lordConfig = aic;
            Assert(AiSelectionRepair.TryRepair(info, new[] { first, second }, new[] { aic }, out AiSelectionRepairResult result),
                "valid custom selection was rejected");
            Assert(!result.Changed && !result.CastleFallback && !result.AicFallback &&
                info.aivs.Count == 2 && ReferenceEquals(info.aivs[0], second) && ReferenceEquals(info.aivs[1], first) &&
                ReferenceEquals(info.lordConfig, aic), "valid custom selection was repaired or reordered unnecessarily");
            Assert(AiSelectionRepair.TryRepair(info, null, null, out result) && !result.Changed,
                "complete embedded custom data incorrectly requires the original local package");
        }

        // Apply has no storage dependency. Bypass its loading constructor so these behavior tests
        // cannot read or write a personal preset directory; a future dependency change must update the fixture.
        private static AivAicPresetStore PresetApplicator() =>
            (AivAicPresetStore)FormatterServices.GetUninitializedObject(typeof(AivAicPresetStore));

        private static void PresetCastleFallbacks()
        {
            var preset = new AivAicPresetDefinition { Rotation = 2 };
            preset.Aivs.Add(AivAicPresetStore.CaptureAiv(Castle("removed", 130)));
            var chosenAic = Configuration("chosen", 131);
            preset.Aic = AivAicPresetStore.CaptureAic(chosenAic);
            var builtin = new FRONT_Multiplayer.MPAIVInfo { lordType = 4 };
            var store = PresetApplicator();
            var builtinResult = store.Apply(preset, builtin, new CustomisationFileManager.CustomAIV[0],
                new[] { chosenAic }, 8);
            Assert(builtin.builtIn && builtin.aivs.Count == 0 && !builtin.builtInLord &&
                ReferenceEquals(builtin.lordConfig, chosenAic) && builtin.rotation == 2 &&
                builtinResult.MissingAivs == 1 && builtinResult.Repaired,
                "builtin preset castle fallback lost its independently selected AIC");

            var custom = BuildCustom(1);
            var defaultCastle = Castle("current-default", 132);
            var customResult = store.Apply(preset, custom, new[] { defaultCastle }, new[] { chosenAic }, 8);
            Assert(!custom.builtIn && custom.aivs.Count == 1 && ReferenceEquals(custom.aivs[0], defaultCastle) &&
                !custom.builtInLord && ReferenceEquals(custom.lordConfig, chosenAic) &&
                customResult.MissingAivs == 1 && customResult.Repaired,
                "custom preset did not repair the castle source without losing the AIC");
        }

        private static void PresetAicFallbackKeepsCastles()
        {
            var first = Castle("preset-first", 140);
            var second = Castle("preset-second", 141);
            var defaultAic = Configuration("package-default", 142);
            var preset = new AivAicPresetDefinition { UseVanillaAic = true, Rotation = 1 };
            preset.Aivs.Add(AivAicPresetStore.CaptureAiv(second));
            preset.Aivs.Add(AivAicPresetStore.CaptureAiv(first));
            var target = BuildCustom(2);
            var result = PresetApplicator().Apply(preset, target, new[] { first, second }, new[] { defaultAic }, 8);
            Assert(!target.builtIn && target.aivs.Count == 2 && ReferenceEquals(target.aivs[0], second) &&
                ReferenceEquals(target.aivs[1], first) && !target.builtInLord &&
                ReferenceEquals(target.lordConfig, defaultAic) && target.rotation == 1 && result.Repaired,
                "preset Standard AIC discarded or reordered the explicit custom castles");
        }

        private static void PresetFailureIsAtomic()
        {
            var target = BuildCustom(3);
            var oldCastle = Castle("current", 150);
            var oldAic = Configuration("current", 151);
            target.aivs.Add(oldCastle);
            target.lordConfig = oldAic;
            target.imageData = new byte[] { 8 };
            var preset = new AivAicPresetDefinition { UseVanillaAic = true, Rotation = 1 };
            bool rejected = false;
            try { PresetApplicator().Apply(preset, target, new[] { oldCastle },
                new CustomisationFileManager.CustomLordConfig[0], 8); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && target.lordType == BuildCustom(3).lordType && target.lordName == "test-custom-lord" &&
                !target.builtIn && !target.builtInLord && target.rotation == 3 && target.imageData[0] == 8 &&
                target.aivs.Count == 1 && ReferenceEquals(target.aivs[0], oldCastle) && ReferenceEquals(target.lordConfig, oldAic),
                "failed preset committed the castle repair or replaced live identity/presentation");
        }

        private static FRONT_Multiplayer.MPAIVInfo BuildCustom(int slot) => new FRONT_Multiplayer.MPAIVInfo
        {
            lordType = Shared.CoopAiSelectionRules.FirstCustomLordType + slot,
            lordName = "test-custom-lord",
            builtIn = false,
            builtInLord = false,
            rotation = 3
        };

        private static void WorkshopIdentityDoesNotAlias()
        {
            var first = BuildCustom(1);
            first.lordName = @"1001\Same Display Name";
            var firstCastle = Castle("same castle filename", 110);
            firstCastle.path = @"C:\Workshop\1001\Same Display Name";
            var firstAic = Configuration("same AIC filename", 111);
            firstAic.path = firstCastle.path;
            first.aivs.Add(firstCastle);
            first.lordConfig = firstAic;

            var second = BuildCustom(2);
            second.lordName = @"1002\Same Display Name";
            // Identical filenames and payload checksums are legal in distinct Workshop packages.
            var secondCastle = Castle(firstCastle.AIVName, firstCastle.checksum);
            secondCastle.path = @"C:\Workshop\1002\Same Display Name";
            var secondAic = Configuration(firstAic.name, firstAic.checksum);
            secondAic.path = secondCastle.path;
            second.aivs.Add(secondCastle);
            second.lordConfig = secondAic;
            Assert(AiAivSelectionCodec.BuildLordKey(Snapshot(first)) != AiAivSelectionCodec.BuildLordKey(Snapshot(second)),
                "the memory key discarded the Workshop package identity");
            bool rejected = false;
            try { AiAivSelectionCodec.Apply(Snapshot(first), second, new[] { secondCastle }, new[] { secondAic }); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && ReferenceEquals(second.aivs[0], secondCastle) && ReferenceEquals(second.lordConfig, secondAic),
                "same display name allowed another Workshop Lord's snapshot to mutate this slot");

            var restored = BuildCustom(3);
            restored.lordName = second.lordName;
            AiAivSelectionCodec.Apply(Snapshot(second), restored, new[] { secondCastle }, new[] { secondAic });
            Assert(ReferenceEquals(restored.aivs[0], secondCastle) && ReferenceEquals(restored.lordConfig, secondAic),
                "restoration did not use the supplied current Lord's own asset list");
        }

        private static void UnicodeAndSpacedNamesResolve()
        {
            var source = BuildCustom(1);
            source.lordName = @"314159\Äbtissin 王";
            var oldCastle = Castle("Burg № 1 沙", 120);
            oldCastle.path = @"C:\Mods mit Leerzeichen\Äbtissin 王";
            var oldAic = Configuration("Diplomat König 王", 121);
            oldAic.path = oldCastle.path;
            source.aivs.Add(oldCastle);
            source.lordConfig = oldAic;
            var currentCastle = Castle(oldCastle.AIVName, 122);
            currentCastle.path = oldCastle.path;
            var currentAic = Configuration(oldAic.name, 123);
            currentAic.path = oldAic.path;
            var target = BuildCustom(3);
            target.lordName = @"314159\ÄBTISSIN 王";
            AiAivSelectionCodec.Apply(Snapshot(source), target, new[] { currentCastle }, new[] { currentAic });
            Assert(target.lordName == @"314159\ÄBTISSIN 王" && target.lordType == BuildCustom(3).lordType &&
                ReferenceEquals(target.aivs[0], currentCastle) && ReferenceEquals(target.lordConfig, currentAic),
                "Unicode/spaced names lost the live identity or failed to resolve changed files at their current path");
        }

        private static AiAivSelectionSnapshot Snapshot(FRONT_Multiplayer.MPAIVInfo info)
        {
            Assert(AiAivSelectionCodec.TryDecode(AiAivSelectionCodec.Encode(info), out AiAivSelectionSnapshot snapshot,
                out _, out string error), error);
            return snapshot;
        }

        private static CustomisationFileManager.CustomAIV Castle(string name, ulong checksum)
        {
            var castle = BuildAiv(checksum, 1);
            castle.AIVName = name;
            return castle;
        }

        private static CustomisationFileManager.CustomLordConfig Configuration(string name, ulong checksum)
        {
            var aic = BuildAic(checksum, 2);
            aic.name = name;
            return aic;
        }

        private static FRONT_Multiplayer.MPAIVInfo BuildSource(ulong aivChecksum, ulong aicChecksum)
        {
            var info = new FRONT_Multiplayer.MPAIVInfo
            {
                lordType = 4,
                lordName = string.Empty,
                builtIn = false,
                rotation = 3,
                builtInLord = false,
                lordConfig = BuildAic(aicChecksum, 2)
            };
            info.aivs.Add(BuildAiv(aivChecksum, 1));
            return info;
        }

        private static CustomisationFileManager.CustomAIV BuildAiv(ulong checksum, short value) =>
            new CustomisationFileManager.CustomAIV
            {
                lordType = 4,
                AIVName = "remembered-castle",
                path = @"C:\CurrentLord",
                checksum = checksum,
                data = new[] { value }
            };

        private static CustomisationFileManager.CustomLordConfig BuildAic(ulong checksum, int power)
        {
            var data = new EngineInterface.AILordConfigTransferData();
            data.lord_power_display_level = power;
            return new CustomisationFileManager.CustomLordConfig
            {
                lordType = 4,
                name = "remembered-config",
                path = @"C:\CurrentLord",
                checksum = checksum,
                lordData = data
            };
        }

        private static string EncodeLegacy(FRONT_Multiplayer.MPAIVInfo info)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(2);
                writer.Write(info.lordType);
                WriteString(writer, info.lordName);
                writer.Write(info.builtIn);
                writer.Write(info.community);
                writer.Write(info.historical);
                writer.Write(info.rotation);
                writer.Write(info.builtInLord);
                WriteBytes(writer, info.lordConfig.encode());
                writer.Write(info.lordConfig.workshop);
                writer.Write(info.lordConfig.workshopUploadInfoAvailable);
                WriteString(writer, info.lordConfig.path);
                WriteBytes(writer, null);
                writer.Write(info.aivs.Count);
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    WriteBytes(writer, aiv.encode());
                    writer.Write(aiv.workshop);
                    writer.Write(aiv.workshopUploadInfoAvailable);
                    WriteString(writer, aiv.path);
                }
                writer.Flush();
                return "v2:" + Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteBytes(BinaryWriter writer, byte[] value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }
            writer.Write(value.Length);
            writer.Write(value);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
