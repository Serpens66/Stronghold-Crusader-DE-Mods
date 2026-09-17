using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private static int assertions;

        private static int Main()
        {
            try
            {
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == ElevatedMoatNativeContract.ReferenceSha256, "canonical DLL hash");
                byte[] image = MapPeImage(file);
                ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(image);
                ValidateRendererResolution(image);
                Check(ElevatedMoatNativeContract.LowerDrawbridgeHookLength == 15,
                    "lowered-drawbridge RedBird hook spans exactly 15 bytes");
                Check(ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteLength == 8,
                    "lowered-drawbridge height write spans exactly 8 bytes");
                Check(ElevatedMoatNativeContract.LowerDrawbridgeImageBaseLeaRva ==
                    ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteRva + 8,
                    "image-base LEA immediately follows the height write");
                Check(ElevatedMoatNativeContract.CompletedDrawbridgeHookLength == 17,
                    "completed-drawbridge RedBird hook spans exactly 17 bytes");
                Check(ElevatedMoatNativeContract.CompletedDrawbridgeHeightWriteLength == 9,
                    "completed-drawbridge height write spans exactly 9 bytes");
                Check(ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookLength == 19,
                    "drawbridge special-renderer hook spans exactly 19 bytes");
                Check(ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsLength == 17,
                    "drawbridge animated-renderer argument hook spans exactly 17 bytes");
                Check(ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsLength == 16,
                    "drawbridge static-renderer argument hook spans exactly 16 bytes");
                Check(ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionLength == 19,
                    "unit drawbridge height-correction hook spans exactly 19 bytes");
                ExpectContractFailure(image, ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteRva + 2,
                    "lowered-drawbridge RBX/RDI operand mutation");
                ExpectContractFailure(image, ElevatedMoatNativeContract.LowerDrawbridgeImageBaseLeaRva + 2,
                    "lowered-drawbridge image-base LEA mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHeightWriteRva + 3,
                    "completed-drawbridge RBX/R14 operand mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeStateCallRva,
                    "completed-drawbridge state call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeJumpRva,
                    "completed-drawbridge continuation mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.BuildingCreationDefaultHeightRestoreRva,
                    "building-creation DefaultHeightGrid restore mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.BuildingHeightWriterRva,
                    "Vanilla building-height writer mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.BuildingCreationMidpointRva,
                    "Vanilla footprint-height midpoint mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeCreationMidpointForwardRva,
                    "drawbridge midpoint forwarding mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeBuildingHeightForwardRva,
                    "drawbridge building-height forwarding mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeBuildingAllocatorCallRva,
                    "drawbridge building allocator call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                    "drawbridge special-renderer prologue mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererBuildingRecordRva,
                    "drawbridge special-renderer building-record setup mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsRva,
                    "drawbridge animated-renderer arguments mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererTypeCheckRva,
                    "drawbridge animated-renderer type gate mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererTileFlagsRva,
                    "drawbridge animated-renderer tile-flags gate mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererBuildingArgumentRva,
                    "drawbridge animated-renderer building argument mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererCallRva,
                    "drawbridge animated-renderer call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall1ArgumentsRva,
                    "first height-blind renderer arguments mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall2ArgumentsRva,
                    "second height-blind renderer arguments mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall1BuildingArgumentRva,
                    "first special-renderer building argument mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall2BuildingArgumentRva,
                    "second special-renderer building argument mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall1Rva,
                    "first drawbridge special-renderer call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall2Rva,
                    "second drawbridge special-renderer call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsRva,
                    "static renderer argument mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitDrawbridgeTypeGateRva,
                    "unit drawbridge type-gate mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitHeightCorrectionFunctionPrologueRva + 26,
                    "unit height-writer image-base mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionRva,
                    "unit drawbridge height-correction mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitDrawbridgeHeightContinuationRva,
                    "unit drawbridge continuation mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitHeightPostCorrectionRva,
                    "unit height-writer register-liveness mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitHeightInitializationCallRva,
                    "unit initialization height-writer call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitHeightUpdateCall1Rva,
                    "unit update height-writer call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitType2SpriteQueueCall1Rva,
                    "first type-2 unit sprite-queue call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitType2SpriteQueueCall2Rva,
                    "second type-2 unit sprite-queue call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.UnitType52HeightForwardingRva,
                    "type-52 unit interpolation height forwarding mutation");
                ValidateInstalledRedBirdSpans();
                ValidateProductionGenerators(image);
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsRva,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsLength,
                    ElevatedMoatNativeContract.MainRendererRva,
                    ElevatedMoatNativeContract.MainRendererLength,
                    "drawbridge animated-renderer argument block");
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsRva,
                    ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsLength,
                    ElevatedMoatNativeContract.MainRendererRva,
                    ElevatedMoatNativeContract.MainRendererLength,
                    "drawbridge static-renderer argument block");
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHookRva,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHookLength,
                    ElevatedMoatNativeContract.DrawbridgeFunctionRva,
                    ElevatedMoatNativeContract.DrawbridgeFunctionLength,
                    "completed-drawbridge hook block");
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.LowerDrawbridgeHookRva,
                    ElevatedMoatNativeContract.LowerDrawbridgeHookLength,
                    ElevatedMoatNativeContract.LowerDrawbridgeFunctionRva,
                    ElevatedMoatNativeContract.LowerDrawbridgeFunctionLength,
                    "lowered-drawbridge hook block");
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookLength,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererLength,
                    "drawbridge special-renderer prologue");
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionRva,
                    ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionLength,
                    ElevatedMoatNativeContract.UnitHeightCorrectionFunctionRva,
                    ElevatedMoatNativeContract.UnitHeightCorrectionFunctionLength,
                    "unit drawbridge height-correction block");
                CheckDirectCallers(
                    image,
                    ElevatedMoatNativeContract.MainRendererRva,
                    ElevatedMoatNativeContract.MainRendererLength,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall1Rva,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererCall2Rva);
                CheckHeight(0, 0, 0);
                CheckHeight(8, 0, 0);
                CheckHeight(12, 4, 0);
                CheckHeight(13, 5, 5);
                CheckHeight(56, 48, 48);
                CheckHeight(80, 72, 72);
                CheckHeight(122, 114, 114);
                CheckHeight(124, 116, 116);
                CheckHeight(128, 120, 120);
                CheckHeight(130, 122, 122);
                CheckHeight(255, 247, 247);
                CheckRendererGate(0, true, false);
                CheckRendererGate(8, true, false);
                CheckRendererGate(12, true, false);
                CheckRendererGate(13, true, true);
                CheckRendererGate(80, true, true);
                CheckRendererGate(130, true, true);
                CheckRendererGate(255, true, true);
                CheckRendererGate(0, false, false);
                CheckRendererGate(8, false, false);
                CheckRendererGate(12, false, false);
                CheckRendererGate(13, false, false);
                CheckRendererGate(80, false, false);
                CheckRendererGate(130, false, false);
                CheckRendererGate(255, false, false);
                CheckRendererOffset(0, true, 0);
                CheckRendererOffset(8, true, 0);
                CheckRendererOffset(12, true, 0);
                CheckRendererOffset(13, true, -5);
                CheckRendererOffset(64, true, -56);
                CheckRendererOffset(80, true, -72);
                CheckRendererOffset(125, true, -117);
                CheckRendererOffset(130, true, -122);
                CheckRendererOffset(255, true, -247);
                CheckRendererOffset(255, false, 0);
                CheckStaticRendererHeight(4, 12, true, -4);
                CheckStaticRendererHeight(5, 13, true, -13);
                CheckStaticRendererHeight(56, 64, true, -64);
                CheckStaticRendererHeight(72, 80, true, -80);
                CheckStaticRendererHeight(117, 125, true, -125);
                CheckStaticRendererHeight(122, 130, true, -130);
                CheckStaticRendererHeight(247, 255, false, -247);
                CheckUnitHeight(0, 0, false, 8, -8);
                CheckUnitHeight(0, 8, true, 8, -8);
                CheckUnitHeight(0, 12, true, 8, -8);
                CheckUnitHeight(5, 13, true, 8, -13);
                CheckUnitHeight(48, 64, true, 16, -64);
                CheckUnitHeight(72, 80, true, 8, -80);
                CheckUnitHeight(116, 125, true, 9, -125);
                CheckUnitHeight(122, 130, true, 8, -130);
                CheckUnitHeight(247, 255, true, 8, -255);
                CheckUnitHeight(72, 80, false, -64, -8);
                Console.WriteLine($"PASS: elevated-moat height tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }

        private static void CheckHeight(byte defaultHeight, byte expectedMoat, byte expectedDrawbridge)
        {
            Check(ElevatedMoatNativeContract.CalculateCompletedHeight(defaultHeight) == expectedMoat,
                $"moat height for {defaultHeight}");
            byte actualDrawbridge = defaultHeight > ElevatedMoatNativeContract.MaximumVanillaTerrainHeight
                ? (byte)(defaultHeight - ElevatedMoatNativeContract.MoatDepth)
                : (byte)0;
            Check(actualDrawbridge == expectedDrawbridge,
                $"drawbridge height for {defaultHeight}");
            Check(ElevatedMoatNativeContract.CalculateRestoredHeight(defaultHeight) == defaultHeight,
                $"restored height for {defaultHeight}");
        }

        private static void CheckUnitHeight(
            short currentElevation,
            ushort buildingHeight,
            bool featureActive,
            short expectedCorrection,
            short expectedRenderHeight)
        {
            short actualCorrection = ShouldCorrectDrawbridgeRendering(buildingHeight, featureActive)
                ? checked((short)(buildingHeight - currentElevation))
                : unchecked((short)(ElevatedMoatNativeContract.MoatDepth - currentElevation));
            Check(actualCorrection == expectedCorrection,
                $"unit drawbridge correction for elevation {currentElevation}, building {buildingHeight}");
            short actualRenderHeight = checked((short)(-((int)currentElevation + actualCorrection)));
            Check(actualRenderHeight == expectedRenderHeight,
                $"unit drawbridge render height for elevation {currentElevation}, building {buildingHeight}");
        }

        private static void CheckRendererGate(
            ushort buildingHeight,
            bool featureActive,
            bool expected)
        {
            Check(ShouldCorrectDrawbridgeRendering(buildingHeight, featureActive) == expected,
                $"drawbridge renderer gate for building {buildingHeight}, active {featureActive}");
        }

        private static void CheckRendererOffset(
            ushort buildingHeight,
            bool featureActive,
            int expected)
        {
            int actual = ShouldCorrectDrawbridgeRendering(buildingHeight, featureActive)
                ? -(buildingHeight - ElevatedMoatNativeContract.MoatDepth)
                : 0;
            Check(actual == expected,
                $"drawbridge renderer offset for building {buildingHeight}, active {featureActive}");
        }

        private static void CheckStaticRendererHeight(
            int vanillaCurrentTileHeight,
            ushort buildingHeight,
            bool featureActive,
            int expected)
        {
            int actual = ShouldCorrectDrawbridgeRendering(buildingHeight, featureActive)
                ? -buildingHeight
                : -vanillaCurrentTileHeight;
            Check(actual == expected,
                $"static drawbridge height for building {buildingHeight}, active {featureActive}");
        }

        private static bool ShouldCorrectDrawbridgeRendering(
            ushort buildingHeight,
            bool featureActive) =>
            featureActive &&
            buildingHeight > ElevatedMoatNativeContract.MaximumVanillaTerrainHeight;

        private static void ValidateRendererResolution(byte[] image)
        {
            ValidateUniqueExecutableSignature(
                image,
                ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookBytes,
                ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                "drawbridge special-renderer prologue");
            ValidateUniqueExecutableSignature(
                image,
                ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsBytes,
                ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsRva,
                "drawbridge animated-renderer arguments");
            ValidateUniqueExecutableSignature(
                image,
                ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsBytes,
                ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsRva,
                "drawbridge static-renderer arguments");
            ValidateUniqueExecutableSignature(
                image,
                ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionBytes,
                ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionRva,
                "unit drawbridge vertical-correction block");
        }

        private static void ValidateUniqueExecutableSignature(
            byte[] image,
            byte[] signature,
            int expectedRva,
            string description)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                image,
                signature,
                expectedRva,
                referenceHashMatches: true,
                description,
                log: null,
                Shared.NativePatternSearchScope.ExecutableSections);
            Check(resolution.Rva == expectedRva &&
                resolution.Method == "reference-rva",
                $"production {description} signature resolves at the audited reference RVA");

            byte[] missing = (byte[])image.Clone();
            Array.Clear(missing, expectedRva, signature.Length);
            ExpectResolutionFailure(missing, signature, expectedRva, description,
                $"missing {description} signature was accepted");

            byte[] mutated = (byte[])image.Clone();
            mutated[expectedRva] ^= 1;
            ExpectResolutionFailure(mutated, signature, expectedRva, description,
                $"mutated {description} signature was accepted");

            byte[] duplicated = (byte[])image.Clone();
            const int DuplicateExecutableRva = 0x3000;
            Buffer.BlockCopy(signature, 0, duplicated, DuplicateExecutableRva, signature.Length);
            ExpectResolutionFailure(duplicated, signature, expectedRva, description,
                $"duplicate {description} signature was accepted");
        }

        private static void ExpectResolutionFailure(
            byte[] image,
            byte[] signature,
            int expectedRva,
            string description,
            string message)
        {
            assertions++;
            try
            {
                Shared.NativePatternResolver.ResolveUnique(
                    image,
                    signature,
                    expectedRva,
                    referenceHashMatches: false,
                    "test " + description,
                    log: null,
                    Shared.NativePatternSearchScope.ExecutableSections);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        private static void ValidateInstalledRedBirdSpans()
        {
            byte[] lowered = SliceFixture(
                ElevatedMoatNativeContract.LowerDrawbridgeHookBytes,
                16);
            byte[] completed = SliceFixture(
                ElevatedMoatNativeContract.CompletedDrawbridgeHookBytes,
                16);
            byte[] completedAtWrite =
            {
                0x41, 0xC6, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00, 0x00,
                0xEB, 0x0C,
                0x42, 0x81, 0xA4, 0xB3, 0x00, 0x84, 0x89, 0x00, 0xFF, 0xFF, 0xFF, 0xBF
            };
            byte[] renderer = SliceFixture(
                ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookBytes,
                16);
            byte[] animatedRenderer = SliceFixture(
                ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsBytes,
                16);
            byte[] staticRenderer = SliceFixture(
                ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsBytes,
                16);
            byte[] unitHeightCorrection = SliceFixture(
                ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionBytes,
                16);

            CheckRedBirdSpan(lowered, 8, 15,
                "requested 8-byte lowered-drawbridge span expands to 15 bytes");
            CheckRedBirdSpan(lowered, 15, 15,
                "audited lowered-drawbridge span remains 15 bytes");
            CheckRedBirdSpan(completedAtWrite, 9, 23,
                "requested 9-byte completed-drawbridge span expands to 23 bytes");
            CheckRedBirdSpan(completed, 17, 17,
                "audited completed-drawbridge span remains 17 bytes");
            CheckRedBirdSpan(renderer, 14, 19,
                "requested minimum drawbridge renderer span expands to 19 bytes");
            CheckRedBirdSpan(renderer, 19, 19,
                "audited drawbridge renderer span remains 19 bytes");
            CheckRedBirdSpan(animatedRenderer, 17, 17,
                "audited animated drawbridge renderer span remains 17 bytes");
            CheckRedBirdSpan(staticRenderer, 16, 16,
                "audited static drawbridge renderer span remains 16 bytes");
            CheckRedBirdSpan(unitHeightCorrection, 19, 19,
                "audited unit drawbridge height-correction span remains 19 bytes");
        }

        private static void ValidateProductionGenerators(byte[] image)
        {
            const ulong imageBase = 0x180000000;
            byte[] completed = new byte[ElevatedMoatNativeContract.CompletedDrawbridgeHookLength];
            Buffer.BlockCopy(image, ElevatedMoatNativeContract.CompletedDrawbridgeHookRva,
                completed, 0, completed.Length);
            Instruction[] completedInstructions = DecodeExact(
                completed,
                imageBase + (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookRva);
            byte[] completedStub = AssembleAndDecode(
                (assembler, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateCompleted(
                    assembler,
                    completedInstructions,
                    returnAddress,
                    imageBase + 0x2000000,
                    imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStateUpdateRva),
                imageBase + (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookRva +
                    (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookLength,
                "completed-drawbridge production generator");
            ValidateCompletedGeneratorOutput(
                completedStub,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStateUpdateRva,
                imageBase + (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookRva +
                    (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookLength);

            byte[] lowered = new byte[ElevatedMoatNativeContract.LowerDrawbridgeHookLength];
            Buffer.BlockCopy(image, ElevatedMoatNativeContract.LowerDrawbridgeHookRva,
                lowered, 0, lowered.Length);
            Instruction[] loweredInstructions = DecodeExact(
                lowered,
                imageBase + (ulong)ElevatedMoatNativeContract.LowerDrawbridgeHookRva);
            byte[] loweredStub = AssembleAndDecode(
                (assembler, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateLowered(
                    assembler,
                    loweredInstructions,
                    returnAddress,
                    imageBase + 0x2000000,
                    imageBase),
                imageBase + (ulong)ElevatedMoatNativeContract.LowerDrawbridgeContinuationRva,
                "lowered-drawbridge production generator");
            ValidateLoweredGeneratorOutput(
                loweredStub,
                imageBase,
                imageBase + (ulong)ElevatedMoatNativeContract.LowerDrawbridgeContinuationRva);

            byte[] renderer = new byte[ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookLength];
            Buffer.BlockCopy(image, ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                renderer, 0, renderer.Length);
            Instruction[] rendererInstructions = DecodeExact(
                renderer,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva);
            byte[] rendererStub = AssembleAndDecode(
                (assembler, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateSpecialRenderer(
                    assembler,
                    rendererInstructions,
                    returnAddress,
                    imageBase + 0x2000000,
                    imageBase + (ulong)ElevatedMoatNativeContract.BuildingManagerRva),
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeSpecialRendererContinuationRva,
                "drawbridge special-renderer production generator");
            ValidateRendererGeneratorOutput(
                rendererStub,
                imageBase + (ulong)ElevatedMoatNativeContract.BuildingManagerRva);

            byte[] animatedRenderer =
                new byte[ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsLength];
            Buffer.BlockCopy(image,
                ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsRva,
                animatedRenderer, 0, animatedRenderer.Length);
            Instruction[] animatedRendererInstructions = DecodeExact(
                animatedRenderer,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsRva);
            byte[] animatedRendererStub = AssembleAndDecode(
                (assembler, returnAddress) =>
                    ElevatedMoatDrawbridgeHooks.GenerateAnimatedRendererArguments(
                        assembler,
                        animatedRendererInstructions,
                        returnAddress,
                        imageBase + 0x2000000,
                        imageBase + (ulong)ElevatedMoatNativeContract.BuildingManagerRva),
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeAnimatedRendererCallRva,
                "drawbridge animated-renderer production generator");
            ValidateAnimatedRendererGeneratorOutput(
                animatedRendererStub,
                imageBase + (ulong)ElevatedMoatNativeContract.BuildingManagerRva,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeAnimatedRendererCallRva);

            byte[] staticRenderer =
                new byte[ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsLength];
            Buffer.BlockCopy(image,
                ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsRva,
                staticRenderer, 0, staticRenderer.Length);
            Instruction[] staticRendererInstructions = DecodeExact(
                staticRenderer,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStaticRendererArgumentsRva);
            byte[] staticRendererStub = AssembleAndDecode(
                (assembler, returnAddress) =>
                    ElevatedMoatDrawbridgeHooks.GenerateStaticRendererArguments(
                        assembler,
                        staticRendererInstructions,
                        returnAddress,
                        imageBase + 0x2000000,
                        imageBase + (ulong)ElevatedMoatNativeContract.BuildingManagerRva),
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStaticRendererContinuationRva,
                "drawbridge static-renderer production generator");
            ValidateStaticRendererGeneratorOutput(
                staticRendererStub,
                imageBase + (ulong)ElevatedMoatNativeContract.BuildingManagerRva,
                imageBase + (ulong)ElevatedMoatNativeContract.CurrentRenderedTileHeightRva,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStaticRendererContinuationRva);

            byte[] unitHeightCorrection =
                new byte[ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionLength];
            Buffer.BlockCopy(image,
                ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionRva,
                unitHeightCorrection, 0, unitHeightCorrection.Length);
            Instruction[] unitHeightCorrectionInstructions = DecodeExact(
                unitHeightCorrection,
                imageBase + (ulong)ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionRva);
            byte[] unitHeightCorrectionStub = AssembleAndDecode(
                (assembler, returnAddress) =>
                    ElevatedMoatDrawbridgeHooks.GenerateUnitHeightCorrection(
                        assembler,
                        unitHeightCorrectionInstructions,
                        returnAddress,
                        imageBase + 0x2000000),
                imageBase + (ulong)ElevatedMoatNativeContract.UnitDrawbridgeHeightContinuationRva,
                "unit drawbridge height-correction production generator");
            ValidateUnitHeightCorrectionGeneratorOutput(
                unitHeightCorrectionStub,
                imageBase + (ulong)ElevatedMoatNativeContract.UnitDrawbridgeHeightContinuationRva);
        }

        private static byte[] SliceFixture(byte[] prefix, int trailingNops)
        {
            byte[] fixture = new byte[prefix.Length + trailingNops];
            Buffer.BlockCopy(prefix, 0, fixture, 0, prefix.Length);
            for (int index = prefix.Length; index < fixture.Length; index++)
                fixture[index] = 0x90;
            return fixture;
        }

        private static void CheckRedBirdSpan(
            byte[] fixture,
            int requestedLength,
            int expectedLength,
            string message)
        {
            IntPtr memory = Marshal.AllocHGlobal(fixture.Length);
            try
            {
                Marshal.Copy(fixture, 0, memory, fixture.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)memory.ToInt64()), requestedLength, null, message))
                {
                    Check(probe.DisplacedByteCount == expectedLength, message);
                    Check(!probe.IsInstalled, message + " remains decode-only");
                }

                byte[] after = new byte[fixture.Length];
                Marshal.Copy(memory, after, 0, after.Length);
                Check(AreEqual(fixture, after), message + " preserves fixture bytes");
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong instructionPointer)
        {
            var reader = new ByteArrayCodeReader(bytes);
            Decoder decoder = Decoder.Create(64, reader);
            decoder.IP = instructionPointer;
            ulong end = instructionPointer + (ulong)bytes.Length;
            var instructions = new List<Instruction>();
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= end,
                    "Vanilla hook fixture decodes on exact instruction boundaries");
                instructions.Add(instruction);
            }

            Check(decoder.IP == end, "Vanilla hook fixture consumes its exact span");
            return instructions.ToArray();
        }

        private static byte[] AssembleAndDecode(
            Action<Assembler, ulong> generate,
            ulong returnAddress,
            string description)
        {
            var assembler = new Assembler(64);
            generate(assembler, returnAddress);
            assembler.AddUnrestrictedJmp(returnAddress);

            const ulong stubAddress = 0x180100000;
            byte[] stub;
            using (var stream = new MemoryStream())
            {
                var writer = new StreamCodeWriter(stream);
                Check(assembler.TryAssemble(writer, stubAddress, out string error, out _),
                    description + " assembles: " + error);
                stub = stream.ToArray();
            }

            int offset = 0;
            int decoded = 0;
            while (offset < stub.Length)
            {
                if (IsAbsoluteJump(stub, offset))
                {
                    offset += 14;
                    decoded++;
                    continue;
                }

                byte[] remaining = new byte[stub.Length - offset];
                Buffer.BlockCopy(stub, offset, remaining, 0, remaining.Length);
                var reader = new ByteArrayCodeReader(remaining);
                Decoder decoder = Decoder.Create(64, reader);
                decoder.IP = stubAddress + (ulong)offset;
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.Length <= remaining.Length,
                    description + " decodes without truncated instructions");
                offset += instruction.Length;
                decoded++;
            }

            Check(offset == stub.Length && decoded > 0,
                description + " consumes the full generated stub");
            return stub;
        }

        private static void ValidateCompletedGeneratorOutput(
            byte[] stub,
            ulong stateUpdateAddress,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int stateCalls = 0;
            int heightWrites = 0;
            int defaultHeightLoads = 0;
            int terrainLimitComparisons = 0;
            int heightArithmetic = 0;
            int moatDepthSubtractions = 0;
            int instructionIndex = 0;
            int defaultHeightLoadIndex = -1;
            int terrainLimitComparisonIndex = -1;
            int moatDepthSubtractionIndex = -1;
            int adjustedHeightWriteIndex = -1;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.NearBranchTarget == stateUpdateAddress)
                {
                    stateCalls++;
                }
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    HasMemoryOperands(instruction, Register.RBX, Register.R14) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.TileHeightGridOffset)
                {
                    heightWrites++;
                    if (instruction.Op1Register == Register.AL)
                        adjustedHeightWriteIndex = instructionIndex;
                }
                if (instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX &&
                    HasMemoryOperands(instruction, Register.RBX, Register.R14) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.TileDefaultHeightGridOffset)
                {
                    defaultHeightLoads++;
                    defaultHeightLoadIndex = instructionIndex;
                }
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                {
                    terrainLimitComparisons++;
                    terrainLimitComparisonIndex = instructionIndex;
                }
                if ((instruction.Mnemonic == Mnemonic.Add ||
                     instruction.Mnemonic == Mnemonic.Sub) &&
                    instruction.Op0Register == Register.EAX)
                {
                    heightArithmetic++;
                    if (instruction.Mnemonic == Mnemonic.Sub &&
                        (instruction.Immediate8 == ElevatedMoatNativeContract.MoatDepth ||
                         instruction.Immediate32 == ElevatedMoatNativeContract.MoatDepth))
                    {
                        moatDepthSubtractions++;
                        moatDepthSubtractionIndex = instructionIndex;
                    }
                }
                instructionIndex++;
            }

            Check(stateCalls == 1,
                "completed generator calls Vanilla state update exactly once");
            Check(heightWrites == 2,
                "completed generator emits adaptive and Vanilla height writes");
            Check(defaultHeightLoads == 1 && terrainLimitComparisons == 1 &&
                heightArithmetic == 1 && moatDepthSubtractions == 1,
                "completed generator derives the elevated moat floor with exactly one depth subtraction");
            Check(defaultHeightLoadIndex < terrainLimitComparisonIndex &&
                terrainLimitComparisonIndex < moatDepthSubtractionIndex &&
                moatDepthSubtractionIndex < adjustedHeightWriteIndex,
                "completed generator checks the terrain limit before subtracting depth and writing");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 2,
                "completed generator returns both height branches to Vanilla continuation");
        }

        private static void ValidateLoweredGeneratorOutput(
            byte[] stub,
            ulong imageBase,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int heightWrites = 0;
            int imageBaseLoads = 0;
            int defaultHeightLoads = 0;
            int terrainLimitComparisons = 0;
            int heightArithmetic = 0;
            int moatDepthSubtractions = 0;
            int instructionIndex = 0;
            int defaultHeightLoadIndex = -1;
            int terrainLimitComparisonIndex = -1;
            int moatDepthSubtractionIndex = -1;
            int adjustedHeightWriteIndex = -1;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    HasMemoryOperands(instruction, Register.RBX, Register.RDI) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.TileHeightGridOffset)
                {
                    heightWrites++;
                    if (instruction.Op1Register == Register.AL)
                        adjustedHeightWriteIndex = instructionIndex;
                }
                if (instruction.Mnemonic == Mnemonic.Lea &&
                    instruction.Op0Register == Register.RDI &&
                    instruction.MemoryBase == Register.RIP &&
                    instruction.MemoryDisplacement64 == imageBase)
                {
                    imageBaseLoads++;
                }
                if (instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX &&
                    HasMemoryOperands(instruction, Register.RBX, Register.RDI) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.TileDefaultHeightGridOffset)
                {
                    defaultHeightLoads++;
                    defaultHeightLoadIndex = instructionIndex;
                }
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                {
                    terrainLimitComparisons++;
                    terrainLimitComparisonIndex = instructionIndex;
                }
                if ((instruction.Mnemonic == Mnemonic.Add ||
                     instruction.Mnemonic == Mnemonic.Sub) &&
                    instruction.Op0Register == Register.EAX)
                {
                    heightArithmetic++;
                    if (instruction.Mnemonic == Mnemonic.Sub &&
                        (instruction.Immediate8 == ElevatedMoatNativeContract.MoatDepth ||
                         instruction.Immediate32 == ElevatedMoatNativeContract.MoatDepth))
                    {
                        moatDepthSubtractions++;
                        moatDepthSubtractionIndex = instructionIndex;
                    }
                }
                instructionIndex++;
            }

            Check(heightWrites == 2,
                "lowered generator emits adaptive and Vanilla height writes");
            Check(imageBaseLoads == 1,
                "lowered generator restores RDI to the image base exactly once");
            Check(defaultHeightLoads == 1 && terrainLimitComparisons == 1 &&
                heightArithmetic == 1 && moatDepthSubtractions == 1,
                "lowered generator derives the elevated moat floor with exactly one depth subtraction");
            Check(defaultHeightLoadIndex < terrainLimitComparisonIndex &&
                terrainLimitComparisonIndex < moatDepthSubtractionIndex &&
                moatDepthSubtractionIndex < adjustedHeightWriteIndex,
                "lowered generator checks the terrain limit before subtracting depth and writing");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 1,
                "lowered generator returns to the exact Vanilla continuation");
        }

        private static void ValidateRendererGeneratorOutput(
            byte[] stub,
            ulong buildingManagerAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out _);
            int managerLoads = 0;
            int buildingIdLoads = 0;
            int strideMultiplications = 0;
            int buildingHeightLoads = 0;
            int terrainLimitComparisons = 0;
            int scratchPushes = 0;
            int scratchPops = 0;
            int depthSubtractions = 0;
            int renderYSubtractions = 0;
            int stackYSubtractions = 0;
            int originalStackAllocation = 0;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.RAX &&
                    instruction.Op1Kind == OpKind.Immediate64 &&
                    instruction.Immediate64 == buildingManagerAddress)
                    managerLoads++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.R10D &&
                    instruction.Op1Register == Register.EDX)
                    buildingIdLoads++;
                if (instruction.Mnemonic == Mnemonic.Imul &&
                    instruction.Op0Register == Register.R10 &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.BuildingRecordStride)
                    strideMultiplications++;
                if (instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX &&
                    HasMemoryOperands(instruction, Register.RAX, Register.R10) &&
                    instruction.MemoryDisplacement64 == ElevatedMoatNativeContract.BuildingHeightOffset)
                    buildingHeightLoads++;
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                    terrainLimitComparisons++;
                if (instruction.Mnemonic == Mnemonic.Push &&
                    instruction.Op0Register == Register.R10)
                    scratchPushes++;
                if (instruction.Mnemonic == Mnemonic.Pop &&
                    instruction.Op0Register == Register.R10)
                    scratchPops++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.EAX &&
                    (instruction.Immediate8 == ElevatedMoatNativeContract.MoatDepth ||
                     instruction.Immediate32 == ElevatedMoatNativeContract.MoatDepth))
                    depthSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.R9D &&
                    instruction.Op1Register == Register.EAX)
                    renderYSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x38 &&
                    instruction.Op1Register == Register.EAX)
                    stackYSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.RSP &&
                    instruction.Immediate32 == 0x80)
                    originalStackAllocation++;
            }

            Check(managerLoads == 1 && buildingIdLoads == 1 && strideMultiplications == 1 &&
                buildingHeightLoads == 1 && terrainLimitComparisons == 1,
                "renderer generator resolves and gates on argument-2 building height exactly once");
            Check(scratchPushes == 1 && scratchPops == 1,
                "renderer generator preserves its temporary building-index register");
            Check(depthSubtractions == 1 && renderYSubtractions == 1 && stackYSubtractions == 1,
                "renderer generator applies H - 8 to both height-blind coordinates exactly once");
            Check(originalStackAllocation == 1,
                "renderer generator replays the original stack allocation exactly once");
        }

        private static void ValidateAnimatedRendererGeneratorOutput(
            byte[] stub,
            ulong buildingManagerAddress,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int managerLoads = 0;
            int buildingIdLoads = 0;
            int strideMultiplications = 0;
            int buildingHeightLoads = 0;
            int terrainLimitComparisons = 0;
            int depthSubtractions = 0;
            int heightNegations = 0;
            int vanillaZeroWrites = 0;
            int adjustedHeightWrites = 0;
            int managerArgumentLoads = 0;
            int fifthArgumentWrites = 0;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.RAX &&
                    instruction.Op1Kind == OpKind.Immediate64 &&
                    instruction.Immediate64 == buildingManagerAddress)
                    managerLoads++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.R10D &&
                    instruction.Op1Register == Register.EDX)
                    buildingIdLoads++;
                if (instruction.Mnemonic == Mnemonic.Imul &&
                    instruction.Op0Register == Register.R10 &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.BuildingRecordStride)
                    strideMultiplications++;
                if (instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX &&
                    HasMemoryOperands(instruction, Register.RAX, Register.R10) &&
                    instruction.MemoryDisplacement64 == ElevatedMoatNativeContract.BuildingHeightOffset)
                    buildingHeightLoads++;
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                    terrainLimitComparisons++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.EAX &&
                    (instruction.Immediate8 == ElevatedMoatNativeContract.MoatDepth ||
                     instruction.Immediate32 == ElevatedMoatNativeContract.MoatDepth))
                    depthSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Neg &&
                    instruction.Op0Register == Register.EAX)
                    heightNegations++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x28)
                {
                    if (instruction.Op1Register == Register.ESI)
                        vanillaZeroWrites++;
                    if (instruction.Op1Register == Register.EAX)
                        adjustedHeightWrites++;
                }
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.RCX &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x140)
                    managerArgumentLoads++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x20 &&
                    instruction.Op1Register == Register.R15D)
                    fifthArgumentWrites++;
            }

            Check(managerLoads == 1 && buildingIdLoads == 1 && strideMultiplications == 1 &&
                buildingHeightLoads == 1 && terrainLimitComparisons == 1,
                "animated renderer generator resolves and gates on EDX building height exactly once");
            Check(depthSubtractions == 1 && heightNegations == 1,
                "animated renderer generator derives exactly one 8 - H offset");
            Check(vanillaZeroWrites == 1 && adjustedHeightWrites == 1,
                "animated renderer generator keeps Vanilla zero and elevated height branches");
            Check(managerArgumentLoads == 1 && fifthArgumentWrites == 2,
                "animated renderer generator preserves the other Vanilla call arguments on both branches");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 2,
                "animated renderer generator returns both branches to the Vanilla call");
        }

        private static void ValidateStaticRendererGeneratorOutput(
            byte[] stub,
            ulong buildingManagerAddress,
            ulong currentHeightAddress,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int managerLoads = 0;
            int buildingIdLoads = 0;
            int strideMultiplications = 0;
            int buildingHeightLoads = 0;
            int terrainLimitComparisons = 0;
            int elevatedSubtractions = 0;
            int vanillaSubtractions = 0;
            int buildingArgumentCopies = 0;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.RAX &&
                    instruction.Op1Kind == OpKind.Immediate64 &&
                    instruction.Immediate64 == buildingManagerAddress)
                    managerLoads++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.R11D &&
                    instruction.Op1Register == Register.EDI)
                    buildingIdLoads++;
                if (instruction.Mnemonic == Mnemonic.Imul &&
                    instruction.Op0Register == Register.R11 &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.BuildingRecordStride)
                    strideMultiplications++;
                if (instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX &&
                    HasMemoryOperands(instruction, Register.RAX, Register.R11) &&
                    instruction.MemoryDisplacement64 == ElevatedMoatNativeContract.BuildingHeightOffset)
                    buildingHeightLoads++;
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                    terrainLimitComparisons++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.R10D &&
                    instruction.Op1Register == Register.EAX)
                    elevatedSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.R10D &&
                    instruction.MemoryBase == Register.RIP &&
                    instruction.MemoryDisplacement64 == currentHeightAddress)
                    vanillaSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.EDX &&
                    instruction.Op1Register == Register.EDI)
                    buildingArgumentCopies++;
            }

            Check(managerLoads == 1 && buildingIdLoads == 1 && strideMultiplications == 1 &&
                buildingHeightLoads == 1 && terrainLimitComparisons == 1,
                "static renderer generator resolves and gates on EDI building height exactly once");
            Check(elevatedSubtractions == 1 && vanillaSubtractions == 1,
                "static renderer generator keeps Vanilla tile height and elevated building-height branches");
            Check(buildingArgumentCopies == 1,
                "static renderer generator preserves Vanilla's EDI-to-EDX building argument");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 1,
                "static renderer generator returns to the exact Vanilla continuation");
        }

        private static void ValidateUnitHeightCorrectionGeneratorOutput(
            byte[] stub,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int buildingIdLoads = 0;
            int strideMultiplications = 0;
            int buildingHeightLoads = 0;
            int terrainLimitComparisons = 0;
            int elevationSubtractions = 0;
            int correctionWrites = 0;
            int constantCorrections = 0;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.ECX &&
                    instruction.Op1Register == Register.EDI)
                    buildingIdLoads++;
                if (instruction.Mnemonic == Mnemonic.Imul &&
                    instruction.Op0Register == Register.RCX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.BuildingRecordStride)
                    strideMultiplications++;
                if (instruction.Mnemonic == Mnemonic.Movzx &&
                    instruction.Op0Register == Register.EAX &&
                    HasMemoryOperands(instruction, Register.RBP, Register.RCX) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.BuildingManagerRva +
                        ElevatedMoatNativeContract.BuildingHeightOffset)
                    buildingHeightLoads++;
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                    terrainLimitComparisons++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.AX &&
                    instruction.MemoryBase == Register.RBX &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.UnitCurrentElevationOffset)
                    elevationSubtractions++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.MemoryBase == Register.RBX &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.UnitVerticalCorrectionOffset &&
                    instruction.Op1Register == Register.AX)
                    correctionWrites++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Immediate32 == ElevatedMoatNativeContract.MoatDepth)
                    constantCorrections++;
            }

            Check(buildingIdLoads == 1 && strideMultiplications == 1 &&
                buildingHeightLoads == 1 && terrainLimitComparisons == 1,
                "unit height generator resolves and gates on EDI building height exactly once");
            Check(elevationSubtractions == 2,
                "unit height generator applies elevation subtraction in elevated and Vanilla branches");
            Check(correctionWrites == 2 && constantCorrections == 1,
                "unit height generator emits one elevated and one Vanilla correction write");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 2,
                "unit height generator returns elevated and Vanilla branches to the exact continuation");
        }

        private static Instruction[] DecodeGeneratedInstructions(
            byte[] stub,
            out ulong[] absoluteJumpTargets)
        {
            const ulong stubAddress = 0x180100000;
            var instructions = new List<Instruction>();
            var jumpTargets = new List<ulong>();
            int offset = 0;
            while (offset < stub.Length)
            {
                if (IsAbsoluteJump(stub, offset))
                {
                    jumpTargets.Add(BitConverter.ToUInt64(stub, offset + 6));
                    offset += 14;
                    continue;
                }

                var reader = new ByteArrayCodeReader(Slice(stub, offset));
                Decoder decoder = Decoder.Create(64, reader);
                decoder.IP = stubAddress + (ulong)offset;
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.Length <= stub.Length - offset,
                    "generated instruction decodes completely");
                instructions.Add(instruction);
                offset += instruction.Length;
            }

            absoluteJumpTargets = jumpTargets.ToArray();
            return instructions.ToArray();
        }

        private static bool HasMemoryOperands(
            Instruction instruction,
            Register first,
            Register second) =>
            (instruction.MemoryBase == first && instruction.MemoryIndex == second) ||
            (instruction.MemoryBase == second && instruction.MemoryIndex == first);

        private static int CountBranchTargets(
            Instruction[] instructions,
            ulong[] absoluteTargets,
            ulong expected)
        {
            int count = 0;
            foreach (Instruction instruction in instructions)
            {
                if ((instruction.Op0Kind == OpKind.NearBranch16 ||
                     instruction.Op0Kind == OpKind.NearBranch32 ||
                     instruction.Op0Kind == OpKind.NearBranch64) &&
                    instruction.NearBranchTarget == expected)
                {
                    count++;
                }
            }
            foreach (ulong value in absoluteTargets)
            {
                if (value == expected)
                    count++;
            }
            return count;
        }

        private static byte[] Slice(byte[] source, int offset)
        {
            byte[] result = new byte[source.Length - offset];
            Buffer.BlockCopy(source, offset, result, 0, result.Length);
            return result;
        }

        private static bool IsAbsoluteJump(byte[] code, int offset) =>
            code.Length - offset >= 14 &&
            code[offset] == 0xFF &&
            code[offset + 1] == 0x25 &&
            code[offset + 2] == 0 &&
            code[offset + 3] == 0 &&
            code[offset + 4] == 0 &&
            code[offset + 5] == 0;

        private static bool AreEqual(byte[] left, byte[] right)
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

        private static void CheckNoIncomingTargets(
            byte[] image,
            int hookRva,
            int hookLength,
            int functionRva,
            int functionLength,
            string description)
        {
            const ulong imageBase = 0x180000000;
            byte[] function = new byte[functionLength];
            Buffer.BlockCopy(image, functionRva, function, 0, function.Length);
            var reader = new ByteArrayCodeReader(function);
            Decoder decoder = Decoder.Create(64, reader);
            decoder.IP = imageBase + (ulong)functionRva;
            ulong functionEnd = decoder.IP + (ulong)functionLength;
            ulong hookStart = imageBase + (ulong)hookRva;
            ulong hookEnd = hookStart + (ulong)hookLength;
            while (decoder.IP < functionEnd)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= functionEnd,
                    description + " owner function decodes completely");
                if (instruction.IP >= hookStart && instruction.IP < hookEnd)
                    continue;
                if (instruction.Op0Kind != OpKind.NearBranch16 &&
                    instruction.Op0Kind != OpKind.NearBranch32 &&
                    instruction.Op0Kind != OpKind.NearBranch64)
                {
                    continue;
                }

                ulong target = instruction.NearBranchTarget;
                Check(target <= hookStart || target >= hookEnd,
                    description + " has no incoming direct target inside its displaced span");
            }
        }

        private static void CheckDirectCallers(
            byte[] image,
            int functionRva,
            int functionLength,
            int targetRva,
            int expectedCall1Rva,
            int expectedCall2Rva)
        {
            const ulong imageBase = 0x180000000;
            byte[] function = new byte[functionLength];
            Buffer.BlockCopy(image, functionRva, function, 0, function.Length);
            var reader = new ByteArrayCodeReader(function);
            Decoder decoder = Decoder.Create(64, reader);
            decoder.IP = imageBase + (ulong)functionRva;
            ulong functionEnd = decoder.IP + (ulong)functionLength;
            var callers = new List<ulong>();
            while (decoder.IP < functionEnd)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= functionEnd,
                    "main renderer decodes completely");
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.NearBranchTarget == imageBase + (ulong)targetRva)
                    callers.Add(instruction.IP);
            }

            Check(callers.Count == 2,
                "drawbridge special renderer has exactly two direct calls in the main renderer");
            Check(callers.Contains(imageBase + (ulong)expectedCall1Rva) &&
                callers.Contains(imageBase + (ulong)expectedCall2Rva),
                "drawbridge special renderer is called only at the two audited call sites");
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int count = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            var image = new byte[BitConverter.ToInt32(file, optional + 56)];
            Buffer.BlockCopy(file, 0, image, 0, BitConverter.ToInt32(file, optional + 60));
            int table = optional + optionalSize;
            for (int index = 0; index < count; index++)
            {
                int header = table + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int raw = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, virtualAddress, Math.Min(rawSize, file.Length - raw));
            }
            return image;
        }

        private static string Hash(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void ExpectContractFailure(byte[] image, int mutationRva, string message)
        {
            assertions++;
            byte[] changed = (byte[])image.Clone();
            changed[mutationRva] ^= 1;
            try
            {
                ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changed);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
