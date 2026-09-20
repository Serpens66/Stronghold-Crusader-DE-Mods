using Iced.Intel;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace SkirmishGameOptionsTest
{
    internal static class Program
    {
        private static int assertions;

        private static int Main()
        {
            TestWorkingCopyRouting();
            TestWorkingCopyApplyAndCancel();
            TestPeaceTimeSynchronization();
            TestGameplayGuards();
            TestAdvancedFlagConversion();
            TestDuplicatedDisplayPolicies();
            TestNoDogsNativeContract();
            TestXamlPatchContract();
            TestInstalledNativeBinary();
            Console.WriteLine(
                $"SkirmishGameOptionsTest tests: {assertions} assertions passed.");
            return 0;
        }

        private static void TestWorkingCopyApplyAndCancel()
        {
            var committed = TestSettings.CreateDefaults();
            var transaction = new WorkingCopyTransaction<TestSettings>(CloneTestSettings);
            TestSettings working = transaction.Begin(committed);
            Assert(TestSettingsEqual(working, committed),
                "opening creates a complete working copy");
            Assert(!ReferenceEquals(working.Buildings, committed.Buildings) &&
                   !ReferenceEquals(working.Goods, committed.Goods) &&
                   !ReferenceEquals(working.Troops, committed.Troops),
                "working-copy availability arrays are independent");

            working.Fairness = 4;
            working.StartingGoods = 3;
            working.WinCondition = 2;
            working.StrongWalls = 1;
            working.NoCows = 1;
            working.NoDogs = 1;
            working.AllowAutoTrading = 0;
            working.AutoSave = 20;
            working.GameSpeed = 55;
            working.PeaceTime = 17;
            working.ExtremeTroops = 1;
            working.ExtremePowers = 1;
            working.ExtremePowersAroundLord = 1;
            working.AllowOutposts = 0;
            working.Buildings[1] = 0;
            working.Goods[2] = 0;
            working.Troops[0] = 0;
            working.Gameplay.ImprovedSieging = 1;
            Assert(committed.Fairness == 0 && committed.PeaceTime == 0 &&
                   committed.Buildings[1] == 1 && committed.Goods[2] == 1 &&
                   committed.Troops[0] == 1,
                "dialog edits do not mutate committed settings before Apply");
            transaction.ApplyTo(committed, CopyTestSettings);

            Assert(committed.Fairness == 4, "Apply synchronizes fairness");
            Assert(committed.StartingGoods == 3, "Apply synchronizes starting goods");
            Assert(committed.WinCondition == 2, "Apply synchronizes the win condition");
            Assert(committed.StrongWalls == 1, "Apply synchronizes Strong Walls");
            Assert(committed.NoCows == 1, "Apply synchronizes No Cows");
            Assert(committed.NoDogs == 1, "Apply synchronizes No Dogs");
            Assert(committed.AllowAutoTrading == 0, "Apply synchronizes auto trading");
            Assert(committed.AutoSave == 20, "Apply synchronizes autosave");
            Assert(committed.GameSpeed == 55, "Apply synchronizes game speed");
            Assert(committed.PeaceTime == 17, "Apply synchronizes Peace Time");
            Assert(committed.ExtremeTroops == 1, "Apply synchronizes Extreme Troops");
            Assert(committed.ExtremePowers == 1, "Apply synchronizes Extreme Powers");
            Assert(committed.ExtremePowersAroundLord == 1,
                "Apply synchronizes Extreme Powers Around Lord");
            Assert(committed.AllowOutposts == 0, "Apply synchronizes outposts");
            Assert(committed.Buildings[1] == 0, "Apply synchronizes building availability");
            Assert(committed.Goods[2] == 0, "Apply synchronizes goods availability");
            Assert(committed.Troops[0] == 0, "Apply synchronizes troop availability");
            Assert(committed.Gameplay.ImprovedSieging == 1,
                "Apply synchronizes gameplay options");
            Assert(transaction.Working == null, "Apply closes the transaction");

            TestSettings beforeCancel = CloneTestSettings(committed);
            working = transaction.Begin(committed);
            working.Fairness = 0;
            working.StartingGoods = 0;
            working.WinCondition = 0;
            working.StrongWalls = 0;
            working.PeaceTime = 2;
            working.Buildings[1] = 1;
            working.Goods[2] = 1;
            working.Troops[0] = 1;
            working.Gameplay.ImprovedSieging = 0;
            transaction.Cancel();

            Assert(TestSettingsEqual(committed, beforeCancel),
                "Cancel preserves every committed general, array and gameplay value");
            Assert(transaction.Working == null, "Cancel closes the transaction");
        }

        private static void TestPeaceTimeSynchronization()
        {
            var committed = TestSettings.CreateDefaults();
            committed.PeaceTime = 5;
            var transaction = new WorkingCopyTransaction<TestSettings>(CloneTestSettings);
            TestSettings working = transaction.Begin(committed);

            int extraFeaturesPeaceTime = 12;
            committed.PeaceTime = extraFeaturesPeaceTime;
            working.PeaceTime = extraFeaturesPeaceTime;
            Assert(committed.PeaceTime == 12 && working.PeaceTime == 12,
                "ExtraFeatures Peace Time updates committed and open working copies");

            working.PeaceTime = 18;
            Assert(extraFeaturesPeaceTime == 12,
                "dialog Peace Time does not update ExtraFeatures before Apply");
            transaction.Cancel();
            Assert(extraFeaturesPeaceTime == 12 && committed.PeaceTime == 12,
                "Cancel preserves the external and committed Peace Time");

            working = transaction.Begin(committed);
            working.PeaceTime = 22;
            transaction.ApplyTo(committed, CopyTestSettings);
            extraFeaturesPeaceTime = committed.PeaceTime;
            Assert(extraFeaturesPeaceTime == 22,
                "Apply propagates dialog Peace Time to ExtraFeatures");
        }

        private static void TestWorkingCopyRouting()
        {
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Fairness3"),
                "fairness is transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("GameType2"),
                "starting goods are transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Settings_ExtremePowers"),
                "extreme powers are transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Settings_Adv_NoGold"),
                "gameplay options are transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("STRUCT_CHURCH"),
                "building availability is transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("TROOPS_31"),
                "troop availability is transactional");
            Assert(SkirmishGameOptionsPolicy.IsWorkingCopyCommand("GOODS_24"),
                "trade availability is transactional");
            Assert(!SkirmishGameOptionsPolicy.IsWorkingCopyCommand("Setup"),
                "open has dedicated handling");
            Assert(!SkirmishGameOptionsPolicy.IsWorkingCopyCommand("ApplySettings"),
                "apply has dedicated handling");
            Assert(!SkirmishGameOptionsPolicy.IsWorkingCopyCommand("CancelSettings"),
                "cancel has dedicated handling");
        }

        private static void TestGameplayGuards()
        {
            Assert(SkirmishGameOptionsPolicy.ShouldBlockCow(true, true),
                "cow action blocked in local Skirmish");
            Assert(!SkirmishGameOptionsPolicy.ShouldBlockCow(false, true),
                "cow action untouched outside local Skirmish");
            Assert(SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(true, false),
                "auto trading blocked in local Skirmish");
            Assert(!SkirmishGameOptionsPolicy.ShouldBlockAutoTrading(true, true),
                "allowed auto trading remains available");
            Assert(!SkirmishGameOptionsPolicy.ShouldAllowNoDogsToggle(false),
                "No Dogs input is rejected without the validated native patch");
            Assert(SkirmishGameOptionsPolicy.ShouldAllowNoDogsToggle(true),
                "No Dogs input is accepted with the validated native patch");
            Assert(!SkirmishGameOptionsPolicy.ShouldAllowOutpostToggle(true, false),
                "outpost input is rejected on unsupported Skirmish maps");
            Assert(SkirmishGameOptionsPolicy.ShouldAllowOutpostToggle(true, true),
                "outpost input is accepted on supported Skirmish maps");
            Assert(SkirmishGameOptionsPolicy.ShouldAllowOutpostToggle(false, false),
                "outpost input outside local Skirmish remains Vanilla-owned");
        }

        private static void TestAdvancedFlagConversion()
        {
            SkirmishGameOptionsPolicy.AdvancedState state = DefaultAdvancedState();
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 0,
                "default advanced settings normalize to disabled");

            state.Buildings[1] = 0;
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 1,
                "building-only restrictions enable the mode-99 advanced flag");
            state = DefaultAdvancedState();
            state.Goods[1] = 0;
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 1,
                "goods-only restrictions enable the mode-99 advanced flag");
            state = DefaultAdvancedState();
            state.Troops[1] = 0;
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 1,
                "troop-only restrictions enable the mode-99 advanced flag");

            AssertGameplayEnablesAdvanced(s => s.PreBuild = 1, "pre-build");
            AssertGameplayEnablesAdvanced(s => s.ImprovedArabSwordsmen = 1,
                "improved Arab swordsmen");
            AssertGameplayEnablesAdvanced(s => s.ImprovedLaddermen = 1,
                "improved laddermen");
            AssertGameplayEnablesAdvanced(s => s.ImprovedSpearmen = 1,
                "improved spearmen");
            AssertGameplayEnablesAdvanced(s => s.RebalancedHorseArchers = 1,
                "rebalanced horse archers");
            AssertGameplayEnablesAdvanced(s => s.ImprovedFletchers = 1,
                "improved fletchers");
            AssertGameplayEnablesAdvanced(s => s.UncappedPeasants = 1,
                "uncapped peasants");
            AssertGameplayEnablesAdvanced(s => s.FasterPeasants = 1,
                "faster peasants");
            AssertGameplayEnablesAdvanced(s => s.EnemyHitPoints = 2,
                "enemy hit points");
            AssertGameplayEnablesAdvanced(s => s.ImprovedSieging = 1,
                "first improved-sieging field omitted by Vanilla normalization");
            AssertGameplayEnablesAdvanced(s => s.ImprovedSieging2 = 1,
                "second improved-sieging field omitted by Vanilla normalization");
            AssertGameplayEnablesAdvanced(s => s.Healers = 1, "healers");
            AssertGameplayEnablesAdvanced(s => s.Eunuchs = 1, "eunuchs");
            AssertGameplayEnablesAdvanced(s => s.NoGold = 1, "no gold");

            state = DefaultAdvancedState();
            state.ImprovedSieging = 1;
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(false, state) == 0,
                "unchecked advanced master switch disables configured options");
        }

        private static void TestDuplicatedDisplayPolicies()
        {
            SkirmishGameOptionsPolicy.AdvancedState state = DefaultAdvancedState();
            state.Goods[0] = 0;
            Assert(SkirmishGameOptionsPolicy.ShouldShowAdvancedIndicator(1, state),
                "array-only restrictions drive the existing Advanced indicator");
            Assert(!SkirmishGameOptionsPolicy.ShouldShowAdvancedIndicator(0, state),
                "disabled advanced state clears the existing Advanced indicator");
            Assert(SkirmishGameOptionsPolicy.GetExtremeTroopsOpacity(true) == 1f,
                "Extreme Troops is fully enabled in the new Skirmish dialog");
            Assert(SkirmishGameOptionsPolicy.GetOutpostOpacity(true) == 1f,
                "supported maps show an enabled outpost control");
            Assert(SkirmishGameOptionsPolicy.GetOutpostOpacity(false) == 0.3f,
                "unsupported maps show a disabled outpost control");
        }

        private static void AssertGameplayEnablesAdvanced(
            Action<SkirmishGameOptionsPolicy.AdvancedState> configure,
            string name)
        {
            SkirmishGameOptionsPolicy.AdvancedState state = DefaultAdvancedState();
            configure(state);
            Assert(SkirmishGameOptionsPolicy.ToSkirmishAdvancedFlag(true, state) == 1,
                name + " enables the mode-99 advanced flag");
        }

        private static SkirmishGameOptionsPolicy.AdvancedState DefaultAdvancedState() =>
            new SkirmishGameOptionsPolicy.AdvancedState
            {
                Buildings = new[] { 1, 1, 1 },
                Goods = new[] { 1, 1, 1 },
                Troops = new[] { 1, 1, 1 },
                EnemyHitPoints = 1
            };

        private static void TestNoDogsNativeContract()
        {
            byte[] image = new byte[NoDogsNativeContract.PatchRva + 16];
            Array.Copy(
                NoDogsNativeContract.ExpectedBytes,
                0,
                image,
                NoDogsNativeContract.PatchRva,
                NoDogsNativeContract.ExpectedBytes.Length);
            NoDogsNativeContract.Validate(
                image,
                NoDogsNativeContract.PreferredImageBase,
                referenceHashMatches: true);
            assertions++;

            var vanillaDecoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(NoDogsNativeContract.ExpectedBytes));
            vanillaDecoder.IP = NoDogsNativeContract.PreferredImageBase +
                NoDogsNativeContract.PatchRva;
            Assert(vanillaDecoder.Decode().FlowControl == FlowControl.ConditionalBranch,
                "Vanilla bytes decode as the audited mode-99 bypass branch");

            var patchedDecoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(NoDogsNativeContract.ReplacementBytes));
            patchedDecoder.IP = NoDogsNativeContract.PreferredImageBase +
                NoDogsNativeContract.PatchRva;
            Instruction first = patchedDecoder.Decode();
            Instruction second = patchedDecoder.Decode();
            Assert(first.Mnemonic == Mnemonic.Nop && second.Mnemonic == Mnemonic.Nop,
                "replacement decodes completely as two one-byte NOPs");

            var assembler = new Assembler(64);
            NoDogsNativeContract.EmitReplacement(assembler);
            byte[] assembledBytes;
            using (var stream = new MemoryStream())
            {
                var writer = new StreamCodeWriter(stream);
                if (!assembler.TryAssemble(
                    writer,
                    NoDogsNativeContract.PreferredImageBase + NoDogsNativeContract.PatchRva,
                    out string errorMessage,
                    out _,
                    BlockEncoderOptions.None))
                {
                    throw new InvalidOperationException(
                        "Iced failed to assemble the No Dogs replacement: " + errorMessage);
                }
                assembledBytes = stream.ToArray();
            }
            Assert(assembledBytes.SequenceEqual(NoDogsNativeContract.ReplacementBytes),
                "the installed Iced backend emits the audited 90-90 replacement");

            image[NoDogsNativeContract.PatchRva] = 0x75;
            AssertThrows(
                () => NoDogsNativeContract.Validate(
                    image,
                    NoDogsNativeContract.PreferredImageBase,
                    referenceHashMatches: true),
                "changed native bytes fail closed");
            AssertThrows(
                () => NoDogsNativeContract.Validate(
                    image,
                    NoDogsNativeContract.PreferredImageBase,
                    referenceHashMatches: false),
                "unknown native hash fails closed");
        }

        private static void TestXamlPatchContract()
        {
            string patchPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                @"..\..\Patches\Assets\GUI\XAMLResources\FRONT_Multiplayer.xaml"));
            Assert(File.Exists(patchPath), "FRONT_Multiplayer XAML patch exists");

            XDocument document = XDocument.Load(patchPath);
            XElement zIndexOperation = document.Root
                .Elements("Operation")
                .Single(element =>
                    (string)element.Attribute("Type") == "SetAttribute" &&
                    (string)element.Attribute("AttributeName") == "Panel.ZIndex");
            Assert((string)zIndexOperation.Attribute("Value") == "1000",
                "Game Options host is rendered above the Skirmish panel");
            Assert(((string)zIndexOperation.Attribute("XPath"))?.Contains("Show_MPSettings") == true,
                "Z-index patch targets the Vanilla Game Options host");

            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            XElement settingsButton = document
                .Descendants()
                .Single(element =>
                    element.Name.LocalName == "Button" &&
                    (string)element.Attribute(xaml + "Name") ==
                        "SkirmishGameOptionsTestButton");
            Assert((string)settingsButton.Attribute("Width") == "280",
                "Settings button retains its Vanilla-compatible width");
            Assert((string)settingsButton.Attribute("Margin") == "52,5,0,0",
                "Settings button is centered above the upper-left fairness bar");
            Assert((string)settingsButton.Attribute("CommandParameter") == "Setup",
                "Settings button still opens Vanilla Game Options");
            Assert(((string)settingsButton.Attribute("Visibility"))?.Contains(
                "SkirmishSetupMode") == true,
                "Settings button remains restricted to the Skirmish setup UI");
        }

        private static void TestInstalledNativeBinary()
        {
            string gameDirectory = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string nativePath = Path.Combine(
                gameDirectory,
                @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
            Assert(File.Exists(nativePath), "installed native DLL exists");

            byte[] file = File.ReadAllBytes(nativePath);
            string hash;
            using (SHA256 sha256 = SHA256.Create())
                hash = BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", string.Empty);
            Assert(
                string.Equals(hash, NoDogsNativeContract.ReferenceSha256, StringComparison.Ordinal),
                "installed native DLL hash matches audited baseline");

            int fileOffset = RvaToFileOffset(file, NoDogsNativeContract.PatchRva);
            byte[] bytes = new byte[NoDogsNativeContract.ExpectedBytes.Length];
            Array.Copy(file, fileOffset, bytes, 0, bytes.Length);
            Assert(
                bytes[0] == NoDogsNativeContract.ExpectedBytes[0] &&
                bytes[1] == NoDogsNativeContract.ExpectedBytes[1],
                "installed native byte window matches audited No Dogs branch");

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = NoDogsNativeContract.PreferredImageBase + NoDogsNativeContract.PatchRva;
            Instruction instruction = decoder.Decode();
            Assert(
                instruction.Code == Code.Je_rel8_64 && instruction.Length == bytes.Length,
                "installed native bytes decode as one complete JE rel8 instruction");
        }

        private static int RvaToFileOffset(byte[] image, int rva)
        {
            using (var stream = new MemoryStream(image, writable: false))
            using (var reader = new BinaryReader(stream))
            {
                stream.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550)
                    throw new InvalidOperationException("Installed native DLL has no PE signature.");

                stream.Position = peOffset + 6;
                ushort sectionCount = reader.ReadUInt16();
                stream.Position = peOffset + 20;
                ushort optionalHeaderSize = reader.ReadUInt16();
                long sectionTable = peOffset + 24L + optionalHeaderSize;

                for (int section = 0; section < sectionCount; section++)
                {
                    stream.Position = sectionTable + section * 40L + 8;
                    uint virtualSize = reader.ReadUInt32();
                    uint virtualAddress = reader.ReadUInt32();
                    uint rawSize = reader.ReadUInt32();
                    uint rawPointer = reader.ReadUInt32();
                    uint mappedSize = Math.Max(virtualSize, rawSize);
                    if ((uint)rva >= virtualAddress && (uint)rva < virtualAddress + mappedSize)
                        return checked((int)(rawPointer + (uint)rva - virtualAddress));
                }
            }

            throw new InvalidOperationException($"RVA 0x{rva:X} is outside the installed PE sections.");
        }

        private static TestSettings CloneTestSettings(TestSettings source)
        {
            var clone = TestSettings.CreateDefaults();
            CopyTestSettings(source, clone);
            return clone;
        }

        private static void CopyTestSettings(TestSettings source, TestSettings target)
        {
            target.Fairness = source.Fairness;
            target.StartingGoods = source.StartingGoods;
            target.WinCondition = source.WinCondition;
            target.StrongWalls = source.StrongWalls;
            target.NoCows = source.NoCows;
            target.NoDogs = source.NoDogs;
            target.AllowAutoTrading = source.AllowAutoTrading;
            target.AutoSave = source.AutoSave;
            target.GameSpeed = source.GameSpeed;
            target.PeaceTime = source.PeaceTime;
            target.ExtremeTroops = source.ExtremeTroops;
            target.ExtremePowers = source.ExtremePowers;
            target.ExtremePowersAroundLord = source.ExtremePowersAroundLord;
            target.AllowOutposts = source.AllowOutposts;
            target.Buildings = (int[])source.Buildings.Clone();
            target.Goods = (int[])source.Goods.Clone();
            target.Troops = (int[])source.Troops.Clone();
            target.Gameplay = new SkirmishGameOptionsPolicy.AdvancedState
            {
                Buildings = (int[])source.Gameplay.Buildings.Clone(),
                Goods = (int[])source.Gameplay.Goods.Clone(),
                Troops = (int[])source.Gameplay.Troops.Clone(),
                PreBuild = source.Gameplay.PreBuild,
                ImprovedArabSwordsmen = source.Gameplay.ImprovedArabSwordsmen,
                ImprovedLaddermen = source.Gameplay.ImprovedLaddermen,
                ImprovedSpearmen = source.Gameplay.ImprovedSpearmen,
                RebalancedHorseArchers = source.Gameplay.RebalancedHorseArchers,
                ImprovedFletchers = source.Gameplay.ImprovedFletchers,
                UncappedPeasants = source.Gameplay.UncappedPeasants,
                FasterPeasants = source.Gameplay.FasterPeasants,
                EnemyHitPoints = source.Gameplay.EnemyHitPoints,
                ImprovedSieging = source.Gameplay.ImprovedSieging,
                ImprovedSieging2 = source.Gameplay.ImprovedSieging2,
                Healers = source.Gameplay.Healers,
                Eunuchs = source.Gameplay.Eunuchs,
                NoGold = source.Gameplay.NoGold
            };
        }

        private static bool TestSettingsEqual(TestSettings left, TestSettings right) =>
            left.Fairness == right.Fairness &&
            left.StartingGoods == right.StartingGoods &&
            left.WinCondition == right.WinCondition &&
            left.StrongWalls == right.StrongWalls &&
            left.NoCows == right.NoCows &&
            left.NoDogs == right.NoDogs &&
            left.AllowAutoTrading == right.AllowAutoTrading &&
            left.AutoSave == right.AutoSave &&
            left.GameSpeed == right.GameSpeed &&
            left.PeaceTime == right.PeaceTime &&
            left.ExtremeTroops == right.ExtremeTroops &&
            left.ExtremePowers == right.ExtremePowers &&
            left.ExtremePowersAroundLord == right.ExtremePowersAroundLord &&
            left.AllowOutposts == right.AllowOutposts &&
            ArraysEqual(left.Buildings, right.Buildings) &&
            ArraysEqual(left.Goods, right.Goods) &&
            ArraysEqual(left.Troops, right.Troops) &&
            AdvancedStatesEqual(left.Gameplay, right.Gameplay);

        private static bool AdvancedStatesEqual(
            SkirmishGameOptionsPolicy.AdvancedState left,
            SkirmishGameOptionsPolicy.AdvancedState right) =>
            ArraysEqual(left.Buildings, right.Buildings) &&
            ArraysEqual(left.Goods, right.Goods) &&
            ArraysEqual(left.Troops, right.Troops) &&
            left.PreBuild == right.PreBuild &&
            left.ImprovedArabSwordsmen == right.ImprovedArabSwordsmen &&
            left.ImprovedLaddermen == right.ImprovedLaddermen &&
            left.ImprovedSpearmen == right.ImprovedSpearmen &&
            left.RebalancedHorseArchers == right.RebalancedHorseArchers &&
            left.ImprovedFletchers == right.ImprovedFletchers &&
            left.UncappedPeasants == right.UncappedPeasants &&
            left.FasterPeasants == right.FasterPeasants &&
            left.EnemyHitPoints == right.EnemyHitPoints &&
            left.ImprovedSieging == right.ImprovedSieging &&
            left.ImprovedSieging2 == right.ImprovedSieging2 &&
            left.Healers == right.Healers &&
            left.Eunuchs == right.Eunuchs &&
            left.NoGold == right.NoGold;

        private static bool ArraysEqual(int[] left, int[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private sealed class TestSettings
        {
            internal int Fairness;
            internal int StartingGoods;
            internal int WinCondition;
            internal int StrongWalls;
            internal int NoCows;
            internal int NoDogs;
            internal int AllowAutoTrading;
            internal int AutoSave;
            internal int GameSpeed;
            internal int PeaceTime;
            internal int ExtremeTroops;
            internal int ExtremePowers;
            internal int ExtremePowersAroundLord;
            internal int AllowOutposts;
            internal int[] Buildings;
            internal int[] Goods;
            internal int[] Troops;
            internal SkirmishGameOptionsPolicy.AdvancedState Gameplay;

            internal static TestSettings CreateDefaults() => new TestSettings
            {
                AllowAutoTrading = 1,
                GameSpeed = 40,
                Buildings = new[] { 1, 1, 1 },
                Goods = new[] { 1, 1, 1 },
                Troops = new[] { 1, 1, 1 },
                Gameplay = DefaultAdvancedState()
            };
        }

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertThrows(Action action, string message)
        {
            assertions++;
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
