using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("current format resolves changed AIV and AIC files", ResolvesChangedFiles);
            Run("legacy snapshot migrates to references", MigratesLegacySnapshot);
            Run("missing files fail safely", MissingFilesFailSafely);
            Run("empty custom selection falls back to built-in", EmptyCustomSelectionFallsBack);
            Run("file-backed selections are detected", DetectsFileBackedSelections);
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
            var target = new FRONT_Multiplayer.MPAIVInfo { imageData = new byte[] { 7 } };
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
            var target = new FRONT_Multiplayer.MPAIVInfo();
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
            var target = new FRONT_Multiplayer.MPAIVInfo();
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

            CustomisationFileManager.CustomLordConfig availableAic = BuildAic(23, 12);
            target = new FRONT_Multiplayer.MPAIVInfo();
            result = AiAivSelectionCodec.Apply(
                snapshot,
                target,
                new List<CustomisationFileManager.CustomAIV>(),
                new List<CustomisationFileManager.CustomLordConfig> { availableAic });
            Assert(result.UsedBuiltInFallback && !result.MissingAic &&
                  target.builtInLord && target.lordConfig == null,
                "built-in AIV fallback retained an incompatible custom AIC");
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
