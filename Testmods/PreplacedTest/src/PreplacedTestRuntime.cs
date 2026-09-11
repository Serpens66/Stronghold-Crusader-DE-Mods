using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PreplacedTest
{
    internal sealed unsafe class PreplacedTestRuntime
    {
        private const int MaxPlayablePlayerId = 8;
        private const int MaxAivSpecIndex = 8;
        private const int AivSpecStride = 0x6D98;
        private const int PlayerRuntimeStateStride = 0x583C;
        private const int PreparedLayoutFrameCount = 0x922;
        private const int PreparedEntrySize = 0x0C;
        private const int PreparedEntryBaseOffset = 0x38;
        private const int PlayerIdOffset = 0x04;
        private const int OrientationOffset = 0x0C;
        private const int CandidateIdOffset = 0x10;
        private const int PlacementStateOffset = 0x14;
        private const int CurrentStepGoalOffset = 0x18;
        private const int BuildCounterOffset = 0x1C;
        private const int BuildRateOffset = 0x20;
        private const int HighestPreparedFrameOffset = 0x24;
        private const int OriginXOffset = 0x28;
        private const int OriginYOffset = 0x2C;
        private const int KeepXOffset = 0x30;
        private const int KeepYOffset = 0x34;
        private const int ActiveAicRelativeOffset = 0x04;
        private const int CrushedCounterRelativeOffset = 0x7E4;
        private const int EconomyPhaseRelativeOffset = 0x1564;
        private const int PauseIndexRelativeOffset = 0x1568;
        private const int PauseCounterRelativeOffset = 0x156C;
        private const int PauseTableRelativeOffset = 0x1570;
        private const int PauseConfiguredRelativeOffset = 0x1598;
        private const int PauseTableEntryCount =
            (PauseConfiguredRelativeOffset - PauseTableRelativeOffset) / sizeof(short);
        private const int WoodSearchCooldownRelativeOffset = 0x167C;
        private const int FarmSearchCooldownRelativeOffset = 0x167E;
        private const int QuarrySearchCooldownRelativeOffset = 0x1680;
        private const int IronSearchCooldownRelativeOffset = 0x1682;
        private const int PitchSearchCooldownRelativeOffset = 0x1684;
        private const int FarmBuiltCountRelativeOffset = 0x162C;
        private const int FarmDesiredCountRelativeOffset = 0x1688;
        private const int QuarryDesiredCountRelativeOffset = 0x168A;
        private const int IronDesiredCountRelativeOffset = 0x168C;
        private const int PitchDesiredCountRelativeOffset = 0x168E;
        private const int IronBuiltCountRelativeOffset = 0x1690;
        private const int PitchBuiltCountRelativeOffset = 0x1694;
        private const int QuarryBuiltCountRelativeOffset = 0x1698;
        private const int FarmSearchMapGateRva = 0x64CCC04;
        private const int AivGridSize = 100;
        private const int LogPayloadLength = 1600;
        private const int BuildingCountModeFieldOffset = 0x2C8;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativePclGridRva = 0x50EC690;
        private const int NativePclGridEndRva = 0x51890D0;
        private const int NativePclEntrySize = sizeof(ushort);
        private const int NativePclEntryCount = (NativePclGridEndRva - NativePclGridRva) / NativePclEntrySize;
        private const int CrushedTimerWriterBlockRva = 0x7F052;
        private const int CrushedTimerWriterRva = 0x7F074;
        private const int CrushedTimerWriterDisplacedLength = 15;
        private const int BuildingDamageFunctionRva = 0x7EB00;
        private const int BuildingDamageFunctionLength = 0xD7A;
        private const int LegacyPlayerStateCopyRva = 0xD4290;
        private const int LegacyPlayerStateCopyCallSiteRva = 0x96CE;
        private const int LegacyPlayerStateSourceRva = 0x37CC7EC;
        private const int CurrentPlayerStateDestinationRva = 0x379ADD0;
        private const int ActivePlayerRuntimeStateBaseRva = 0x379D0CC;
        private const int PlayerResourcesOffsetInSerializedRecord =
            ActivePlayerRuntimeStateBaseRva - CurrentPlayerStateDestinationRva;
        private const int SerializedCrushedCounterOffset =
            PlayerResourcesOffsetInSerializedRecord + CrushedCounterRelativeOffset;
        private const int MapFormatVersionRva = 0x32DC084;
        private const int LegacyPlayerStateCopyVersionExclusive = 0xD5;
        private const int LegacyPlayerStateStride = 0x39F4;
        private const int SerializedPlayerRecordCount = 9;
        private const int PlayerStateChoreRva = 0x15B90;
        private const int PlayerStateRecordCopyCallSiteRva = 0x15C4A;
        private const int ChoreCopyFieldRva = 0x1F5F0;
        private const int ChoreCopyFieldMemcpyCallSiteRva = 0x1F65D;
        private const int ChoreCopyFieldEndRva = 0x1F68D;
        private const int NativeChoreManagerRva = 0x8574320;
        private const int CurrentChoreRecordIndexRva = 0x86C132C;
        private const int ChoreDirectionRva = 0x85F8FEC;
        private const int ChoreBlockedRva = 0x8574CC0;
        private const int ChoreCursorOffset = 0x370BF8;
        private const int ChoreLinearBufferOffset = 0x84CD8;
        private const int ChorePlayerIndexFieldSize = sizeof(int);
        private const int ChoreBufferCapacity = 180000;
        private const int PlayerClassTableRva = 0x37EDF3C;
        // Literal protocol/layout values of the audited PCL and accessibility functions.
        private const int MaximumPortalRecordCount = 200;
        private const int PortalRecordStrideDwords = 0x81;
        private const int PortalStateOffsetDwords = 0x809;
        private const int PortalKindOffsetDwords = 0x80A;
        private const int PortalBuildingIdOffsetDwords = 0x80C;
        private const int PortalActiveOffsetDwords = 0x80F;
        private const int PortalFirstPclOffsetDwords = 0x816;
        private const int PortalSecondPclOffsetDwords = 0x817;
        private const int PortalOwnerOffsetDwords = 0x882;
        private const int PortalThirdPclOffsetDwords = 0x883;
        private const int NativePortalLiveState = 1;
        private const int NativePortalExcludedKindForEconomyModeZero = 1;
        // Audited AI economy flood-fill layout for FBCB9319. These names intentionally
        // describe storage only; the individual cell bytes are not assigned semantics.
        private const int EconomyGridWidth = 160;
        private const int NativeTileGridWidth = 800;
        private const int EconomyGridCellCount = EconomyGridWidth * EconomyGridWidth;
        private const int EconomyGridCellStride = 0x30;
        private const int EconomyGridBaseOffset = 0x5B830;
        private const int EconomyVisitGenerationOffset = 0x5B50C;
        private const int EconomyQueueDepthOffset = 0x187830;
        private const int EconomyQueueReadOffset = 0x187834;
        private const int EconomyQueueWriteOffset = 0x187838;
        private const int EconomyResultXOffset = 0x1B983C;
        private const int EconomyResultYOffset = 0x1B9840;
        private const int EconomyReferencePclOffset = 0x5B504;
        private const int EconomyCoarseCellTileSize = 5;
        // Signed comparison thresholds taken directly from the audited FBCB9319 pseudocode.
        private const int WoodExpansionCellValueExclusive = 0x10;
        private const int FarmExpansionCellValueExclusive = 0x11;
        private const int ResourceExpansionDifferenceExclusive = 0x10;
        private const int NearbyExpansionCellValueExclusive = 0x0F;

        private const string AllocateSpecPattern =
            "48 89 74 24 10 57 48 83 EC 20 BF 01 00 00 00 48 8D 81 9C 6D 00 00";
        private const string SetPlacementPattern =
            "40 53 48 83 EC 30 48 63 C2 45 8B D1 48 69 D8 98 6D 00 00";
        private const string SelectBestFitPattern =
            "44 88 44 24 18 89 54 24 10 55 56 41 54 41 55 41 56 41 57 48 83 EC 58";
        private const string TestSpecificCandidatePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 41 8B F0 48 63 EA";
        private const string LoadCandidatePattern =
            "40 53 56 57 41 55 48 83 EC 38 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 41 8B D8 48 63 FA 85 C0";
        private const string ApplyRotationPattern =
            "85 D2 0F 84 ?? ?? ?? ?? 53 48 83 EC 20 48 89 74 24 30 48 8B D9 48 89 7C 24 38 83 FA 06";
        private const string EvaluateCandidateFitPattern =
            "89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 45 33 C9 48 8D 81 44 98 1B 00";
        private const string PrepareLayoutPattern =
            "44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 68";
        private const string SchedulerPattern =
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 F2 48 8D 05 ?? ?? ?? ??";
        private const string ExecuteBuildStepPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private const string AlternativeExecutionPattern =
            "44 89 44 24 18 89 54 24 10 48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC C8 00 00 00";
        private const string PlacementHelperPattern =
            "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48 " +
            "44 8B BC 24 B8 00 00 00 45 8B E1 41 8B E8 89 54 24 20 45 8B C4 8B D5";
        private const string ValidatorPattern =
            "40 53 55 56 57 41 56 48 83 EC 40 33 C0 49 63 E8 83 BC 24 90 00 00 00 02";
        private const string ResourceGatePattern =
            "48 89 5C 24 20 44 89 44 24 18 89 54 24 10 48 89 4C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83";
        private const string MapperWaitOnePattern =
            "41 83 F8 36 75 4D 48 63 C2 48 8D 15 ?? ?? ?? ?? 48 69 C8 3C 58 00 00";
        private const string MapperWaitTwoPattern =
            "48 63 C2 48 69 C8 3C 58 00 00 48 8D 05 ?? ?? ?? ?? 83 BC 01 B0 24 13 00 00";
        private const string MapperWaitThreePattern =
            "41 81 C0 60 FF FF FF 41 81 F8 A8 00 00 00 77 52 49 63 C0 4C 8D 05 ?? ?? ?? ??";
        private const string MapperWaitFourPattern =
            "41 81 C0 50 FF FF FF 41 81 F8 87 00 00 00 77 57 49 63 C0 4C 8D 05 ?? ?? ?? ??";
        private const string DeleteHovelPattern =
            "48 89 5C 24 08 57 48 83 EC 20 48 63 FA 48 8D 15 ?? ?? ?? ?? 48 69 CF 3C 58 00 00";
        private const string MaintenanceOnePattern =
            "48 8B C4 55 41 57 48 83 EC 68 48 63 EA 4C 8D 3D ?? ?? ?? ?? 48 69 CD 3C 58 00 00";
        private const string MaintenanceTwoPattern =
            "4C 8B DC 55 41 56 41 57 48 83 EC 60 4C 8D 3D ?? ?? ?? ?? 48 63 EA 48 69 D5 3C 58 00 00";
        private const string ActiveLayoutReferencePattern =
            "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00";
        private const string CountBuildingsPattern =
            "4C 63 59 50 45 33 D2 49 83 FB 01 7E 40 48 81 C1 5E 04 00 00 49 FF CB 66 83 79 FA 02";
        private const string PlacementReachabilityPattern =
            "48 83 EC 38 49 63 C0 4C 8D 15 ?? ?? ?? ?? 41 83 BC 82 E0 4F 2E 00 00 75 62 48 63 C2";
        private const string AccessibilitySweepPattern =
            "40 56 57 41 56 48 83 EC 20 BE 01 00 00 00 44 8B F2 48 8B F9 39 71 50";
        private const string BuildingAccessibilityPattern =
            "44 89 44 24 18 55 41 57 48 83 EC 58 48 63 EA 4C 8B F9 85 D2 7F 0A 33 C0";
        private const string EconomyFarmPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 41 8B D8";
        private const string EconomyIronPattern =
            "40 56 57 41 54 48 83 EC 50 8B F2 48 8B F9 44 8B C2";
        private const string EconomyOxenPattern =
            "48 89 5C 24 20 56 57 41 54 48 83 EC 40 48 63 FA";
        private const string EconomyPitchPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 40 8B FA 48 8B D9 44 8B C2 48 8D 0D ?? ?? ?? ?? BE 5B 00 00 00";
        private const string EconomyQuarryPattern =
            "40 53 55 56 48 83 EC 50 8B F2 48 8B D9 44 8B C2";
        private const string EconomyWoodPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 40 8B FA 48 8B D9 44 8B C2 48 8D 0D ?? ?? ?? ?? BE 33 00 00 00";
        private const string FarmSearchPattern =
            "44 89 44 24 18 89 54 24 10 53 41 57 48 81 EC A8 00 00 00";
        private const string ResourceSearchPattern =
            "41 54 41 55 48 83 EC 18 45 33 ED 48 C7 81 30 78 18 00 01 00 00 00";
        private const string WoodSearchPattern =
            "40 53 55 41 56 41 57 48 83 EC 18 33 C0 48 C7 81 30 78 18 00 01 00 00 00";
        private const string NearbySearchPattern =
            "41 56 48 83 EC 10 48 C7 81 30 78 18 00 01 00 00 00 45 33 F6";
        private const string ConstructBuildingPattern =
            "89 54 24 10 53 55 56 57 41 55 41 56 41 57 48 83 EC 70";
        private const string RegionPairReachabilityPattern =
            "40 55 41 54 41 55 41 56 48 8D AC 24 78 F7 FF FF 48 81 EC 88 09 00 00";
        private const string EconomyGridUpdatePattern =
            "40 53 56 48 83 EC 38 83 3D ?? ?? ?? ?? 00 8B F2 48 8B D9 0F 84";
        private const string SelectDominantPclPattern =
            "40 53 48 83 EC 20 48 8D 1D ?? ?? ?? ?? 45 33 C0 4C 8B CB 48 8D 0D";
        private const string InitializePlayerBuildingsPattern =
            "48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 BF 01 00 00 00 8B EA";
        private const string InitializeBuildingPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 41 BF 03 00 00 00 48 63";
        private const string ClearBuildingRecordPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 83 3D ?? ?? ?? ?? 10";
        private const string CrushedTimerWriterBlockPattern =
            "4C 8D 15 ?? ?? ?? ?? 41 83 FD 01 75 1D 48 0F BF C2 48 69 C8 3C 58 00 00 42 39 B4 11 B0 D8 79 03 75 08 46 89 AC 11 B0 D8 79 03 4C 8D 2D ?? ?? ?? ??";
        private const string LegacyPlayerStateCopyPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 54 41 56 41 57 48 83 EC 20 48 8D 2D ?? ?? ?? ?? BB 60 0D 03 00";
        private const string PlayerStateChorePattern =
            "48 89 5C 24 08 57 48 83 EC 30 48 63 05 ?? ?? ?? ?? 48 8D 1D ?? ?? ?? ?? 33 FF 48 8D 0C 80 48 C1 E1 08 " +
            "89 BC 19 F8 0B 0B 00 8B 05 ?? ?? ?? ?? C7 05 ?? ?? ?? ?? 40 58 00 00 83 F8 01 75 ?? 45 33 C9";
        private const string ChoreCopyFieldPattern =
            "45 85 C0 0F 8E ?? ?? ?? ?? 48 89 5C 24 08 57 48 83 EC 20 41 8B F8 48 8B D9 48 85 D2 74 ?? 4C 63 91 F8 0B 37 00";
        private const string InitializeUnitSubsystemPattern =
            "48 83 EC 28 4C 8D 0D ?? ?? ?? ?? 45 33 C0 BA 5C 37 10 00 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ??";
        private const string ResetMapObjectSubsystemPattern =
            "40 53 48 83 EC 20 83 39 00 48 8B D9 74 15 E8 ?? ?? ?? ?? C7 83 4C 09 00 00 00 00 00 00";
        private const string InitializePlayerPathingPattern =
            "48 89 5C 24 08 48 89 74 24 10 48 89 7C 24 18 41 56 48 83 EC 30 BB 01 00 00 00 4C 8D 35 ?? ?? ?? ??";

        private const int AllocateSpecRva = 0x50680;
        private const int SetPlacementRva = 0x54EC0;
        private const int SelectBestFitRva = 0x54F60;
        private const int TestSpecificCandidateRva = 0x54DE0;
        private const int LoadCandidateRva = 0x55320;
        private const int ApplyRotationRva = 0x56670;
        private const int EvaluateCandidateFitRva = 0x57080;
        private const int PrepareLayoutRva = 0x53D00;
        private const int SchedulerRva = 0x539B0;
        private const int ExecuteBuildStepRva = 0x51790;
        private const int AlternativeExecutionRva = 0x52270;
        private const int PlacementHelperRva = 0x5CD90;
        private const int ValidatorRva = 0x7B060;
        private const int ActiveLayoutReferenceRva = 0x55F64;
        private const int ResourceGateRva = 0xCC420;
        private const int MapperWaitOneRva = 0x414A0;
        private const int MapperWaitTwoRva = 0x41230;
        private const int MapperWaitThreeRva = 0x41380;
        private const int MapperWaitFourRva = 0x41280;
        private const int DeleteHovelRva = 0x3B1D0;
        private const int MaintenanceOneRva = 0x50340;
        private const int MaintenanceTwoRva = 0x504F0;
        private const int CountBuildingsRva = 0xB8270;
        private const int PlacementReachabilityRva = 0xC3BF0;
        private const int AccessibilitySweepRva = 0xC8F50;
        private const int BuildingAccessibilityRva = 0xC90E0;
        private const int EconomyFarmRva = 0x50D80;
        private const int EconomyIronRva = 0x50E00;
        private const int EconomyOxenRva = 0x50F90;
        private const int EconomyPitchRva = 0x51190;
        private const int EconomyQuarryRva = 0x51270;
        private const int EconomyWoodRva = 0x51540;
        private const int FarmSearchRva = 0x575B0;
        private const int ResourceSearchRva = 0x57B80;
        private const int WoodSearchRva = 0x58020;
        private const int NearbySearchRva = 0x58950;
        private const int ConstructBuildingRva = 0x6D580;
        private const int RegionPairReachabilityRva = 0xE2610;
        private const int EconomyGridUpdateRva = 0x50720;
        private const int SelectDominantPclRva = 0x572B0;
        private const int InitializePlayerBuildingsRva = 0xC3FA0;
        private const int InitializeBuildingRva = 0xC43A0;
        private const int ClearBuildingRecordRva = 0xB8310;
        private const int InitializeUnitSubsystemRva = 0x115830;
        private const int ResetMapObjectSubsystemRva = 0x102C30;
        private const int InitializePlayerPathingRva = 0x2A340;
        private const int FinalMapStartUnitCallSiteRva = 0x96D2C;
        private const int FinalMapStartObjectCallSiteRva = 0x96D38;
        private const int FinalMapStartEconomyGridCallSiteRva = 0x96D49;
        private const int FinalMapStartPathingCallSiteRva = 0x96D55;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int AllocateSpecDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetPlacementDelegate(ulong state, int spec, int keepX, int keepY, int orientation);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SelectBestFitDelegate(ulong state, int spec, byte rotations);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint TestSpecificCandidateDelegate(ulong state, int spec, int candidate);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LoadCandidateDelegate(ulong state, int zeroBasedPlayerId, int candidate);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ApplyRotationDelegate(ulong state, int orientation);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EvaluateCandidateFitDelegate(ulong state, int spec);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PrepareLayoutDelegate(ulong state, int spec, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SchedulerDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ExecuteBuildStepDelegate(ulong state, int playerId, int frame, int restrictedMode, byte freeOrForced);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long AlternativeExecutionDelegate(ulong state, int playerId, int pausedMode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long PlacementHelperDelegate(ulong manager, int playerId, int x, int y, int mapperValue, int orientation);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ValidatorDelegate(ulong state, int tileId, int playerId, int mapperValue, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ResourceGateDelegate(ulong manager, int mapperValue, int playerId, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MapperGateDelegate(ulong manager, int playerId, int mapperValue);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PlayerAiDelegate(ulong manager, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void MaintenanceDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CountBuildingsDelegate(ulong manager, int playerId, int structureType, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int PlacementReachabilityDelegate(ulong manager, int playerId, int structureType, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void AccessibilitySweepDelegate(ulong manager, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int BuildingAccessibilityDelegate(ulong manager, int buildingId, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long EconomyFarmDelegate(ulong state, int playerId, int desiredStructureType);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void EconomyPlayerDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long FarmSearchDelegate(ulong state, int playerId, int desiredStructureType);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ResourceSearchDelegate(ulong state, int playerId, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void WoodSearchDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void NearbySearchDelegate(ulong state, uint coarseX, uint coarseY);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ConstructBuildingDelegate(
            ulong state, int playerId, int x, int y, short mapperValue, int orientation, int mode, byte suppressPostProcessing);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int RegionPairReachabilityDelegate(
            ulong pathManager, int playerId, int targetPcl, int sourcePcl, int routeMode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void EconomyGridUpdateDelegate(ulong state, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SelectDominantPclDelegate(ulong state);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PlayerBuildingInitializationDelegate(ulong manager, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void BuildingInitializationDelegate(ulong manager, int buildingId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void LegacyPlayerStateCopyDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void InitializationStateDelegate(ulong state);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PlayerStateChoreDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ChoreCopyFieldDelegate(
            ulong manager, ulong fieldAddress, int size, int bufferMode, int direction);

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, PlayerSession> players = new Dictionary<int, PlayerSession>();
        private readonly EarlyOwnerEventBuffer earlyOwnerEvents = new EarlyOwnerEventBuffer(1, MaxPlayablePlayerId);
        private readonly Dictionary<int, List<InventoryRecord>> earlyOwnerInventories = new Dictionary<int, List<InventoryRecord>>();
        private readonly List<InventoryRecord> pendingRawInventories = new List<InventoryRecord>();
        private readonly Stack<DamageContext> pendingDamage = new Stack<DamageContext>();
        private readonly DiagnosticCounterSet unattributedCounters = new DiagnosticCounterSet();
        private readonly DetourHandle<AllocateSpecDelegate> allocateHook = new DetourHandle<AllocateSpecDelegate>();
        private readonly DetourHandle<SetPlacementDelegate> setPlacementHook = new DetourHandle<SetPlacementDelegate>();
        private readonly DetourHandle<SelectBestFitDelegate> selectHook = new DetourHandle<SelectBestFitDelegate>();
        private readonly DetourHandle<TestSpecificCandidateDelegate> specificHook = new DetourHandle<TestSpecificCandidateDelegate>();
        private readonly DetourHandle<LoadCandidateDelegate> loadHook = new DetourHandle<LoadCandidateDelegate>();
        private readonly DetourHandle<ApplyRotationDelegate> rotationHook = new DetourHandle<ApplyRotationDelegate>();
        private readonly DetourHandle<EvaluateCandidateFitDelegate> fitHook = new DetourHandle<EvaluateCandidateFitDelegate>();
        private readonly DetourHandle<PrepareLayoutDelegate> prepareHook = new DetourHandle<PrepareLayoutDelegate>();
        private readonly DetourHandle<SchedulerDelegate> schedulerHook = new DetourHandle<SchedulerDelegate>();
        private readonly DetourHandle<ExecuteBuildStepDelegate> executeHook = new DetourHandle<ExecuteBuildStepDelegate>();
        private readonly DetourHandle<AlternativeExecutionDelegate> alternativeHook = new DetourHandle<AlternativeExecutionDelegate>();
        private readonly DetourHandle<PlacementHelperDelegate> placementHook = new DetourHandle<PlacementHelperDelegate>();
        private readonly DetourHandle<ValidatorDelegate> validatorHook = new DetourHandle<ValidatorDelegate>();
        private readonly DetourHandle<ResourceGateDelegate> resourceGateHook = new DetourHandle<ResourceGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitOneHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitTwoHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitThreeHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<MapperGateDelegate> mapperWaitFourHook = new DetourHandle<MapperGateDelegate>();
        private readonly DetourHandle<PlayerAiDelegate> deleteHovelHook = new DetourHandle<PlayerAiDelegate>();
        private readonly DetourHandle<MaintenanceDelegate> maintenanceOneHook = new DetourHandle<MaintenanceDelegate>();
        private readonly DetourHandle<MaintenanceDelegate> maintenanceTwoHook = new DetourHandle<MaintenanceDelegate>();
        private readonly DetourHandle<CountBuildingsDelegate> countBuildingsHook = new DetourHandle<CountBuildingsDelegate>();
        private readonly DetourHandle<PlacementReachabilityDelegate> placementReachabilityHook = new DetourHandle<PlacementReachabilityDelegate>();
        private readonly DetourHandle<AccessibilitySweepDelegate> accessibilitySweepHook = new DetourHandle<AccessibilitySweepDelegate>();
        private readonly DetourHandle<BuildingAccessibilityDelegate> buildingAccessibilityHook = new DetourHandle<BuildingAccessibilityDelegate>();
        private readonly DetourHandle<EconomyFarmDelegate> economyFarmHook = new DetourHandle<EconomyFarmDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyIronHook = new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyOxenHook = new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyPitchHook = new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyQuarryHook = new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<EconomyPlayerDelegate> economyWoodHook = new DetourHandle<EconomyPlayerDelegate>();
        private readonly DetourHandle<FarmSearchDelegate> farmSearchHook = new DetourHandle<FarmSearchDelegate>();
        private readonly DetourHandle<ResourceSearchDelegate> resourceSearchHook = new DetourHandle<ResourceSearchDelegate>();
        private readonly DetourHandle<WoodSearchDelegate> woodSearchHook = new DetourHandle<WoodSearchDelegate>();
        private readonly DetourHandle<NearbySearchDelegate> nearbySearchHook = new DetourHandle<NearbySearchDelegate>();
        private readonly DetourHandle<ConstructBuildingDelegate> constructBuildingHook = new DetourHandle<ConstructBuildingDelegate>();
        private readonly DetourHandle<RegionPairReachabilityDelegate> regionPairReachabilityHook = new DetourHandle<RegionPairReachabilityDelegate>();
        private readonly DetourHandle<EconomyGridUpdateDelegate> economyGridUpdateHook = new DetourHandle<EconomyGridUpdateDelegate>();
        private readonly DetourHandle<SelectDominantPclDelegate> selectDominantPclHook = new DetourHandle<SelectDominantPclDelegate>();
        private readonly DetourHandle<PlayerBuildingInitializationDelegate> initializePlayerBuildingsHook = new DetourHandle<PlayerBuildingInitializationDelegate>();
        private readonly DetourHandle<BuildingInitializationDelegate> initializeBuildingHook = new DetourHandle<BuildingInitializationDelegate>();
        private readonly DetourHandle<BuildingInitializationDelegate> clearBuildingRecordHook = new DetourHandle<BuildingInitializationDelegate>();
        private readonly DetourHandle<LegacyPlayerStateCopyDelegate> legacyPlayerStateCopyHook = new DetourHandle<LegacyPlayerStateCopyDelegate>();
        private readonly DetourHandle<InitializationStateDelegate> initializeUnitSubsystemHook = new DetourHandle<InitializationStateDelegate>();
        private readonly DetourHandle<InitializationStateDelegate> resetMapObjectSubsystemHook = new DetourHandle<InitializationStateDelegate>();
        private readonly DetourHandle<InitializationStateDelegate> initializePlayerPathingHook = new DetourHandle<InitializationStateDelegate>();
        private readonly DetourHandle<PlayerStateChoreDelegate> playerStateChoreHook = new DetourHandle<PlayerStateChoreDelegate>();
        private readonly DetourHandle<ChoreCopyFieldDelegate> choreCopyFieldHook = new DetourHandle<ChoreCopyFieldDelegate>();
        private readonly HookHandle<X64InlineHook> crushedTimerWriterHook = new HookHandle<X64InlineHook>();
        private readonly object crushedWriterSync = new object();
        private readonly Queue<CrushedWriterSignal> pendingCrushedWriterSignals = new Queue<CrushedWriterSignal>();
        private HookTransaction transaction;
        private ulong activeLayoutIndexBase;
        private ulong nativeModuleBase;
        private ulong nativePathManagerBase;
        private ushort* nativePclGrid;
        private ulong lastAivState;
        private int mapSequence;
        private ExecuteContext activeExecute;
        private int activeSelectionPlayerId;
        private int activeAccessibilityPlayerId;
        private List<AccessibilityCallSnapshot> activeAccessibilityCalls;
        [ThreadStatic] private static Stack<EconomyContext> activeEconomyContexts;
        private bool mapActive;
        private bool aiOwnershipResolved;
        private string lastObservedPhase = "plugin-start";
        private readonly Dictionary<int, int> lastCrushedCounters = new Dictionary<int, int>();
        private readonly Dictionary<int, PreplacedIdentity> preplacedBuildings = new Dictionary<int, PreplacedIdentity>();
        private readonly Dictionary<long, List<PreplacedIdentity>> preplacedByOwnerAndType =
            new Dictionary<long, List<PreplacedIdentity>>();
        private readonly Dictionary<int, BuildingSnapshot> lastRawBuildings = new Dictionary<int, BuildingSnapshot>();
        private DateTime nextUnattributedFlushUtc = DateTime.UtcNow.AddSeconds(1);
        private DateTime nextRawDeltaUtc = DateTime.UtcNow.AddSeconds(1);
        private bool unattributedFinalized;
        private EconomyRoutingSnapshot lastRoutingSnapshot;
        private ushort[] lastPclTopology;
        private DateTime nextRoutingPollUtc = DateTime.UtcNow.AddSeconds(1);
        private readonly HashSet<string> emittedGridUpdateSignatures = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> emittedDominantPclSignatures = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> emittedInvalidPclAccesses = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> emittedShadowSearchSignatures = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> emittedInitializationCheckpoints = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, WallTileBaseline> wallBaselines = new Dictionary<int, WallTileBaseline>();
        private bool initializationTracingActive;
        private bool currentMapIsSave;
        private bool loadSaveEventObserved;
        private bool loadingEditorMap;
        private int lastLegacyCopyMapVersion = -1;
        private int[] lastLegacyCopySourceBefore;
        private int[] lastLegacyCopyDestinationBefore;
        private int[] lastLegacyCopySource;
        private int[] lastLegacyCopyDestination;
        private List<BuildingSnapshot> legacyCopyBuildings = new List<BuildingSnapshot>();
        private readonly Dictionary<int, List<BuildingSnapshot>> crushedActivationBuildings =
            new Dictionary<int, List<BuildingSnapshot>>();

        public PreplacedTestRuntime(ManualLogSource log) => this.log = log ?? throw new ArgumentNullException(nameof(log));

        public void InstallEventDiagnostics()
        {
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(a => OnMapLoad(a)));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable.Subscribe(a => OnLoadSave(a)));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(a => OnMapStart(a)));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(a => OnMapUnload(a)));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuildStructure));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(OnPlacementValidation));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(OnBuildingDamage));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(OnBuildingBulldoze));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(OnBuildingDelete));
            Shared.DebugLogHelper.LogInfo(log, "PREPLACED_EVENTS_READY: passive Script-Extender lifecycle/building diagnostics installed.");
        }

        public void TryInstallNativeDiagnostics(CrusaderLibraryLoadContext context, bool hashMatches)
        {
            if (!hashMatches)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "PREPLACED_NATIVE_INCOMPLETE: native hash differs; all native AIV hooks remain atomically disabled, event diagnostics continue.");
                return;
            }
            try
            {
                Dictionary<string, int> rvas = ResolveAll(context.Memory);
                ValidateManagedLayouts();
                long pathManagerEnd = NativePathManagerRva +
                    ((long)(MaximumPortalRecordCount - 1) * PortalRecordStrideDwords +
                    PortalThirdPclOffsetDwords + 1) * sizeof(int);
                if (NativePathManagerRva < 0 || pathManagerEnd > context.Memory.Length)
                    throw new InvalidOperationException("native path-manager range is outside the image");
                if (NativePclGridRva < 0 || NativePclGridEndRva > context.Memory.Length ||
                    NativePclEntryCount != 320800)
                    throw new InvalidOperationException("audited native PCL-grid range is outside the image or has the wrong length");
                activeLayoutIndexBase = ResolveRipAddress(context, rvas["active-layout-reference"] + 3, 3, 7);
                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                if (activeLayoutIndexBase != module + ActivePlayerRuntimeStateBaseRva)
                    throw new InvalidOperationException("active player runtime-state base differs from the audited serialized-record offset");
                nativeModuleBase = module;
                nativePathManagerBase = module + NativePathManagerRva;
                nativePclGrid = (ushort*)(module + NativePclGridRva);
                int writerRva = checked(rvas["crushed-timer-writer-block"] +
                    (CrushedTimerWriterRva - CrushedTimerWriterBlockRva));
                ValidateCrushedTimerWriterBytes(context.Memory, writerRva);
                ValidateLegacyPlayerStateCopy(context.Memory, rvas["legacy-player-state-copy"]);
                ValidateChorePlayerStateRanges(context.Memory, rvas);
                ValidateFinalMapStartSequence(context.Memory, rvas);
                using (var probe = new X64InlineHook(module + (ulong)writerRva,
                    CrushedTimerWriterDisplacedLength))
                {
                    if (probe.DisplacedByteCount != CrushedTimerWriterDisplacedLength)
                        throw new InvalidOperationException("unexpected RedBird crushed-timer writer displacement length");
                }
                transaction = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = false });
                transaction.AddDetour(allocateHook, HookTarget.FromAddress(module + (ulong)rvas["allocate"]), AllocateSpec);
                transaction.AddDetour(setPlacementHook, HookTarget.FromAddress(module + (ulong)rvas["set-placement"]), SetPlacement);
                transaction.AddDetour(selectHook, HookTarget.FromAddress(module + (ulong)rvas["select"]), SelectBestFit);
                transaction.AddDetour(specificHook, HookTarget.FromAddress(module + (ulong)rvas["specific"]), TestSpecificCandidate);
                transaction.AddDetour(loadHook, HookTarget.FromAddress(module + (ulong)rvas["load"]), LoadCandidate);
                transaction.AddDetour(rotationHook, HookTarget.FromAddress(module + (ulong)rvas["rotation"]), ApplyRotation);
                transaction.AddDetour(fitHook, HookTarget.FromAddress(module + (ulong)rvas["fit"]), EvaluateCandidateFit);
                transaction.AddDetour(prepareHook, HookTarget.FromAddress(module + (ulong)rvas["prepare"]), PrepareLayout);
                transaction.AddDetour(schedulerHook, HookTarget.FromAddress(module + (ulong)rvas["scheduler"]), Scheduler);
                transaction.AddDetour(executeHook, HookTarget.FromAddress(module + (ulong)rvas["execute"]), ExecuteBuildStep);
                transaction.AddDetour(alternativeHook, HookTarget.FromAddress(module + (ulong)rvas["alternative"]), AlternativeExecution);
                transaction.AddDetour(placementHook, HookTarget.FromAddress(module + (ulong)rvas["placement-helper"]), PlacementHelper);
                transaction.AddDetour(validatorHook, HookTarget.FromAddress(module + (ulong)rvas["validator"]), Validator);
                transaction.AddDetour(resourceGateHook, HookTarget.FromAddress(module + (ulong)rvas["resource-gate"]), ResourceGate);
                transaction.AddDetour(mapperWaitOneHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-1"]), MapperWaitOne);
                transaction.AddDetour(mapperWaitTwoHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-2"]), MapperWaitTwo);
                transaction.AddDetour(mapperWaitThreeHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-3"]), MapperWaitThree);
                transaction.AddDetour(mapperWaitFourHook, HookTarget.FromAddress(module + (ulong)rvas["mapper-wait-4"]), MapperWaitFour);
                transaction.AddDetour(deleteHovelHook, HookTarget.FromAddress(module + (ulong)rvas["delete-hovel"]), DeleteHovel);
                transaction.AddDetour(maintenanceOneHook, HookTarget.FromAddress(module + (ulong)rvas["maintenance-1"]), MaintenanceOne);
                transaction.AddDetour(maintenanceTwoHook, HookTarget.FromAddress(module + (ulong)rvas["maintenance-2"]), MaintenanceTwo);
                transaction.AddDetour(countBuildingsHook, HookTarget.FromAddress(module + (ulong)rvas["count-buildings"]), CountBuildings);
                transaction.AddDetour(placementReachabilityHook, HookTarget.FromAddress(module + (ulong)rvas["placement-reachability"]), PlacementReachability);
                transaction.AddDetour(accessibilitySweepHook, HookTarget.FromAddress(module + (ulong)rvas["accessibility-sweep"]), AccessibilitySweep);
                transaction.AddDetour(buildingAccessibilityHook, HookTarget.FromAddress(module + (ulong)rvas["building-accessibility"]), BuildingAccessibility);
                transaction.AddDetour(economyFarmHook, HookTarget.FromAddress(module + (ulong)rvas["economy-farm"]), EconomyFarm);
                transaction.AddDetour(economyIronHook, HookTarget.FromAddress(module + (ulong)rvas["economy-iron"]), EconomyIron);
                transaction.AddDetour(economyOxenHook, HookTarget.FromAddress(module + (ulong)rvas["economy-oxen"]), EconomyOxen);
                transaction.AddDetour(economyPitchHook, HookTarget.FromAddress(module + (ulong)rvas["economy-pitch"]), EconomyPitch);
                transaction.AddDetour(economyQuarryHook, HookTarget.FromAddress(module + (ulong)rvas["economy-quarry"]), EconomyQuarry);
                transaction.AddDetour(economyWoodHook, HookTarget.FromAddress(module + (ulong)rvas["economy-wood"]), EconomyWood);
                transaction.AddDetour(farmSearchHook, HookTarget.FromAddress(module + (ulong)rvas["farm-search"]), FarmSearch);
                transaction.AddDetour(resourceSearchHook, HookTarget.FromAddress(module + (ulong)rvas["resource-search"]), ResourceSearch);
                transaction.AddDetour(woodSearchHook, HookTarget.FromAddress(module + (ulong)rvas["wood-search"]), WoodSearch);
                transaction.AddDetour(nearbySearchHook, HookTarget.FromAddress(module + (ulong)rvas["nearby-search"]), NearbySearch);
                transaction.AddDetour(constructBuildingHook, HookTarget.FromAddress(module + (ulong)rvas["construct-building"]), ConstructBuilding);
                transaction.AddDetour(regionPairReachabilityHook, HookTarget.FromAddress(module + (ulong)rvas["region-pair-reachability"]), RegionPairReachability);
                transaction.AddDetour(economyGridUpdateHook, HookTarget.FromAddress(module + (ulong)rvas["economy-grid-update"]), EconomyGridUpdate);
                transaction.AddDetour(selectDominantPclHook, HookTarget.FromAddress(module + (ulong)rvas["select-dominant-pcl"]), SelectDominantPcl);
                transaction.AddDetour(initializePlayerBuildingsHook, HookTarget.FromAddress(module + (ulong)rvas["initialize-player-buildings"]), InitializePlayerBuildings);
                transaction.AddDetour(initializeBuildingHook, HookTarget.FromAddress(module + (ulong)rvas["initialize-building"]), InitializeBuilding);
                transaction.AddDetour(clearBuildingRecordHook, HookTarget.FromAddress(module + (ulong)rvas["clear-building-record"]), ClearBuildingRecord);
                transaction.AddDetour(legacyPlayerStateCopyHook,
                    HookTarget.FromAddress(module + (ulong)rvas["legacy-player-state-copy"]), LegacyPlayerStateCopy);
                transaction.AddDetour(initializeUnitSubsystemHook,
                    HookTarget.FromAddress(module + (ulong)rvas["initialize-unit-subsystem"]), InitializeUnitSubsystem);
                transaction.AddDetour(resetMapObjectSubsystemHook,
                    HookTarget.FromAddress(module + (ulong)rvas["reset-map-object-subsystem"]), ResetMapObjectSubsystem);
                transaction.AddDetour(initializePlayerPathingHook,
                    HookTarget.FromAddress(module + (ulong)rvas["initialize-player-pathing"]), InitializePlayerPathing);
                transaction.AddDetour(playerStateChoreHook,
                    HookTarget.FromAddress(module + (ulong)rvas["player-state-chore"]), PlayerStateChore);
                transaction.AddDetour(choreCopyFieldHook,
                    HookTarget.FromAddress(module + (ulong)rvas["chore-copy-field"]), ChoreCopyField);
                transaction.AddContextHook(crushedTimerWriterHook, HookTarget.FromAddress(module + (ulong)writerRva),
                    ObserveCrushedTimerWriter, new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = CrushedTimerWriterDisplacedLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        // RedBird executes the displaced store and LEA before this callback.
                        Placement = OverwrittenInstructionPlacement.BeforeCallback
                    });
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !AllHooksSucceeded())
                    throw new InvalidOperationException("atomic native hook transaction was incomplete: " + result);
                if (crushedTimerWriterHook.Hook.DisplacedByteCount != CrushedTimerWriterDisplacedLength)
                    throw new InvalidOperationException("installed crushed-timer writer hook displaced an unexpected byte range");
                Shared.DebugLogHelper.LogInfo(log,
                    $"PREPLACED_NATIVE_READY: 48 passive detours and one passive writer context hook installed atomically; activeLayoutBase=0x{activeLayoutIndexBase:X}, pathManagerBase=0x{nativePathManagerBase:X}, pclRange=0x{NativePclGridRva:X}-0x{NativePclGridEndRva:X} ({NativePclEntryCount} ushorts)." );
            }
            catch (Exception ex)
            {
                // RollbackAndThrow handles commit failures; this also covers a defensive
                // post-commit handle-consistency failure before exposing native diagnostics.
                try { transaction?.DisableAll(); } catch { }
                activeLayoutIndexBase = 0;
                nativeModuleBase = 0;
                nativePathManagerBase = 0;
                nativePclGrid = null;
                lastAivState = 0;
                Shared.DebugLogHelper.LogError(log,
                    $"PREPLACED_NATIVE_INCOMPLETE: signature/ABI/address validation failed; native hook set rolled back, event diagnostics continue. {ex}");
            }
        }

        private Dictionary<string, int> ResolveAll(ReadOnlySpan<byte> memory)
        {
            var definitions = new[]
            {
                Def("allocate", AllocateSpecPattern, AllocateSpecRva), Def("set-placement", SetPlacementPattern, SetPlacementRva),
                Def("select", SelectBestFitPattern, SelectBestFitRva), Def("specific", TestSpecificCandidatePattern, TestSpecificCandidateRva),
                Def("load", LoadCandidatePattern, LoadCandidateRva), Def("rotation", ApplyRotationPattern, ApplyRotationRva),
                Def("fit", EvaluateCandidateFitPattern, EvaluateCandidateFitRva), Def("prepare", PrepareLayoutPattern, PrepareLayoutRva),
                Def("scheduler", SchedulerPattern, SchedulerRva), Def("execute", ExecuteBuildStepPattern, ExecuteBuildStepRva),
                Def("alternative", AlternativeExecutionPattern, AlternativeExecutionRva),
                Def("placement-helper", PlacementHelperPattern, PlacementHelperRva), Def("validator", ValidatorPattern, ValidatorRva),
                Def("resource-gate", ResourceGatePattern, ResourceGateRva),
                Def("mapper-wait-1", MapperWaitOnePattern, MapperWaitOneRva), Def("mapper-wait-2", MapperWaitTwoPattern, MapperWaitTwoRva),
                Def("mapper-wait-3", MapperWaitThreePattern, MapperWaitThreeRva), Def("mapper-wait-4", MapperWaitFourPattern, MapperWaitFourRva),
                Def("delete-hovel", DeleteHovelPattern, DeleteHovelRva), Def("maintenance-1", MaintenanceOnePattern, MaintenanceOneRva),
                Def("maintenance-2", MaintenanceTwoPattern, MaintenanceTwoRva),
                Def("count-buildings", CountBuildingsPattern, CountBuildingsRva),
                Def("placement-reachability", PlacementReachabilityPattern, PlacementReachabilityRva),
                Def("accessibility-sweep", AccessibilitySweepPattern, AccessibilitySweepRva),
                Def("building-accessibility", BuildingAccessibilityPattern, BuildingAccessibilityRva),
                Def("economy-farm", EconomyFarmPattern, EconomyFarmRva),
                Def("economy-iron", EconomyIronPattern, EconomyIronRva),
                Def("economy-oxen", EconomyOxenPattern, EconomyOxenRva),
                Def("economy-pitch", EconomyPitchPattern, EconomyPitchRva),
                Def("economy-quarry", EconomyQuarryPattern, EconomyQuarryRva),
                Def("economy-wood", EconomyWoodPattern, EconomyWoodRva),
                Def("farm-search", FarmSearchPattern, FarmSearchRva),
                Def("resource-search", ResourceSearchPattern, ResourceSearchRva),
                Def("wood-search", WoodSearchPattern, WoodSearchRva),
                Def("nearby-search", NearbySearchPattern, NearbySearchRva),
                Def("construct-building", ConstructBuildingPattern, ConstructBuildingRva),
                Def("region-pair-reachability", RegionPairReachabilityPattern, RegionPairReachabilityRva),
                Def("economy-grid-update", EconomyGridUpdatePattern, EconomyGridUpdateRva),
                Def("select-dominant-pcl", SelectDominantPclPattern, SelectDominantPclRva),
                Def("initialize-player-buildings", InitializePlayerBuildingsPattern, InitializePlayerBuildingsRva),
                Def("initialize-building", InitializeBuildingPattern, InitializeBuildingRva),
                Def("clear-building-record", ClearBuildingRecordPattern, ClearBuildingRecordRva),
                Def("legacy-player-state-copy", LegacyPlayerStateCopyPattern, LegacyPlayerStateCopyRva),
                Def("initialize-unit-subsystem", InitializeUnitSubsystemPattern, InitializeUnitSubsystemRva),
                Def("reset-map-object-subsystem", ResetMapObjectSubsystemPattern, ResetMapObjectSubsystemRva),
                Def("initialize-player-pathing", InitializePlayerPathingPattern, InitializePlayerPathingRva),
                Def("player-state-chore", PlayerStateChorePattern, PlayerStateChoreRva),
                Def("chore-copy-field", ChoreCopyFieldPattern, ChoreCopyFieldRva),
                Def("crushed-timer-writer-block", CrushedTimerWriterBlockPattern, CrushedTimerWriterBlockRva),
                Def("active-layout-reference", ActiveLayoutReferencePattern, ActiveLayoutReferenceRva)
            };
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (NativeDefinition definition in definitions)
                result.Add(definition.Name, Shared.NativePatternResolver.ResolveUnique(memory, definition.Pattern,
                    definition.Rva, true, "PreplacedTest " + definition.Name, log).Rva);
            return result;
        }

        private static NativeDefinition Def(string name, string pattern, int rva) => new NativeDefinition(name, pattern, rva);

        private static void ValidateCrushedTimerWriterBytes(ReadOnlySpan<byte> memory, int writerRva)
        {
            byte[] expected = { 0x46, 0x89, 0xAC, 0x11, 0xB0, 0xD8, 0x79, 0x03,
                0x4C, 0x8D, 0x2D, 0x2D, 0xDB, 0x44, 0x06 };
            if (writerRva != CrushedTimerWriterRva || writerRva < 0 ||
                writerRva + expected.Length > memory.Length ||
                writerRva < BuildingDamageFunctionRva ||
                writerRva + expected.Length > BuildingDamageFunctionRva + BuildingDamageFunctionLength ||
                !memory.Slice(writerRva, expected.Length).SequenceEqual(expected))
                throw new InvalidOperationException("crushed-timer writer bytes or RVA differ from the audited FBCB9319 contract");
        }

        private static void ValidateLegacyPlayerStateCopy(ReadOnlySpan<byte> memory, int functionRva)
        {
            if (functionRva != LegacyPlayerStateCopyRva ||
                LegacyPlayerStateCopyCallSiteRva < 0 || LegacyPlayerStateCopyCallSiteRva + 5 > memory.Length ||
                memory[LegacyPlayerStateCopyCallSiteRva] != 0xE8 ||
                !memory.Slice(LegacyPlayerStateCopyCallSiteRva - 8, 8)
                    .SequenceEqual(new byte[] { 0x81, 0xFA, 0xD5, 0x00, 0x00, 0x00, 0x7D, 0x0B }))
                throw new InvalidOperationException("legacy player-state copy function or call-site bytes differ");
            int target = Shared.NativePatternResolver.ResolveRelativeTarget(memory,
                LegacyPlayerStateCopyCallSiteRva + 1, LegacyPlayerStateCopyCallSiteRva + 5);
            if (target != functionRva)
                throw new InvalidOperationException("legacy player-state copy call-site target differs");
            long sourceEnd = LegacyPlayerStateSourceRva +
                (long)SerializedPlayerRecordCount * LegacyPlayerStateStride;
            long destinationEnd = CurrentPlayerStateDestinationRva +
                (long)SerializedPlayerRecordCount * PlayerRuntimeStateStride;
            if (sourceEnd > memory.Length || destinationEnd > memory.Length ||
                MapFormatVersionRva + sizeof(int) > memory.Length ||
                SerializedCrushedCounterOffset + sizeof(int) > LegacyPlayerStateStride)
                throw new InvalidOperationException("legacy player-state copy data ranges differ");
        }

        private static void ValidateChorePlayerStateRanges(ReadOnlySpan<byte> memory,
            Dictionary<string, int> rvas)
        {
            long playerStateEnd = CurrentPlayerStateDestinationRva +
                (long)SerializedPlayerRecordCount * PlayerRuntimeStateStride;
            long choreLinearBufferEnd = NativeChoreManagerRva + ChoreLinearBufferOffset + ChoreBufferCapacity;
            if (rvas["player-state-chore"] != PlayerStateChoreRva ||
                rvas["chore-copy-field"] != ChoreCopyFieldRva ||
                playerStateEnd > memory.Length || choreLinearBufferEnd > memory.Length ||
                CurrentChoreRecordIndexRva + sizeof(int) > memory.Length ||
                ChoreDirectionRva + sizeof(int) > memory.Length ||
                ChoreBlockedRva + sizeof(int) > memory.Length ||
                FarmSearchMapGateRva + sizeof(int) > memory.Length ||
                PlayerClassTableRva + SerializedPlayerRecordCount * sizeof(int) > memory.Length ||
                SerializedCrushedCounterOffset + sizeof(int) > PlayerRuntimeStateStride ||
                PitchBuiltCountRelativeOffset + sizeof(int) > PlayerRuntimeStateStride ||
                PlayerResourcesOffsetInSerializedRecord != 0x22FC ||
                SerializedCrushedCounterOffset != 0x2AE0)
                throw new InvalidOperationException("player-state chore/copy ranges differ from the audited FBCB9319 contract");

            byte[] recordCopySetup =
            {
                0x48, 0x63, 0x05, 0xFF, 0xB6, 0x6A, 0x08, 0x45, 0x33, 0xC9,
                0x48, 0x69, 0xD0, 0x3C, 0x58, 0x00, 0x00,
                0x48, 0x8D, 0x05, 0x92, 0x51, 0x78, 0x03,
                0x41, 0xB8, 0x3C, 0x58, 0x00, 0x00, 0x48, 0x03, 0xD0, 0x48, 0x8B, 0xCB, 0xE8
            };
            const int recordCopySetupRva = 0x15C26;
            if (recordCopySetupRva + recordCopySetup.Length > memory.Length ||
                !memory.Slice(recordCopySetupRva, recordCopySetup.Length).SequenceEqual(recordCopySetup) ||
                Shared.NativePatternResolver.ResolveRelativeTarget(memory, recordCopySetupRva + 3,
                    recordCopySetupRva + 7) != CurrentChoreRecordIndexRva ||
                Shared.NativePatternResolver.ResolveRelativeTarget(memory, recordCopySetupRva + 20,
                    recordCopySetupRva + 24) != CurrentPlayerStateDestinationRva ||
                Shared.NativePatternResolver.ResolveRelativeTarget(memory, PlayerStateRecordCopyCallSiteRva + 1,
                    PlayerStateRecordCopyCallSiteRva + 5) != ChoreCopyFieldRva)
                throw new InvalidOperationException("player-state chore full-record call contract differs");

            if (ChoreCopyFieldEndRva > memory.Length || memory[ChoreCopyFieldEndRva - 1] != 0xC3 ||
                memory[ChoreCopyFieldMemcpyCallSiteRva] != 0xE8 ||
                Shared.NativePatternResolver.ResolveRelativeTarget(memory, ChoreCopyFieldMemcpyCallSiteRva + 1,
                    ChoreCopyFieldMemcpyCallSiteRva + 5) != 0x7140 ||
                !memory.Slice(ChoreCopyFieldMemcpyCallSiteRva + 5, 18).SequenceEqual(new byte[]
                {
                    0x01, 0xBB, 0xF8, 0x0B, 0x37, 0x00, 0x8B, 0x93, 0xF8,
                    0x0B, 0x37, 0x00, 0x81, 0xFA, 0x20, 0xBF, 0x02, 0x00
                }))
                throw new InvalidOperationException("chore field-copy direction/cursor/function-boundary contract differs");
        }

        private static void ValidateManagedLayouts()
        {
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_AliveState), 0xD0);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType), 0xD2);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId), 0xD8);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionXEnd), 0xFE);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionYEnd), 0x100);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_IsSleeping), 0x296);
            ValidateOffset(typeof(GameBuilding), nameof(GameBuilding.r_GatehouseId), 0x2D2);
            ValidateOffset(typeof(GamePlayerResources), nameof(GamePlayerResources.r_KeepTileId), 0xA0);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_RecordGlobalId), 0x08);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_BuildingId), 0x0C);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_SubjectGlobalId), 0x14);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_IsEnabledOrOpen), 0x18);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_EntryTileId), 0x24);
            ValidateOffset(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_ExitTileId), 0x30);
            ValidateSize(typeof(GameBuilding), 0x32C);
            ValidateSize(typeof(GamePlayerResources), PlayerRuntimeStateStride);
            ValidateSize(typeof(PathConnectionRecord), 0x204);
        }

        private static void ValidateOffset(Type type, string field, int expected)
        {
            int actual = Marshal.OffsetOf(type, field).ToInt32();
            if (actual != expected)
                throw new InvalidOperationException($"managed layout mismatch: {type.Name}.{field}=0x{actual:X}, expected=0x{expected:X}");
        }

        private static void ValidateSize(Type type, int expected)
        {
            int actual = Marshal.SizeOf(type);
            if (actual != expected)
                throw new InvalidOperationException($"managed layout mismatch: sizeof({type.Name})=0x{actual:X}, expected=0x{expected:X}");
        }

        private static ulong ResolveRipAddress(CrusaderLibraryLoadContext context, int instructionRva, int displacementOffset, int length)
        {
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(context.Memory,
                instructionRva + displacementOffset, instructionRva + length);
            if (targetRva < 0 || targetRva >= context.Memory.Length)
                throw new InvalidOperationException("RIP-relative active-layout target is outside the image.");
            return unchecked((ulong)context.ModuleHandle.ToInt64()) + (ulong)targetRva;
        }

        private bool AllHooksSucceeded() => allocateHook.Success && setPlacementHook.Success && selectHook.Success &&
            specificHook.Success && loadHook.Success && rotationHook.Success && fitHook.Success && prepareHook.Success &&
            schedulerHook.Success && executeHook.Success && alternativeHook.Success && placementHook.Success && validatorHook.Success &&
            resourceGateHook.Success && mapperWaitOneHook.Success && mapperWaitTwoHook.Success && mapperWaitThreeHook.Success &&
            mapperWaitFourHook.Success && deleteHovelHook.Success && maintenanceOneHook.Success && maintenanceTwoHook.Success &&
            countBuildingsHook.Success && placementReachabilityHook.Success && accessibilitySweepHook.Success &&
            buildingAccessibilityHook.Success && economyFarmHook.Success && economyIronHook.Success &&
            economyOxenHook.Success && economyPitchHook.Success && economyQuarryHook.Success &&
            economyWoodHook.Success && farmSearchHook.Success && resourceSearchHook.Success &&
            woodSearchHook.Success && nearbySearchHook.Success && constructBuildingHook.Success &&
            regionPairReachabilityHook.Success && economyGridUpdateHook.Success && selectDominantPclHook.Success &&
            initializePlayerBuildingsHook.Success && initializeBuildingHook.Success && clearBuildingRecordHook.Success &&
            legacyPlayerStateCopyHook.Success && initializeUnitSubsystemHook.Success &&
            resetMapObjectSubsystemHook.Success && initializePlayerPathingHook.Success &&
            playerStateChoreHook.Success && choreCopyFieldHook.Success && crushedTimerWriterHook.Success;

        private void EconomyGridUpdate(ulong state, int mode)
        {
            lastAivState = state;
            string phase = lastObservedPhase;
            EconomyGridBuildSnapshot before = null;
            int[] timersBefore = null;
            Safe(() =>
            {
                before = EconomyGridBuildSnapshot.Capture((byte*)state);
                timersBefore = CaptureAllCrushedCounters();
                ObserveCrushedCounters("economy-grid.entry");
            });
            economyGridUpdateHook.Original(state, mode);
            Safe(() =>
            {
                EconomyGridBuildSnapshot after = EconomyGridBuildSnapshot.Capture((byte*)state);
                string kind = mode == 0 ? "periodic-update" : "full-rebuild";
                string signature = kind + "/" + (before?.Signature ?? 0).ToString("X16") + "/" + after.Signature.ToString("X16") +
                    "/reference=" + (before?.ReferencePcl ?? 0) + "->" + after.ReferencePcl;
                unattributedCounters.Add("economy-grid-update kind=" + kind + " phase=" + phase + " signature=" + signature);
                if (emittedGridUpdateSignatures.Add(signature))
                {
                    EmitChunked("PREPLACED_ECONOMY_GRID_UPDATE: ",
                        $"kind={kind}; mode={mode}; phase={phase}; referencePcl={(before?.ReferencePcl ?? 0)}->{after.ReferencePcl}; " +
                        after.DescribeDelta(before));
                }
                CaptureRoutingSnapshot(state, "economy-grid-" + kind, false);
                EmitTimerCheckpointChanges("0x50720-economy-grid-" + kind, 0, 0, timersBefore,
                    CaptureAllCrushedCounters());
                ObserveCrushedCounters("economy-grid-" + kind + ".post");
            });
        }

        private static void ValidateFinalMapStartSequence(ReadOnlySpan<byte> memory,
            Dictionary<string, int> rvas)
        {
            int[] sites = { FinalMapStartUnitCallSiteRva, FinalMapStartObjectCallSiteRva,
                FinalMapStartEconomyGridCallSiteRva, FinalMapStartPathingCallSiteRva };
            int[] targets = { rvas["initialize-unit-subsystem"], rvas["reset-map-object-subsystem"],
                rvas["economy-grid-update"], rvas["initialize-player-pathing"] };
            for (int index = 0; index < sites.Length; index++)
            {
                int site = sites[index];
                if (site < 0 || site + 5 > memory.Length || memory[site] != 0xE8)
                    throw new InvalidOperationException("final map-start checkpoint call-site bytes differ");
                int target = Shared.NativePatternResolver.ResolveRelativeTarget(memory, site + 1, site + 5);
                if (target != targets[index])
                    throw new InvalidOperationException("final map-start checkpoint call target differs");
            }
        }

        private void InitializeUnitSubsystem(ulong state) =>
            RunInitializationCheckpoint("0x115830-unit-subsystem", 0, 0,
                () => initializeUnitSubsystemHook.Original(state));

        private void ResetMapObjectSubsystem(ulong state) =>
            RunInitializationCheckpoint("0x102C30-map-object-reset", 0, 0,
                () => resetMapObjectSubsystemHook.Original(state));

        private void InitializePlayerPathing(ulong state) =>
            RunInitializationCheckpoint("0x2A340-player-pathing", 0, 0,
                () => initializePlayerPathingHook.Original(state));

        private int SelectDominantPcl(ulong state)
        {
            int result = selectDominantPclHook.Original(state);
            Safe(() => EmitDominantPclSelection(result));
            return result;
        }

        private void EmitDominantPclSelection(int selectedPcl)
        {
            SortedDictionary<int, int> counts = CapturePclCounts();
            string distribution = string.Join(",", counts.Select(pair => pair.Key + "=" + pair.Value));
            var playersText = new List<string>();
            List<PortalConnection> portals = CapturePortalConnections(out string portalDetails);
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                if (!IsAi(playerId)) continue;
                int keepPcl = TryGetKeepPcl(playerId, out int capturedKeep) ? capturedKeep : 0;
                PortalRouteResult route = PortalRouteModel.EvaluateEconomyModeZero(keepPcl, selectedPcl, portals);
                playersText.Add($"player={playerId}/keepPcl={keepPcl}/routeToSelected={route.Kind}/portals=[{string.Join(",", route.UsedPortalIds)}]");
            }
            string signature = selectedPcl + "/" + distribution + "/" + string.Join(";", playersText);
            unattributedCounters.Add("dominant-pcl-selection selected=" + selectedPcl);
            if (!emittedDominantPclSignatures.Add(signature)) return;
            EmitChunked("PREPLACED_DOMINANT_PCL: ",
                $"selected={selectedPcl}; positivePclCount={counts.Count}; distribution=[{distribution}]; " +
                $"players=[{string.Join(";", playersText)}]; portalSummary={portalDetails}");
        }

        private SortedDictionary<int, int> CapturePclCounts()
        {
            var result = new SortedDictionary<int, int>();
            if (nativePclGrid == null) return result;
            for (int tileIndex = 0; tileIndex < NativePclEntryCount; tileIndex++)
            {
                int pcl = nativePclGrid[tileIndex];
                if (pcl <= 0) continue;
                result.TryGetValue(pcl, out int count);
                result[pcl] = count + 1;
            }
            return result;
        }

        private void InitializePlayerBuildings(ulong manager, int playerId)
        {
            RunInitializationCheckpoint("0xC3FA0-player-buildings", playerId, 0,
                () => initializePlayerBuildingsHook.Original(manager, playerId));
        }

        private void InitializeBuilding(ulong manager, int buildingId)
        {
            RunInitializationCheckpoint("0xC43A0-building-init", 0, buildingId,
                () => initializeBuildingHook.Original(manager, buildingId));
        }

        private void ClearBuildingRecord(ulong manager, int buildingId)
        {
            RunInitializationCheckpoint("0xB8310-building-clear", 0, buildingId,
                () => clearBuildingRecordHook.Original(manager, buildingId));
        }

        private void LegacyPlayerStateCopy()
        {
            int mapVersion = -1;
            int[] sourceBefore = null;
            int[] destinationBefore = null;
            Safe(() =>
            {
                mapVersion = *(int*)(nativeModuleBase + MapFormatVersionRva);
                sourceBefore = CaptureSerializedCrushedCounters(LegacyPlayerStateSourceRva, LegacyPlayerStateStride);
                destinationBefore = CaptureSerializedCrushedCounters(CurrentPlayerStateDestinationRva,
                    PlayerRuntimeStateStride);
            });

            // Passive detour: Vanilla is always called exactly once, even if capture failed.
            legacyPlayerStateCopyHook.Original();

            Safe(() =>
            {
                int[] sourceAfter = CaptureSerializedCrushedCounters(LegacyPlayerStateSourceRva,
                    LegacyPlayerStateStride);
                int[] destinationAfter = CaptureSerializedCrushedCounters(CurrentPlayerStateDestinationRva,
                    PlayerRuntimeStateStride);
                var rows = new List<string>();
                for (int playerIndex = 0; playerIndex < SerializedPlayerRecordCount; playerIndex++)
                {
                    int sourcePre = sourceBefore != null ? sourceBefore[playerIndex] : int.MinValue;
                    int destinationPre = destinationBefore != null ? destinationBefore[playerIndex] : int.MinValue;
                    rows.Add($"index={playerIndex}/source={FormatCapturedInt(sourcePre)}->{sourceAfter[playerIndex]}" +
                        $"/destination={FormatCapturedInt(destinationPre)}->{destinationAfter[playerIndex]}");
                }
                lastLegacyCopyMapVersion = mapVersion;
                lastLegacyCopySourceBefore = sourceBefore;
                lastLegacyCopyDestinationBefore = destinationBefore;
                lastLegacyCopySource = sourceAfter;
                lastLegacyCopyDestination = destinationAfter;
                legacyCopyBuildings = CaptureRawBuildings();
                EmitChunked("PREPLACED_CRUSHED_TIMER_BULK_COPY: ",
                    $"mapVersion={mapVersion}; legacyThresholdExclusive=0x{LegacyPlayerStateCopyVersionExclusive:X}; mapIsSave={currentMapIsSave}; " +
                    $"loadSaveEventObserved={loadSaveEventObserved}; loadingEditorMap={loadingEditorMap}; " +
                    $"sourceRva=0x{LegacyPlayerStateSourceRva:X}; sourceStride=0x{LegacyPlayerStateStride:X}; " +
                    $"destinationRva=0x{CurrentPlayerStateDestinationRva:X}; destinationStride=0x{PlayerRuntimeStateStride:X}; " +
                    $"playerResourcesOffset=0x{PlayerResourcesOffsetInSerializedRecord:X}; " +
                    $"serializedTimerOffset=0x{SerializedCrushedCounterOffset:X}; records=[{string.Join("; ", rows)}]");
            });
        }

        private void PlayerStateChore()
        {
            int recordIndex = -1;
            int direction = -1;
            int blocked = -1;
            int cursorBefore = -1;
            int bufferedPlayerBefore = -1;
            int bufferedTimerBefore = int.MinValue;
            int[] timersBefore = null;
            Safe(() =>
            {
                recordIndex = *(int*)(nativeModuleBase + CurrentChoreRecordIndexRva);
                direction = *(int*)(nativeModuleBase + ChoreDirectionRva);
                blocked = *(int*)(nativeModuleBase + ChoreBlockedRva);
                byte* manager = (byte*)(nativeModuleBase + NativeChoreManagerRva);
                cursorBefore = *(int*)(manager + ChoreCursorOffset);
                if (cursorBefore >= 0 && (long)cursorBefore + ChorePlayerIndexFieldSize +
                    SerializedCrushedCounterOffset + sizeof(int) <= ChoreBufferCapacity)
                {
                    byte* payload = manager + ChoreLinearBufferOffset + cursorBefore;
                    bufferedPlayerBefore = *(int*)payload;
                    bufferedTimerBefore = *(int*)(payload + ChorePlayerIndexFieldSize + SerializedCrushedCounterOffset);
                }
                timersBefore = CaptureAllCrushedCounters();
                EmitInitializationCheckpointReached("0x15B90-player-state-chore", "entry");
            });

            playerStateChoreHook.Original();

            Safe(() =>
            {
                int cursorAfter = *(int*)(nativeModuleBase + NativeChoreManagerRva + ChoreCursorOffset);
                int bufferedPlayerAfter = -1;
                int bufferedTimerAfter = int.MinValue;
                if (cursorBefore >= 0 && (long)cursorBefore + ChorePlayerIndexFieldSize +
                    SerializedCrushedCounterOffset + sizeof(int) <= ChoreBufferCapacity)
                {
                    byte* payload = (byte*)(nativeModuleBase + NativeChoreManagerRva +
                        ChoreLinearBufferOffset + (ulong)cursorBefore);
                    bufferedPlayerAfter = *(int*)payload;
                    bufferedTimerAfter = *(int*)(payload + ChorePlayerIndexFieldSize + SerializedCrushedCounterOffset);
                }
                int[] timersAfter = CaptureAllCrushedCounters();
                EmitTimerCheckpointChanges("0x15B90-player-state-chore", recordIndex, 0,
                    timersBefore, timersAfter);
                string transfer = ChoreTransferDirection.Classify(direction);
                string destroyed = string.Join(",", CaptureRawBuildings()
                    .Where(value => value.OwnerId == recordIndex && !IsLiving(value))
                    .Select(value => value.Id + "/" + value.GlobalId + "/" + value.Type));
                EmitChunked("PREPLACED_PLAYER_STATE_CHORE: ",
                    $"recordIndex={recordIndex}; bufferedPlayer={bufferedPlayerBefore}->{bufferedPlayerAfter}; directionRaw={direction}; " +
                    $"transfer={transfer}; blocked={blocked}; cursor={cursorBefore}->{cursorAfter}; " +
                    $"bufferTimer={FormatCapturedInt(bufferedTimerBefore)}->{FormatCapturedInt(bufferedTimerAfter)}; " +
                    $"runtimeTimersBefore=[{FormatTimerArray(timersBefore)}]; " +
                    $"runtimeTimersAfter=[{FormatTimerArray(timersAfter)}]; mapIsSave={currentMapIsSave}; " +
                    $"loadSaveEventObserved={loadSaveEventObserved}; phase={lastObservedPhase}; " +
                    $"nonLivingOwnedByRecord=[{destroyed}]");
                EmitInitializationCheckpointReached("0x15B90-player-state-chore", "post");
                ObserveCrushedCounters("player-state-chore.post");
            });
        }

        private void ChoreCopyField(ulong managerAddress, ulong fieldAddress, int size,
            int bufferMode, int direction)
        {
            bool overlapsPlayerState = RangesOverlap(fieldAddress, size,
                nativeModuleBase + CurrentPlayerStateDestinationRva,
                SerializedPlayerRecordCount * PlayerRuntimeStateStride);
            int cursorBefore = -1;
            ulong bufferAddress = 0;
            int[] timersBefore = null;
            int[] bufferTimersBefore = null;
            bool diagnosticCaptureReady = false;
            if (overlapsPlayerState)
            {
                Safe(() =>
                {
                    if (managerAddress != nativeModuleBase + NativeChoreManagerRva) return;
                    byte* manager = (byte*)managerAddress;
                    cursorBefore = *(int*)(manager + ChoreCursorOffset);
                    bufferAddress = ResolveChoreBufferAddress(manager, cursorBefore, size, bufferMode);
                    timersBefore = CaptureAllCrushedCounters();
                    bufferTimersBefore = CaptureTimersFromIntersectingPayload(bufferAddress, size);
                    diagnosticCaptureReady = true;
                    EmitInitializationCheckpointReached("0x1F5F0-player-state-copy", "entry");
                });
            }

            choreCopyFieldHook.Original(managerAddress, fieldAddress, size, bufferMode, direction);

            if (!overlapsPlayerState || !diagnosticCaptureReady) return;
            Safe(() =>
            {
                int cursorAfter = *(int*)((byte*)managerAddress + ChoreCursorOffset);
                int[] timersAfter = CaptureAllCrushedCounters();
                int[] bufferTimersAfter = CaptureTimersFromIntersectingPayload(bufferAddress, size);
                EmitTimerCheckpointChanges("0x1F5F0-player-state-copy", 0, 0,
                    timersBefore, timersAfter);
                EmitChunked("PREPLACED_PLAYER_STATE_FIELD_COPY: ",
                    $"field=0x{fieldAddress:X}; size=0x{size:X}; bufferMode={bufferMode}; directionRaw={direction}; " +
                    $"transfer={ChoreTransferDirection.Classify(direction)}; " +
                    $"buffer=0x{bufferAddress:X}; cursor={cursorBefore}->{cursorAfter}; " +
                    $"runtimeTimers=[{FormatTimerArray(timersBefore)}]->[{FormatTimerArray(timersAfter)}]; " +
                    $"bufferTimers=[{FormatTimerArray(bufferTimersBefore)}]->[{FormatTimerArray(bufferTimersAfter)}]; " +
                    $"phase={lastObservedPhase}");
                EmitInitializationCheckpointReached("0x1F5F0-player-state-copy", "post");
                ObserveCrushedCounters("player-state-field-copy.post");
            });
        }

        private static bool RangesOverlap(ulong address, int size, ulong expectedAddress, int expectedSize)
        {
            if (size <= 0 || expectedSize <= 0) return false;
            ulong end = address + (ulong)size;
            ulong expectedEnd = expectedAddress + (ulong)expectedSize;
            return end >= address && expectedEnd >= expectedAddress && address < expectedEnd && expectedAddress < end;
        }

        private static ulong ResolveChoreBufferAddress(byte* manager, int cursor, int size, int bufferMode)
        {
            if (manager == null || cursor < 0 || size <= 0 ||
                (long)cursor + size > ChoreBufferCapacity) return 0;
            // The complete player-record handler is proven to use only the linear mode.
            // Avoid interpreting the unrelated banked layout without a separate capacity contract.
            return bufferMode == 0 ? (ulong)(manager + ChoreLinearBufferOffset + cursor) : 0;
        }

        private int[] CaptureTimersFromIntersectingPayload(ulong payloadAddress, int payloadSize)
        {
            if (payloadAddress == 0 || payloadSize < PlayerRuntimeStateStride) return null;
            int records = Math.Min(SerializedPlayerRecordCount, payloadSize / PlayerRuntimeStateStride);
            var result = new int[records];
            for (int index = 0; index < records; index++)
                result[index] = *(int*)(payloadAddress + (ulong)(index * PlayerRuntimeStateStride + SerializedCrushedCounterOffset));
            return result;
        }

        private static string FormatTimerArray(int[] values) => values == null ? "unavailable" :
            string.Join(",", values.Select((value, index) => index + "=" + value));

        private int[] CaptureSerializedCrushedCounters(int baseRva, int stride)
        {
            if (nativeModuleBase == 0) throw new InvalidOperationException("native module base unavailable");
            var result = new int[SerializedPlayerRecordCount];
            byte* baseAddress = (byte*)(nativeModuleBase + (ulong)baseRva);
            for (int playerIndex = 0; playerIndex < result.Length; playerIndex++)
                result[playerIndex] = *(int*)(baseAddress + playerIndex * stride + SerializedCrushedCounterOffset);
            return result;
        }

        private void EmitLegacyCopyBuildingCorrelation()
        {
            if (lastLegacyCopySource == null || lastLegacyCopyDestination == null) return;
            List<BuildingSnapshot> buildings = CaptureRawBuildings();
            var rows = new List<string>();
            for (int ownerId = 0; ownerId < SerializedPlayerRecordCount; ownerId++)
            {
                string nonLiving = string.Join(",", buildings
                    .Where(building => building.OwnerId == ownerId && !IsLiving(building))
                    .Select(building => $"{building.Id}/{building.GlobalId}/{building.Type}/{building.Alive}"));
                rows.Add($"owner={ownerId}/sourceTimer={lastLegacyCopySource[ownerId]}" +
                    $"/destinationTimer={lastLegacyCopyDestination[ownerId]}/nonLivingOwned=[{nonLiving}]");
            }
            EmitChunked("PREPLACED_CRUSHED_TIMER_BULK_COPY_BUILDING_CORRELATION: ",
                $"mapVersion={lastLegacyCopyMapVersion}; sequence={mapSequence}; records=[{string.Join("; ", rows)}]");
        }

        private void EmitLegacyTimerFixEligibility()
        {
            if (lastLegacyCopySourceBefore == null || lastLegacyCopyDestinationBefore == null ||
                lastLegacyCopySource == null || lastLegacyCopyDestination == null) return;

            List<BuildingSnapshot> current = CaptureRawBuildings();
            var currentByGlobalId = current.Where(value => value.GlobalId != 0)
                .GroupBy(value => value.GlobalId).ToDictionary(group => group.Key, group => group.First());
            var currentByGameId = current.GroupBy(value => value.Id)
                .ToDictionary(group => group.Key, group => group.First());
            IEnumerable<BuildingSnapshot> earlyBuildings = legacyCopyBuildings.Concat(
                crushedActivationBuildings.Values.SelectMany(value => value));
            string ownerTransitions = string.Join("; ", earlyBuildings
                .Where(value => IsDestroyedTower(value.Type))
                .GroupBy(value => value.GlobalId != 0 ? "global=" + value.GlobalId :
                    "game=" + value.Id + "/type=" + value.Type).Select(group => group.First())
                .Select(value => (value.GlobalId != 0 && currentByGlobalId.TryGetValue(value.GlobalId, out BuildingSnapshot byGlobal)) ||
                    currentByGameId.TryGetValue(value.Id, out byGlobal)
                    ? $"game={value.Id}/global={value.GlobalId}/type={value.Type}/owner={value.OwnerId}->{byGlobal.OwnerId}"
                    : $"game={value.Id}/global={value.GlobalId}/type={value.Type}/owner={value.OwnerId}->missing"));
            var rows = new List<string>();
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                string classification = LegacyTimerFixEligibility.Classify(IsAi(playerId), currentMapIsSave,
                    lastLegacyCopyMapVersion, LegacyPlayerStateCopyVersionExclusive,
                    lastLegacyCopySourceBefore[playerId], lastLegacyCopySource[playerId],
                    lastLegacyCopyDestinationBefore[playerId], lastLegacyCopyDestination[playerId]);
                rows.Add($"player={playerId}/isAi={IsAi(playerId)}/source={lastLegacyCopySourceBefore[playerId]}->{lastLegacyCopySource[playerId]}" +
                    $"/destination={lastLegacyCopyDestinationBefore[playerId]}->{lastLegacyCopyDestination[playerId]}" +
                    $"/decision={classification}/wouldNormalize={LegacyTimerFixEligibility.IsEligible(classification)}");
            }
            EmitChunked("PREPLACED_LEGACY_TIMER_FIX_ELIGIBILITY: ",
                $"sequence={mapSequence}; passive=true; mapVersion={lastLegacyCopyMapVersion}; mapIsSave={currentMapIsSave}; " +
                $"destroyedTowerOwnerTransitions=[{ownerTransitions}]; records=[{string.Join("; ", rows)}]");
        }

        private static string FormatCapturedInt(int value) =>
            value == int.MinValue ? "capture-failed" : value.ToString();

        private void RunInitializationCheckpoint(string step, int playerId, int buildingId, Action original)
        {
            if (!initializationTracingActive)
            {
                original();
                return;
            }
            int[] before = null;
            string building = buildingId > 0 ? "unresolved" : "none";
            Safe(() =>
            {
                before = CaptureAllCrushedCounters();
                EmitInitializationCheckpointReached(step, "entry");
                if (buildingId > 0 && TryCaptureBuilding(buildingId, out BuildingSnapshot snapshot))
                    building = snapshot.ToText(TryGetArea(snapshot.OwnerId, snapshot));
            });
            original();
            Safe(() =>
            {
                if (before == null) return;
                int[] after = CaptureAllCrushedCounters();
                EmitTimerCheckpointChanges(step, playerId, buildingId, before, after, building);
                EmitInitializationCheckpointReached(step, "post");
                ObserveCrushedCounters("init-checkpoint." + step + ".post");
            });
        }

        private void EmitInitializationCheckpointReached(string step, string stage)
        {
            string key = step + "/" + stage;
            if (!emittedInitializationCheckpoints.Add(key)) return;
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_INIT_CHECKPOINT_REACHED: sequence={mapSequence}; step={step}; stage={stage}; " +
                $"timers=[{FormatTimerArray(CaptureAllCrushedCounters())}]; phase={lastObservedPhase}.");
        }

        private void EmitTimerCheckpointChanges(string step, int playerId, int buildingId,
            int[] before, int[] after, string building = "none")
        {
            if (before == null || after == null) return;
            for (int ownerId = 1; ownerId <= MaxPlayablePlayerId; ownerId++)
            {
                if (before[ownerId] == after[ownerId]) continue;
                Shared.DebugLogHelper.LogWarning(log,
                    $"PREPLACED_INIT_CHECKPOINT_TIMER_CHANGE: step={step}; argumentPlayer={playerId}; " +
                    $"buildingId={buildingId}; building={building}; timerOwner={ownerId}; " +
                    $"crushed={before[ownerId]}->{after[ownerId]}; phase={lastObservedPhase}.");
            }
        }

        private int[] CaptureAllCrushedCounters()
        {
            var result = new int[MaxPlayablePlayerId + 1];
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
                result[playerId] = ReadPlayerGlobal(playerId, CrushedCounterRelativeOffset);
            return result;
        }

        private void ObserveCrushedTimerWriter(NativePointer<X64SmartCPUContext> context)
        {
            // This callback runs after the exact 15 displaced bytes. It only snapshots
            // primitive native data and never calls a Script Extender API.
            X64SmartCPUContext* registers = context.Pointer;
            if (registers == null) return;
            byte* record = (byte*)(registers->R13 + registers->RBP);
            if (record == null) return;
            ulong* stack = (ulong*)registers->RSP;
            var signal = new CrushedWriterSignal(
                DateTime.UtcNow,
                mapSequence,
                lastObservedPhase,
                unchecked((int)(uint)registers->R15),
                unchecked((int)(uint)registers->R12),
                *(short*)(record + 0x12C),
                *(short*)(record + 0x12E),
                *(short*)(record + 0x132),
                *(uint*)(record + 0x134),
                *(short*)(record + 0x168),
                *(short*)(record + 0x16A),
                *(short*)(record + 0x14A),
                *(short*)(record + 0x14C),
                *(short*)(record + 0x15A),
                *(short*)(record + 0x15C),
                unchecked((int)*(uint*)((byte*)stack + 0xC0)),
                unchecked((int)*(uint*)((byte*)stack + 0xC8)),
                unchecked((int)*(uint*)((byte*)stack + 0xD0)),
                unchecked((int)*(uint*)((byte*)stack + 0xD8)),
                unchecked((int)*(uint*)((byte*)stack + 0xE0)));
            lock (crushedWriterSync) pendingCrushedWriterSignals.Enqueue(signal);
        }

        private void DrainCrushedWriterSignals()
        {
            while (true)
            {
                CrushedWriterSignal signal;
                lock (crushedWriterSync)
                {
                    if (pendingCrushedWriterSignals.Count == 0) return;
                    signal = pendingCrushedWriterSignals.Dequeue();
                }
                if (signal.MapSequence != mapSequence) continue;
                bool baselineKnown = preplacedBuildings.TryGetValue(signal.BuildingId, out PreplacedIdentity identity);
                bool baselineMatch = baselineKnown && identity.Matches(signal.BuildingId, signal.GlobalId,
                    signal.BuildingOwnerId, signal.StructureType);
                string text = $"utc={signal.Utc:O}; phase={signal.Phase}; timerOwner={signal.TimerOwnerId}; " +
                    $"buildingId={signal.BuildingId}; global={signal.GlobalId}; buildingOwner={signal.BuildingOwnerId}; " +
                    $"type={(eStructs)signal.StructureType}; alive={(AliveState)signal.AliveStateValue}({signal.AliveStateValue}); healthAfterDamage={signal.HealthAfterDamage}/{signal.MaxHealth}; " +
                    $"tile=({signal.TileX},{signal.TileY})-({signal.EndX},{signal.EndY}); damage={signal.Damage}; " +
                    $"unknown1={signal.Unknown1}; sourcePlayer={signal.SourcePlayerId}; activationMode={signal.ActivationMode}; " +
                    $"unknown4={signal.Unknown4}; timer=0->1; baselineKnown={baselineKnown}; baselineMatch={baselineMatch}";
                Shared.DebugLogHelper.LogWarning(log, "PREPLACED_CRUSHED_TIMER_NATIVE_WRITE: " + text);
                if (IsValidOwner(signal.TimerOwnerId)) RecordOwnerEvent(signal.TimerOwnerId,
                    "crushed-native-writer " + text);
                else RecordUnattributed("crushed-native-writer " + text);
            }
        }

        private int AllocateSpec(ulong state, int playerId)
        {
            lastAivState = state;
            Safe(() => ObservePhase("allocate-spec.pre", true));
            int result = allocateHook.Original(state, playerId);
            Safe(() =>
            {
                ObservePhase("allocate-spec.post", true);
                if (TryGetAiSession(playerId, "allocate-spec", out PlayerSession session))
                {
                    session.Counters.Add("phase.allocate-spec result=" + result);
                    Immediate(playerId, $"AIV_SPEC_ALLOCATED: spec={result}");
                }
            });
            return result;
        }

        private void SetPlacement(ulong state, int spec, int keepX, int keepY, int orientation)
        {
            lastAivState = state;
            Safe(() => ObservePhase("set-placement.pre", true));
            setPlacementHook.Original(state, spec, keepX, keepY, orientation);
            Safe(() =>
            {
                ObservePhase("set-placement.post", true);
                int playerId = ReadSpec(state, spec, PlayerIdOffset);
                if (!TryGetAiSession(playerId, "set-placement", out PlayerSession session)) return;
                session.Counters.Add("phase.set-placement");
                SetAivArea(session, ReadSpec(state, spec, OriginXOffset), ReadSpec(state, spec, OriginYOffset));
                Immediate(playerId, $"AIV_PLACEMENT_SET: spec={spec}, keep=({keepX},{keepY}), orientation={orientation}, {DescribeSpec(state, spec)}");
            });
        }

        private void SelectBestFit(ulong state, int spec, byte rotations)
        {
            MarkPhase("select-best-fit.pre");
            int old = activeSelectionPlayerId;
            Safe(() =>
            {
                activeSelectionPlayerId = SafePlayerFromSpec(state, spec);
                if (TryGetAiSession(activeSelectionPlayerId, "select-best-fit", out PlayerSession session))
                    session.Counters.Add("phase.select-best-fit");
            });
            try { selectHook.Original(state, spec, rotations); }
            finally
            {
                MarkPhase("select-best-fit.post");
                if (IsAi(activeSelectionPlayerId))
                    Safe(() => Immediate(activeSelectionPlayerId, $"AIV_SELECTION_COMPLETE: rotations={rotations}, {DescribeSpec(state, spec)}"));
                activeSelectionPlayerId = old;
            }
        }

        private uint TestSpecificCandidate(ulong state, int spec, int candidate)
        {
            MarkPhase("test-specific-candidate.pre");
            int playerId = 0;
            Safe(() =>
            {
                playerId = SafePlayerFromSpec(state, spec);
                if (TryGetAiSession(playerId, "test-specific-candidate", out PlayerSession session))
                    session.Counters.Add("phase.test-specific-candidate candidate=" + candidate);
            });
            uint result = specificHook.Original(state, spec, candidate);
            MarkPhase("test-specific-candidate.post");
            if (IsAi(playerId))
                Safe(() => Immediate(playerId, $"AIV_SPECIFIC_RESULT: candidate={candidate}, result={result}, {DescribeSpec(state, spec)}"));
            return result;
        }

        private void LoadCandidate(ulong state, int zeroBasedPlayerId, int candidate)
        {
            MarkPhase("load-candidate.pre");
            loadHook.Original(state, zeroBasedPlayerId, candidate);
            MarkPhase("load-candidate.post");
            Safe(() =>
            {
                int playerId = checked(zeroBasedPlayerId + 1);
                if (TryGetAiSession(playerId, "load-candidate", out PlayerSession session))
                    session.Counters.Add("phase.load-candidate candidate=" + candidate);
            });
        }

        private void ApplyRotation(ulong state, int orientation)
        {
            MarkPhase("apply-rotation.pre");
            rotationHook.Original(state, orientation);
            MarkPhase("apply-rotation.post");
            Safe(() =>
            {
                if (IsAi(activeSelectionPlayerId)) Session(activeSelectionPlayerId).Counters.Add("phase.apply-rotation orientation=" + orientation);
                else RecordUnattributed("phase.apply-rotation orientation=" + orientation);
            });
        }

        private int EvaluateCandidateFit(ulong state, int spec)
        {
            MarkPhase("evaluate-fit.pre");
            int result = fitHook.Original(state, spec);
            MarkPhase("evaluate-fit.post");
            Safe(() =>
            {
                int playerId = SafePlayerFromSpec(state, spec);
                if (TryGetAiSession(playerId, "evaluate-fit", out PlayerSession session))
                    session.Counters.Add("candidate-fit result=" + result + " candidate=" + ReadSpec(state, spec, CandidateIdOffset));
            });
            return result;
        }

        private void PrepareLayout(ulong state, int spec, int playerId)
        {
            lastAivState = state;
            Safe(() => ObservePhase("prepare-layout.pre", true));
            PlayerSession session = null;
            List<BuildingSnapshot> before = null;
            Safe(() =>
            {
                if (!TryGetAiSession(playerId, "prepare-layout", out session)) return;
                SetAivArea(session, ReadSpec(state, spec, OriginXOffset), ReadSpec(state, spec, OriginYOffset));
                before = CaptureAndEmitInventory(session, "PREPARE_BEFORE");
                session.Counters.Add("phase.prepare-layout");
            });
            prepareHook.Original(state, spec, playerId);
            Safe(() =>
            {
                ObservePhase("prepare-layout.post", true);
                if (session == null || before == null) return;
                int originX = ReadSpec(state, spec, OriginXOffset), originY = ReadSpec(state, spec, OriginYOffset);
                SetAivArea(session, originX, originY);
                EmitBuildingInventory(playerId, "PREPARE_TRANSLATED", before, session.IsInsideAivArea);
                Immediate(playerId, "AIV_LAYOUT_PREPARED: " + DescribeSpec(state, spec));
            });
        }

        private void Scheduler(ulong state, int playerId)
        {
            initializationTracingActive = false;
            MarkPhase("scheduler.pre");
            Safe(() => ObserveCrushedCounters("scheduler.pre"));
            lastAivState = state;
            PlayerSession session = null;
            SchedulerGateState before = default;
            int executeBefore = 0;
            int alternateBefore = 0;
            Safe(() =>
            {
                if (!TryGetAiSession(playerId, "scheduler", out session)) return;
                before = ReadSchedulerState(state, playerId);
                if (!session.HasAivArea && before.ActiveAivSlot > 0 && before.ActiveAivSlot <= MaxAivSpecIndex)
                    SetAivArea(session, ReadSpec(state, before.ActiveAivSlot, OriginXOffset), ReadSpec(state, before.ActiveAivSlot, OriginYOffset));
                if (!session.FirstSchedulerSnapshotEmitted)
                {
                    session.FirstSchedulerSnapshotEmitted = true;
                    CaptureAndEmitInventory(session, "FIRST_SCHEDULER");
                }
                if (before.CrushedCounter != 0 && !session.FirstActiveDelaySnapshotEmitted)
                {
                    session.FirstActiveDelaySnapshotEmitted = true;
                    CaptureAndEmitInventory(session, "FIRST_ACTIVE_CRUSHED_DELAY");
                }
                executeBefore = session.NestedExecuteCalls;
                alternateBefore = session.AlternativeCalls;
                session.Counters.Add("scheduler.call");
                session.Counters.Add("scheduler.pre=" + SchedulerGateClassifier.ClassifyBeforeCall(before));
            });
            schedulerHook.Original(state, playerId);
            Safe(() =>
            {
                if (session == null)
                    return;
                SchedulerGateState after = ReadSchedulerState(state, playerId);
                string reached = session.NestedExecuteCalls != executeBefore ? "execute-build-step" :
                    session.AlternativeCalls != alternateBefore ? "alternative-execution" : "no-aiv-execution";
                session.Counters.Add("scheduler.reached=" + reached);
                session.Counters.Add("economy.phase=" + ReadPlayerGlobal(playerId, EconomyPhaseRelativeOffset));
                if (before.CrushedDelay < 0)
                    session.Counters.Add("aic.crushed-delay-unresolved slot=" + ReadPlayerGlobal(playerId, ActiveAicRelativeOffset));
                if (before.CrushedCounter != after.CrushedCounter)
                    Immediate(playerId, $"CRUSHED_TIMER_CHANGE: {before.CrushedCounter}->{after.CrushedCounter}, configured={before.CrushedDelay}");
                FlushIfDue(playerId, state, after);
            });
            MarkPhase("scheduler.post");
        }

        private int ExecuteBuildStep(ulong state, int playerId, int frame, int restrictedMode, byte freeOrForced)
        {
            MarkPhase("execute-build-step.pre");
            PlayerSession session = null;
            FrameSnapshot before = default;
            ExecuteContext current = null;
            Safe(() =>
            {
                if (!TryGetAiSession(playerId, "execute-build-step", out session)) return;
                session.NestedExecuteCalls++;
                before = ReadFrame(state, playerId, frame);
                current = new ExecuteContext(playerId, frame, before.Status, CaptureOwnedIdentities(playerId));
            });
            ExecuteContext previous = activeExecute;
            if (current != null)
            {
                activeExecute = current;
            }
            int result;
            try { result = executeHook.Original(state, playerId, frame, restrictedMode, freeOrForced); }
            finally
            {
                activeExecute = previous;
            }
            Safe(() =>
            {
                if (session == null || current == null) return;
                FrameSnapshot after = ReadFrame(state, playerId, frame);
                CorrelateExecuteSpawns(current, playerId);
                string reason = ClassifyExecuteResult(result, current, before, after);
                session.Counters.Add($"execute.result={result} reason={reason} mapper={before.Mapper}");
                session.Counters.Add($"execute.frame={frame} status={before.Status}->{after.Status} mode={restrictedMode}/{freeOrForced}");
                bool confirmed = session.FirstBuilding.TryConfirm(DateTime.UtcNow, result != 0,
                    current.SpawnedBuildingIds.Count != 0, before.Status != after.Status);
                if (confirmed)
                    Immediate(playerId, $"FIRST_AIV_BUILDING_CONFIRMED: frame={frame}, mapper={before.Mapper}, pos={before.Position}, result={result}, spawned=[{string.Join(",", current.SpawnedBuildingIds)}], signals=[{string.Join(",", current.SpawnSignals)}], status={before.Status}->{after.Status}; followUpSeconds=10");
            });
            MarkPhase("execute-build-step.post");
            return result;
        }

        private long AlternativeExecution(ulong state, int playerId, int pausedMode)
        {
            MarkPhase("alternative-execution.pre");
            PlayerSession session = null;
            Safe(() =>
            {
                if (!TryGetAiSession(playerId, "alternative-execution", out session)) return;
                session.AlternativeCalls++;
                session.Counters.Add("phase.alternative-execution mode=" + pausedMode);
            });
            long result = alternativeHook.Original(state, playerId, pausedMode);
            MarkPhase("alternative-execution.post");
            return result;
        }

        private long PlacementHelper(ulong manager, int playerId, int x, int y, int mapperValue, int orientation)
        {
            MarkPhase("placement-helper.pre");
            long result = placementHook.Original(manager, playerId, x, y, mapperValue, orientation);
            MarkPhase("placement-helper.post");
            Safe(() =>
            {
                if (IsAi(playerId)) Session(playerId).Counters.Add($"placement-helper result={result} mapper={(eMappers)mapperValue} pos=({x},{y}) orientation={orientation}");
                else RecordUnattributed($"placement-helper player={playerId} result={result} mapper={(eMappers)mapperValue} orientation={orientation}");
                if (activeExecute != null && activeExecute.PlayerId == playerId) activeExecute.PlacementResults.Add(result);
            });
            return result;
        }

        private int Validator(ulong state, int tileId, int playerId, int mapperValue, int mode)
        {
            MarkPhase("validator.pre");
            int result = validatorHook.Original(state, tileId, playerId, mapperValue, mode);
            MarkPhase("validator.post");
            Safe(() =>
            {
                string outcome = PlacementValidatorResult.Classify(result);
                if (IsAi(playerId))
                    Session(playerId).Counters.Add($"native-validator outcome={outcome} result={result} mapper={(eMappers)mapperValue} tileId={tileId} mode={mode}");
                else
                    RecordUnattributed($"native-validator player={playerId} outcome={outcome} result={result} mapper={(eMappers)mapperValue} mode={mode}");
                if (activeExecute != null && activeExecute.PlayerId == playerId) activeExecute.ValidatorResults.Add(result);
            });
            return result;
        }

        private int ResourceGate(ulong manager, int mapperValue, int playerId, int mode)
        {
            MarkPhase("resource-gate.pre");
            int result = resourceGateHook.Original(manager, mapperValue, playerId, mode);
            MarkPhase("resource-gate.post");
            Safe(() =>
            {
                if (IsAi(playerId)) Session(playerId).Counters.Add($"resource-gate result={result} mapper={(eMappers)mapperValue} mode={mode}");
                else RecordUnattributed($"resource-gate player={playerId} result={result} mapper={(eMappers)mapperValue} mode={mode}");
                if (activeExecute != null && activeExecute.PlayerId == playerId) activeExecute.ResourceResults.Add(result);
                CurrentEconomy(playerId)?.ResourceResults.Add($"{(eMappers)mapperValue}/{mode}={result}");
            });
            return result;
        }

        private int MapperWaitOne(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitOneHook, "mapper-wait-1", manager, playerId, mapperValue);
        private int MapperWaitTwo(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitTwoHook, "mapper-wait-2", manager, playerId, mapperValue);
        private int MapperWaitThree(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitThreeHook, "mapper-wait-3", manager, playerId, mapperValue);
        private int MapperWaitFour(ulong manager, int playerId, int mapperValue) => RecordMapperGate(mapperWaitFourHook, "mapper-wait-4", manager, playerId, mapperValue);

        private int RecordMapperGate(DetourHandle<MapperGateDelegate> hook, string name, ulong manager, int playerId, int mapperValue)
        {
            MarkPhase(name + ".pre");
            int result = hook.Original(manager, playerId, mapperValue);
            MarkPhase(name + ".post");
            Safe(() =>
            {
                if (IsAi(playerId)) Session(playerId).Counters.Add($"{name} result={result} mapper={(eMappers)mapperValue}");
                else RecordUnattributed($"{name} player={playerId} result={result} mapper={(eMappers)mapperValue}");
                if (activeExecute != null && activeExecute.PlayerId == playerId && result != 0) activeExecute.WaitRejectors.Add(name);
            });
            return result;
        }

        private int DeleteHovel(ulong manager, int playerId)
        {
            MarkPhase("delete-hovel.pre");
            int result = deleteHovelHook.Original(manager, playerId);
            MarkPhase("delete-hovel.post");
            Safe(() =>
            {
                if (!TryGetAiSession(playerId, "delete-hovel", out PlayerSession session)) return;
                session.Counters.Add("hovel-delete result=" + result);
                if (result != 0) Immediate(playerId, "HOVEL_DELETE_SUCCEEDED");
            });
            return result;
        }

        private void MaintenanceOne(ulong state, int playerId)
        {
            MarkPhase("maintenance-1.pre");
            maintenanceOneHook.Original(state, playerId);
            MarkPhase("maintenance-1.post");
            Safe(() =>
            {
                if (TryGetAiSession(playerId, "maintenance-1", out PlayerSession session))
                    session.Counters.Add("maintenance.0x50340");
            });
        }

        private void MaintenanceTwo(ulong state, int playerId)
        {
            MarkPhase("maintenance-2.pre");
            maintenanceTwoHook.Original(state, playerId);
            MarkPhase("maintenance-2.post");
            Safe(() =>
            {
                if (TryGetAiSession(playerId, "maintenance-2", out PlayerSession session))
                    session.Counters.Add("maintenance.0x504F0");
            });
        }

        private int CountBuildings(ulong manager, int playerId, int structureType, int mode)
        {
            MarkPhase("count-buildings.pre");
            int result = countBuildingsHook.Original(manager, playerId, structureType, mode);
            MarkPhase("count-buildings.post");
            Safe(() =>
            {
                int preplaced = CountMatchingPreplaced(playerId, (eStructs)structureType, mode);
                int hypothetical = PreplacedCountProjection.WithoutPreplaced(result, preplaced);
                string key = $"global-count type={(eStructs)structureType} mode={mode} vanilla={result} preplacedFraction={preplaced}/{result} hypothetical={hypothetical}";
                if (IsAi(playerId)) Session(playerId).Counters.Add(key);
                else RecordUnattributed($"{key} player={playerId}");
                CurrentEconomy(playerId)?.CountResults.Add($"{(eStructs)structureType}/{mode}={result}");
            });
            return result;
        }

        private int PlacementReachability(ulong manager, int playerId, int structureType, int x, int y)
        {
            MarkPhase("placement-reachability.pre");
            int result = placementReachabilityHook.Original(manager, playerId, structureType, x, y);
            MarkPhase("placement-reachability.post");
            Safe(() =>
            {
                ReachabilitySnapshot diagnostic = CaptureReachability(playerId, x, y);
                string area = TryIsPointInsideAiv(playerId, x, y, out bool inside) ? (inside ? "inside" : "outside") : "pending";
                string key = $"placement-reachability result={result} type={(eStructs)structureType} pos=({x},{y}) area={area} keepPcl={diagnostic.KeepPcl} targetPcl={diagnostic.TargetPcl} route={diagnostic.Route.Kind}";
                if (IsAi(playerId))
                {
                    Session(playerId).Counters.Add(key);
                    if (result == 0) EmitPortalTopology(playerId, "PLACEMENT_REACHABILITY_REJECTED", diagnostic);
                }
                else RecordUnattributed($"{key} player={playerId}");
                if (activeExecute != null && activeExecute.PlayerId == playerId)
                    activeExecute.ReachabilityResults.Add(result);
                CurrentEconomy(playerId)?.ReachabilityResults.Add($"{(eStructs)structureType}@{x},{y}={result}/{diagnostic.Route.Kind}");
            });
            return result;
        }

        private void AccessibilitySweep(ulong manager, int playerId)
        {
            MarkPhase("accessibility-sweep.pre");
            int previous = activeAccessibilityPlayerId;
            List<AccessibilityCallSnapshot> previousCalls = activeAccessibilityCalls;
            activeAccessibilityPlayerId = playerId;
            List<AccessibilityCallSnapshot> calls = new List<AccessibilityCallSnapshot>();
            activeAccessibilityCalls = calls;
            try { accessibilitySweepHook.Original(manager, playerId); }
            finally { activeAccessibilityPlayerId = previous; activeAccessibilityCalls = previousCalls; }
            Safe(() =>
            {
                PlayerSession session = IsAi(playerId) ? Session(playerId) : null;
                if (session != null)
                {
                    session.Counters.Add("accessibility-sweep.call");
                    foreach (AccessibilityCallSnapshot call in calls)
                    {
                        string after = TryCaptureBuilding(call.BuildingId, out BuildingSnapshot building)
                            ? building.Alive + "/sleep=" + building.Sleeping
                            : "removed";
                        if (!string.Equals(call.BeforeState, after, StringComparison.Ordinal))
                            session.Counters.Add($"accessibility-sweep.change building={call.BuildingId} {call.BeforeState}->{after} classifier={call.Result}");
                    }
                }
                else RecordUnattributed("accessibility-sweep player=" + playerId);
            });
            MarkPhase("accessibility-sweep.post");
        }

        private int BuildingAccessibility(ulong manager, int buildingId, int mode)
        {
            MarkPhase("building-accessibility.pre");
            BuildingSnapshot? before = null;
            Safe(() =>
            {
                if (activeAccessibilityPlayerId != 0 &&
                    TryCaptureBuilding(buildingId, out BuildingSnapshot snapshot)) before = snapshot;
            });
            int result = buildingAccessibilityHook.Original(manager, buildingId, mode);
            Safe(() =>
            {
                if (activeAccessibilityPlayerId == 0) return;
                int playerId = activeAccessibilityPlayerId;
                string identity = before.HasValue ? before.Value.ToText(TryGetArea(before.Value.OwnerId, before.Value)) : "building=unresolved";
                string key = $"building-accessibility result={result} class={BuildingAccessibilityResult.Classify(result)} mode={mode} preplaced={IsCurrentPreplaced(buildingId)} {identity}";
                activeAccessibilityCalls?.Add(new AccessibilityCallSnapshot(buildingId,
                    before.HasValue ? before.Value.Alive + "/sleep=" + before.Value.Sleeping : "unresolved", result));
                if (IsAi(playerId))
                {
                    Session(playerId).Counters.Add(key);
                    if (BuildingAccessibilityResult.IsRejected(result))
                    {
                        ReachabilitySnapshot diagnostic = before.HasValue
                            ? CaptureReachability(playerId, before.Value.EndX, before.Value.EndY)
                            : ReachabilitySnapshot.Unavailable;
                        EmitPortalTopology(playerId, "BUILDING_ACCESSIBILITY_REJECTED", diagnostic);
                    }
                }
                else RecordUnattributed(key + " contextPlayer=" + playerId);
            });
            MarkPhase("building-accessibility.post");
            return result;
        }

        private long EconomyFarm(ulong state, int playerId, int desiredStructureType)
        {
            EconomyContext context = BeginEconomy(state, playerId, "farm", (eStructs)desiredStructureType);
            long result;
            try { result = economyFarmHook.Original(state, playerId, desiredStructureType); }
            finally { EndEconomy(context); }
            CompleteEconomy(context, "return=" + result);
            return result;
        }

        private void EconomyIron(ulong state, int playerId) => RunEconomyPlayer(
            economyIronHook, state, playerId, "iron", eStructs.STRUCT_IRON_MINE);

        private void EconomyOxen(ulong state, int playerId) => RunEconomyPlayer(
            economyOxenHook, state, playerId, "oxen", eStructs.STRUCT_OXEN_BASE);

        private void EconomyPitch(ulong state, int playerId) => RunEconomyPlayer(
            economyPitchHook, state, playerId, "pitch", eStructs.STRUCT_PITCH_DIGGER);

        private void EconomyQuarry(ulong state, int playerId) => RunEconomyPlayer(
            economyQuarryHook, state, playerId, "quarry", eStructs.STRUCT_QUARRY);

        private void EconomyWood(ulong state, int playerId) => RunEconomyPlayer(
            economyWoodHook, state, playerId, "wood", eStructs.STRUCT_WOODCUTTERS_HUT);

        private void RunEconomyPlayer(DetourHandle<EconomyPlayerDelegate> hook, ulong state, int playerId,
            string phase, eStructs desiredType)
        {
            EconomyContext context = BeginEconomy(state, playerId, phase, desiredType);
            try { hook.Original(state, playerId); }
            finally { EndEconomy(context); }
            CompleteEconomy(context, "void-return");
        }

        private EconomyContext BeginEconomy(ulong state, int playerId, string phase, eStructs desiredType)
        {
            MarkPhase("economy-" + phase + ".pre");
            bool afterConfirmedBreach = players.TryGetValue(playerId, out PlayerSession existingSession) &&
                existingSession.ConfirmedWallBreach;
            var context = new EconomyContext(playerId, phase, desiredType, state, afterConfirmedBreach);
            EconomyContexts.Push(context);
            Safe(() =>
            {
                if (!TryGetAiSession(playerId, "economy-" + phase, out PlayerSession session)) return;
                session.Counters.Add($"economy-dispatch phase={phase} desired={desiredType}");
                if (!session.FirstEconomyRoutingSnapshotEmitted)
                {
                    session.FirstEconomyRoutingSnapshotEmitted = true;
                    // Map-start already emitted the lossless baseline. A reference or delta is sufficient here.
                    CaptureRoutingSnapshot(state, "FIRST_ECONOMY_SEARCH_PLAYER_" + playerId, false);
                }
            });
            return context;
        }

        private void EndEconomy(EconomyContext context)
        {
            if (EconomyContexts.Count == 0 || !ReferenceEquals(EconomyContexts.Peek(), context))
            {
                Safe(() => RecordUnattributed("economy-context-stack-mismatch phase=" + context.Phase));
                return;
            }
            EconomyContexts.Pop();
            MarkPhase("economy-" + context.Phase + ".post");
        }

        private void CompleteEconomy(EconomyContext context, string returnText)
        {
            Safe(() =>
            {
                string outcome = EconomySearchOutcome.Classify(context.Searches.Count,
                    context.Searches.Any(search => search.CandidateFound), context.ConstructionCalls.Count);
                string detail = $"phase={context.Phase} desired={context.DesiredType} afterConfirmedBreach={context.AfterConfirmedBreach} outcome={outcome} {returnText}; " +
                    $"counts=[{string.Join(",", context.CountResults)}]; resources=[{string.Join(",", context.ResourceResults)}]; " +
                    $"searches=[{string.Join(",", context.Searches.Select(s => s.CompactText))}]; " +
                    $"reachability=[{string.Join(",", context.ReachabilityResults)}]; pclCalls=[{string.Join(",", context.PclCalls)}]; " +
                    $"constructs=[{string.Join(",", context.ConstructionCalls)}]; spawns=[{string.Join(",", context.SpawnSignals)}]";
                if (IsAi(context.PlayerId))
                {
                    // Nested hooks already aggregate their complete observations. Keep the enclosing result key stable.
                    Session(context.PlayerId).Counters.Add($"economy-result phase={context.Phase} desired={context.DesiredType} " +
                        $"afterConfirmedBreach={context.AfterConfirmedBreach} outcome={outcome} return={returnText}");
                }
                else RecordUnattributed("economy-result player=" + context.PlayerId + " " + detail);
            });
        }

        private long FarmSearch(ulong state, int playerId, int desiredStructureType)
        {
            EconomyGridState before = CaptureEconomyGridState(state);
            short cooldownBefore = ReadPlayerInt16(playerId, FarmSearchCooldownRelativeOffset, 0);
            string entryGate = DescribeFarmEntryGate(playerId, cooldownBefore);
            long result = farmSearchHook.Original(state, playerId, desiredStructureType);
            short cooldownAfter = ReadPlayerInt16(playerId, FarmSearchCooldownRelativeOffset, 0);
            ObserveEconomySearch(state, playerId, "farm-search", (eStructs)desiredStructureType,
                "desired=" + (eStructs)desiredStructureType + "/return=" + result +
                "/entryGate=" + entryGate + "/cooldown=" + DescribeCooldown(cooldownBefore, cooldownAfter),
                before, result != 0, cooldownBefore, cooldownAfter, 0);
            return result;
        }

        private void ResourceSearch(ulong state, int playerId, int mode)
        {
            EconomyGridState before = CaptureEconomyGridState(state);
            // Literal mode values from RVA 0x57B80 select quarry, iron, and pitch cooldown slots.
            int cooldownOffset = mode == 2 ? QuarrySearchCooldownRelativeOffset :
                mode == 3 ? IronSearchCooldownRelativeOffset : mode == 4 ? PitchSearchCooldownRelativeOffset : -1;
            short cooldownBefore = cooldownOffset < 0 ? (short)-1 : ReadPlayerInt16(playerId, cooldownOffset, 0);
            string entryGate = DescribeResourceEntryGate(playerId, mode, cooldownBefore);
            resourceSearchHook.Original(state, playerId, mode);
            short cooldownAfter = cooldownOffset < 0 ? (short)-1 : ReadPlayerInt16(playerId, cooldownOffset, 0);
            ObserveEconomySearch(state, playerId, "resource-search", CurrentEconomy(playerId)?.DesiredType,
                "mode=" + mode + "/entryGate=" + entryGate + "/cooldown=" + (cooldownOffset < 0 ? "unavailable-invalid-mode" :
                    DescribeCooldown(cooldownBefore, cooldownAfter)), before, null, cooldownBefore, cooldownAfter, mode);
        }

        private void WoodSearch(ulong state, int playerId)
        {
            EconomyGridState before = CaptureEconomyGridState(state);
            short cooldownBefore = ReadPlayerInt16(playerId, WoodSearchCooldownRelativeOffset, 0);
            string entryGate = cooldownBefore > 0 ? "cooldown-active" : "ready";
            woodSearchHook.Original(state, playerId);
            short cooldownAfter = ReadPlayerInt16(playerId, WoodSearchCooldownRelativeOffset, 0);
            ObserveEconomySearch(state, playerId, "wood-search", eStructs.STRUCT_WOODCUTTERS_HUT,
                "player=" + playerId + "/entryGate=" + entryGate + "/cooldown=" + DescribeCooldown(cooldownBefore, cooldownAfter),
                before, null, cooldownBefore, cooldownAfter, 0);
        }

        private void NearbySearch(ulong state, uint coarseX, uint coarseY)
        {
            EconomyContext context = CurrentEconomy(0);
            EconomyGridState before = CaptureEconomyGridState(state);
            nearbySearchHook.Original(state, coarseX, coarseY);
            ObserveEconomySearch(state, context?.PlayerId ?? 0, "nearby-search", context?.DesiredType,
                $"start=({coarseX},{coarseY})", before, null, -1, -1, 0,
                checked((int)coarseX), checked((int)coarseY));
        }

        private void ConstructBuilding(ulong state, int playerId, int x, int y, short mapperValue,
            int orientation, int mode, byte suppressPostProcessing)
        {
            EconomyContext context = CurrentEconomy(playerId);
            Dictionary<int, PreplacedIdentity> before = null;
            if (context != null)
                Safe(() => before = CaptureOwnedIdentities(playerId));
            constructBuildingHook.Original(state, playerId, x, y, mapperValue, orientation, mode, suppressPostProcessing);
            Safe(() =>
            {
                string delta = context == null ? "outside-economy-context" : DescribeIdentityDelta(before, CaptureOwnedIdentities(playerId));
                string call = $"mapper={(eMappers)(ushort)mapperValue} pos=({x},{y}) orientation={orientation} mode={mode} suppressPost={suppressPostProcessing} delta={delta}";
                if (context != null) context.ConstructionCalls.Add(call);
                if (IsAi(playerId)) Session(playerId).Counters.Add("construct-building " + call);
                else RecordUnattributed("construct-building player=" + playerId + " " + call);
            });
        }

        private int RegionPairReachability(ulong pathManager, int playerId, int targetPcl, int sourcePcl, int routeMode)
        {
            int result = regionPairReachabilityHook.Original(pathManager, playerId, targetPcl, sourcePcl, routeMode);
            Safe(() =>
            {
                EconomyContext context = CurrentEconomy(playerId);
                string scope = context == null ? "outside-economy" : "economy-" + context.Phase;
                string key = $"pcl-pair scope={scope} source={sourcePcl} target={targetPcl} mode={routeMode} result={result}";
                if (IsAi(playerId)) Session(playerId).Counters.Add(key);
                else RecordUnattributed(key + " player=" + playerId);
                context?.PclCalls.Add($"{sourcePcl}->{targetPcl}/{routeMode}={result}");
            });
            return result;
        }

        private EconomyContext CurrentEconomy(int playerId)
        {
            foreach (EconomyContext context in EconomyContexts)
                if (playerId == 0 || context.PlayerId == playerId) return context;
            return null;
        }

        private static Stack<EconomyContext> EconomyContexts =>
            activeEconomyContexts ?? (activeEconomyContexts = new Stack<EconomyContext>());

        private static string DescribeCooldown(int before, int after) =>
            before + "->" + after + "/" + EconomyCooldownTransition.Classify(before, after);

        private EconomyGridState CaptureEconomyGridState(ulong state)
        {
            if (state == 0) return EconomyGridState.Unavailable;
            byte* memory = (byte*)state;
            return new EconomyGridState(
                *(int*)(memory + EconomyVisitGenerationOffset),
                *(int*)(memory + EconomyQueueDepthOffset),
                *(int*)(memory + EconomyQueueReadOffset),
                *(int*)(memory + EconomyQueueWriteOffset),
                *(int*)(memory + EconomyResultXOffset),
                *(int*)(memory + EconomyResultYOffset));
        }

        private void ObserveEconomySearch(ulong state, int playerId, string helper, eStructs? desiredType,
            string arguments, EconomyGridState before, bool? candidateFoundOverride = null,
            int cooldownBefore = -1, int cooldownAfter = -1, int resourceMode = 0,
            int explicitStartX = -1, int explicitStartY = -1)
        {
            Safe(() =>
            {
                EconomyGridState after = CaptureEconomyGridState(state);
                EconomySearchObservation observation = AnalyzeEconomySearch(state, helper, arguments, before, after,
                    candidateFoundOverride, cooldownBefore, cooldownAfter);
                EconomyContext context = CurrentEconomy(playerId);
                context?.Searches.Add(observation);
                string counter = $"economy-search helper={helper} desired={(desiredType.HasValue ? desiredType.Value.ToString() : "unknown")} " +
                    $"traversal={observation.PerformedTraversal} visited={observation.Visited.Count} frontier={observation.Frontier.Count} " +
                    $"gate={observation.GateReason} result=({observation.ResultX},{observation.ResultY}) " +
                    $"candidate={observation.CandidateFound} signature={observation.Signature:X16}";
                if (IsAi(playerId))
                {
                    PlayerSession session = Session(playerId);
                    session.Counters.Add(counter);
                    if (observation.PerformedTraversal && !observation.CandidateFound &&
                        observation.Frontier.Count != 0)
                    {
                        EconomyBarrierObservation barrier = CaptureEconomyBarrier(helper, desiredType, observation);
                        session.LastEconomyBarrier = barrier;
                        session.EconomyBarriers.Add(barrier);
                    }
                    string emissionKey = helper + "/" + (desiredType?.ToString() ?? "unknown") + "/" +
                        observation.Signature + "/afterBreach=" + session.ConfirmedWallBreach +
                        "/candidate=" + observation.CandidateFound;
                    if (session.EmittedSearchSignatures.Add(emissionKey))
                    {
                        EmitChunked($"PREPLACED_ECONOMY_SEARCH: player={playerId}; helper={helper}; ",
                            observation.FullText + "; counterfactual=" + DescribeSearchRoutes(playerId, observation));
                    }
                    EmitShadowNativeResultOracle(state, playerId, helper, resourceMode, observation);
                    EmitShadowEconomySearch(state, playerId, helper, resourceMode, explicitStartX, explicitStartY);
                    if (session.WallLossStageEmitted && observation.PerformedTraversal)
                        EmitChunked($"PREPLACED_POST_WALL_LOSS_ECONOMY_SEARCH: player={playerId}; ",
                            $"elapsedMs={(DateTime.UtcNow - session.WallLossUtc).TotalMilliseconds:F0}; helper={helper}; " +
                            $"gate={observation.GateReason}; visited={observation.Visited.Count}; frontier={observation.Frontier.Count}; " +
                            $"result=({observation.ResultX},{observation.ResultY}); candidate={observation.CandidateFound}; " +
                            $"cooldown={cooldownBefore}->{cooldownAfter}; signature={observation.Signature:X16}");
                    if (session.ConfirmedWallBreach && observation.PerformedTraversal)
                    {
                        string postBreachKey = helper + "/" + (desiredType?.ToString() ?? "unknown");
                        session.Counters.Add("post-breach-economy-search " + counter);
                        if (session.EmittedPostBreachSearchKinds.Add(postBreachKey))
                            EmitChunked($"PREPLACED_POST_BREACH_ECONOMY_SEARCH: player={playerId}; ",
                                $"elapsedMs={(DateTime.UtcNow - session.WallBreachUtc).TotalMilliseconds:F0}; helper={helper}; " +
                                $"desired={(desiredType.HasValue ? desiredType.Value.ToString() : "unknown")}; " +
                                observation.FullText + "; counterfactual=" + DescribeSearchRoutes(playerId, observation));
                    }
                }
                else RecordUnattributed(counter + " player=" + playerId);
            });
        }

        private void EmitShadowEconomySearch(ulong state, int playerId, string helper, int resourceMode,
            int explicitStartX, int explicitStartY, string trigger = "vanilla-call")
        {
            if (state == 0 || !IsAi(playerId) || !TryGetKeepPcl(playerId, out int keepPcl)) return;
            ShadowEconomySearchKind kind = helper == "farm-search" ? ShadowEconomySearchKind.Farm :
                helper == "resource-search" ? ShadowEconomySearchKind.Resource :
                helper == "wood-search" ? ShadowEconomySearchKind.Wood : ShadowEconomySearchKind.Nearby;
            int startX;
            int startY;
            if (explicitStartX >= 0 && explicitStartY >= 0)
            { startX = explicitStartX; startY = explicitStartY; }
            else
            {
                if (!TryGetKeepPosition(playerId, out int keepX, out int keepY)) return;
                startX = keepX / EconomyCoarseCellTileSize;
                startY = keepY / EconomyCoarseCellTileSize;
            }
            if ((uint)startX >= EconomyGridWidth || (uint)startY >= EconomyGridWidth) return;
            List<PortalConnection> portals = CapturePortalConnections(out _);
            HashSet<int> reachablePcls = PortalRouteModel.ReachableFriendlyPcls(keepPcl, portals,
                playerId, IsAllied);
            ulong routingSignature = lastRoutingSnapshot?.Signature ?? 0;
            var cells = new ShadowEconomyCell[EconomyGridCellCount];
            byte* memory = (byte*)state;
            for (int index = 0; index < cells.Length; index++)
            {
                EconomyCoordinate coordinate = EconomyCoordinate.FromIndex(index);
                int projected04 = CountPclTilesOutsideSet(coordinate.X, coordinate.Y, reachablePcls);
                byte* cell = memory + EconomyGridBaseOffset + index * EconomyGridCellStride;
                byte rawOwnerClass = cell[0x15];
                cells[index] = new ShadowEconomyCell(projected04, (sbyte)cell[0x16], cell[0x07],
                    cell[0x08], cell[0x09], cell[0x0A], cell[0x0B], cell[0x0C], cell[0x0D],
                    cell[0x0E], cell[0x0F], cell[0x11], cell[0x12], cell[0x13], rawOwnerClass,
                    PlayerClassMatches(playerId, rawOwnerClass));
            }
            ShadowEconomySearchResult result = ShadowEconomySearch.Run(cells, EconomyGridWidth,
                startX * EconomyGridWidth + startY, kind, resourceMode);
            PlayerSession playerSession = Session(playerId);
            string shadowKind = helper + "/" + resourceMode;
            if (!playerSession.ShadowBaselines.ContainsKey(shadowKind))
                playerSession.ShadowBaselines.Add(shadowKind,
                    new ShadowSearchSummary(result.ReachableCount, result.CandidateIndices.Length));
            if (!playerSession.ShadowExpansionStageEmitted &&
                wallBaselines.TryGetValue(playerId, out WallTileBaseline wallBaseline) &&
                wallBaseline.LostWallTiles.Any(wallBaseline.ComponentTiles.Contains))
            {
                ShadowSearchSummary baselineSummary = playerSession.ShadowBaselines[shadowKind];
                if (result.ReachableCount > baselineSummary.Reachable && result.CandidateIndices.Length > baselineSummary.Candidates)
                {
                    playerSession.ShadowExpansionStageEmitted = true;
                    EmitChunked($"PREPLACED_WALL_BREACH_STAGE: player={playerId}; ",
                        $"stage=shadow-external-expansion; helper={helper}; mode={resourceMode}; " +
                        $"reachable={baselineSummary.Reachable}->{result.ReachableCount}; " +
                        $"candidates={baselineSummary.Candidates}->{result.CandidateIndices.Length}");
                }
            }
            ulong semanticSignature = ComputeShadowSemanticSignature(kind, resourceMode, startX, startY,
                cells, result);
            string inputSignature = playerId + "/" + helper + "/" + resourceMode + "/" + semanticSignature.ToString("X16");
            if (!emittedShadowSearchSignatures.Add(inputSignature))
            {
                Session(playerId).Counters.Add("shadow-economy-repeat helper=" + helper +
                    " semanticSignature=" + semanticSignature.ToString("X16"));
                return;
            }
            string firstCandidate = result.FirstCandidateIndex < 0 ? "none" :
                EconomyCoordinate.FromIndex(result.FirstCandidateIndex).ToString();
            string resourceAnalysis = kind == ShadowEconomySearchKind.Resource
                ? DescribeResourceShadowAnalysis(cells, result, resourceMode)
                : "not-resource-search";
            EmitChunked($"PREPLACED_SHADOW_ECONOMY_SEARCH: player={playerId}; helper={helper}; ",
                $"mode={resourceMode}; trigger={trigger}; semanticSignature={semanticSignature:X16}; routingSignature={routingSignature:X16}; " +
                $"start=({startX},{startY}); keepPcl={keepPcl}; " +
                $"friendlyReachablePcls=[{string.Join(",", reachablePcls.OrderBy(value => value))}]; " +
                $"reachableCells={result.ReachableCount}; blockedBoundaryCells={result.BlockedIndices.Length}; " +
                $"candidateCells={result.CandidateIndices.Length}; firstCandidate={firstCandidate}; " +
                $"blocked=[{LosslessGridCoordinateFormatter.Format(result.BlockedIndices, EconomyGridWidth)}]; " +
                $"candidates=[{LosslessGridCoordinateFormatter.Format(result.CandidateIndices, EconomyGridWidth)}]; " +
                $"resourceAnalysis=[{resourceAnalysis}]; byte+16 remains byte-for-byte Vanilla because 0x50720 builds it from tile logic rather than the selected PCL");
        }

        private static ulong ComputeShadowSemanticSignature(ShadowEconomySearchKind kind, int resourceMode,
            int startX, int startY, ShadowEconomyCell[] cells, ShadowEconomySearchResult result)
        {
            ulong signature = 1469598103934665603UL;
            signature = Hash(signature, (int)kind);
            signature = Hash(signature, resourceMode);
            signature = Hash(signature, startX);
            signature = Hash(signature, startY);
            foreach (int index in result.ReachedIndices) signature = Hash(signature, index);
            signature = Hash(signature, -1);
            foreach (int index in result.BlockedIndices) signature = Hash(signature, index);
            signature = Hash(signature, -2);
            foreach (int index in result.CandidateIndices) signature = Hash(signature, index);
            if (kind == ShadowEconomySearchKind.Resource)
                for (int index = 0; index < cells.Length; index++)
                    signature = Hash(signature,
                        ShadowEconomySearch.ResourceCandidateRejectionCode(cells[index], resourceMode));
            return signature;
        }

        private void EmitProactiveShadowSuite(ulong state, string trigger)
        {
            if (!aiOwnershipResolved || state == 0) return;
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                if (!IsAi(playerId)) continue;
                EmitShadowEconomySearch(state, playerId, "farm-search", 0, -1, -1, trigger);
                EmitShadowEconomySearch(state, playerId, "wood-search", 0, -1, -1, trigger);
                EmitShadowEconomySearch(state, playerId, "resource-search", 2, -1, -1, trigger);
                EmitShadowEconomySearch(state, playerId, "resource-search", 3, -1, -1, trigger);
                EmitShadowEconomySearch(state, playerId, "resource-search", 4, -1, -1, trigger);
            }
        }

        private void EmitShadowNativeResultOracle(ulong state, int playerId, string helper, int resourceMode,
            EconomySearchObservation observation)
        {
            if (state == 0 || !observation.PerformedTraversal || !observation.CandidateFound ||
                (uint)observation.ResultX >= EconomyGridWidth || (uint)observation.ResultY >= EconomyGridWidth ||
                !TryGetKeepPcl(playerId, out int keepPcl)) return;
            HashSet<int> reachablePcls = PortalRouteModel.ReachableFriendlyPcls(keepPcl,
                CapturePortalConnections(out _), playerId, IsAllied);
            int index = observation.ResultX * EconomyGridWidth + observation.ResultY;
            byte* raw = (byte*)state + EconomyGridBaseOffset + index * EconomyGridCellStride;
            var cell = new ShadowEconomyCell(CountPclTilesOutsideSet(observation.ResultX, observation.ResultY, reachablePcls),
                (sbyte)raw[0x16], raw[0x07], raw[0x08], raw[0x09], raw[0x0A], raw[0x0B], raw[0x0C], raw[0x0D],
                raw[0x0E], raw[0x0F], raw[0x11], raw[0x12], raw[0x13], raw[0x15], PlayerClassMatches(playerId, raw[0x15]));
            string checks;
            string result;
            if (helper == "resource-search")
            {
                checks = ShadowEconomySearch.DescribeResourceChecks(cell, resourceMode);
                result = ShadowEconomySearch.ResourceCandidateRejectionReason(cell, resourceMode);
            }
            else if (helper == "farm-search")
            {
                result = cell.Projected04 != 0 ? "pcl-not-exact" : cell.Raw0F != 0 ? "occupied-or-reserved-byte+0F" :
                    cell.Raw07 != 0 ? "wood-density-byte+07" : cell.Raw13 != 0 ? "blocked-byte+13" :
                    cell.Raw11 <= 24 ? "farm-density-byte+11" : cell.Raw12 <= 13 ? "farm-density-byte+12" : "candidate";
                checks = $"projected04={cell.Projected04}/raw07={cell.Raw07}/raw0F={cell.Raw0F}/raw11={cell.Raw11}" +
                    $"/raw12={cell.Raw12}/raw13={cell.Raw13}/firstRejection={result}";
            }
            else return;
            string key = helper + "/" + resourceMode + "/" + observation.ResultX + "/" + observation.ResultY + "/" + result;
            if (!Session(playerId).EmittedOracleSignatures.Add(key)) return;
            string label = result == "candidate" ? "PREPLACED_SHADOW_NATIVE_RESULT_MATCH: " :
                "PREPLACED_SHADOW_NATIVE_RESULT_MISMATCH: ";
            EmitChunked(label, $"player={playerId}; helper={helper}; nativeResult=({observation.ResultX},{observation.ResultY}); " +
                $"nativeAccepted=true; shadowResult={result}; checks=[{checks}]");
        }

        private string DescribeFarmEntryGate(int playerId, short cooldown)
        {
            int mapGate = nativeModuleBase == 0 ? int.MinValue : *(int*)(nativeModuleBase + FarmSearchMapGateRva);
            int built = ReadPlayerGlobal(playerId, FarmBuiltCountRelativeOffset);
            short desired = ReadPlayerInt16(playerId, FarmDesiredCountRelativeOffset, 0);
            if (mapGate <= 19) return $"map-gate mapValue={mapGate}";
            if (built >= desired) return $"demand-satisfied built={built} desired={desired}";
            if (cooldown > 0) return $"cooldown-active value={cooldown}";
            return $"ready mapValue={mapGate} built={built} desired={desired}";
        }

        private string DescribeResourceEntryGate(int playerId, int mode, short cooldown)
        {
            int desiredOffset = mode == 2 ? QuarryDesiredCountRelativeOffset :
                mode == 3 ? IronDesiredCountRelativeOffset : mode == 4 ? PitchDesiredCountRelativeOffset : -1;
            int builtOffset = mode == 2 ? QuarryBuiltCountRelativeOffset :
                mode == 3 ? IronBuiltCountRelativeOffset : mode == 4 ? PitchBuiltCountRelativeOffset : -1;
            if (desiredOffset < 0) return "invalid-mode";
            short desired = ReadPlayerInt16(playerId, desiredOffset, 0);
            int built = ReadPlayerGlobal(playerId, builtOffset);
            if (built >= desired) return $"demand-satisfied built={built} desired={desired}";
            if (cooldown > 0) return $"cooldown-active value={cooldown}";
            return $"ready built={built} desired={desired}";
        }

        private bool PlayerClassMatches(int playerId, byte rawOwnerClass)
        {
            if (rawOwnerClass == 0) return true;
            if (nativeModuleBase == 0 || playerId < 0 || playerId >= SerializedPlayerRecordCount ||
                rawOwnerClass >= SerializedPlayerRecordCount) return false;
            int* classes = (int*)(nativeModuleBase + PlayerClassTableRva);
            return classes[playerId] == classes[rawOwnerClass];
        }

        private static string DescribeResourceShadowAnalysis(ShadowEconomyCell[] cells,
            ShadowEconomySearchResult result, int resourceMode)
        {
            var reached = new HashSet<int>(result.ReachedIndices);
            var blocked = new HashSet<int>(result.BlockedIndices);
            var groups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            var nonResourceTerrain = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < cells.Length; index++)
            {
                string terrain = ShadowEconomySearch.ResourceTerrainReason(cells[index], resourceMode);
                if (terrain != "candidate")
                {
                    nonResourceTerrain[terrain] = nonResourceTerrain.TryGetValue(terrain, out int count)
                        ? count + 1 : 1;
                    continue;
                }
                string reason;
                if (blocked.Contains(index)) reason = "pcl-expansion-boundary";
                else if (!reached.Contains(index)) reason = "outside-depth-or-disconnected";
                else reason = ShadowEconomySearch.ResourceCandidateRejectionReason(cells[index], resourceMode);
                if (reason == "candidate") reason = "candidate/reached";
                if (!groups.TryGetValue(reason, out List<int> indices))
                {
                    indices = new List<int>();
                    groups.Add(reason, indices);
                }
                indices.Add(index);
            }
            return "nonResourceTerrainReasons=[" + string.Join(",", nonResourceTerrain.Select(pair => pair.Key + "=" + pair.Value)) + "];" +
                string.Join(";", groups.Select(pair => pair.Key + "=" + pair.Value.Count + "[" +
                LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]"));
        }

        private int CountPclTilesOutsideSet(int coarseX, int coarseY, HashSet<int> reachablePcls)
        {
            int count = 0;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            int beginX = coarseX * EconomyCoarseCellTileSize;
            int beginY = coarseY * EconomyCoarseCellTileSize;
            for (int dx = 0; dx < EconomyCoarseCellTileSize; dx++)
                for (int dy = 0; dy < EconomyCoarseCellTileSize; dy++)
                {
                    int x = beginX + dx;
                    int y = beginY + dy;
                    if (!api.IsTileInsideMapBounds(x, y)) { count++; continue; }
                    int tileId = api.GetTileId(x, y);
                    if (!TryGetPclByTileId(tileId, out int pcl, "shadow-economy") || !reachablePcls.Contains(pcl)) count++;
                }
            return count;
        }

        private EconomyBarrierObservation CaptureEconomyBarrier(string helper, eStructs? desiredType,
            EconomySearchObservation observation)
        {
            var visitedPcls = new HashSet<int>();
            var frontierPcls = new HashSet<int>();
            var visitedTileIds = new HashSet<int>();
            var frontierTileIds = new HashSet<int>();
            foreach (EconomyCoordinate coordinate in observation.Visited)
                CaptureCoarseCellTopology(coordinate, visitedPcls, visitedTileIds);
            foreach (int index in observation.Frontier)
                CaptureCoarseCellTopology(EconomyCoordinate.FromIndex(index), frontierPcls, frontierTileIds);
            return new EconomyBarrierObservation(helper, desiredType, visitedPcls, frontierPcls,
                visitedTileIds, frontierTileIds);
        }

        private void CaptureCoarseCellTopology(EconomyCoordinate coordinate, HashSet<int> pcls,
            HashSet<int> tileIds)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int beginX = coordinate.X * EconomyCoarseCellTileSize;
            int beginY = coordinate.Y * EconomyCoarseCellTileSize;
            for (int dy = 0; dy < EconomyCoarseCellTileSize; dy++)
                for (int dx = 0; dx < EconomyCoarseCellTileSize; dx++)
                {
                    int x = beginX + dx;
                    int y = beginY + dy;
                    if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                    int tileId = tiles.GetTileId(x, y);
                    tileIds.Add(tileId);
                    if (TryGetPclByTileId(tileId, out int pcl, "economy-barrier")) pcls.Add(pcl);
                }
        }

        private EconomySearchObservation AnalyzeEconomySearch(ulong state, string helper, string arguments,
            EconomyGridState before, EconomyGridState after, bool? candidateFoundOverride,
            int cooldownBefore, int cooldownAfter)
        {
            if (state == 0) return EconomySearchObservation.Unavailable(helper, arguments, before, after);
            string gateReason = EconomySearchGateClassifier.Classify(before.Generation != after.Generation,
                cooldownBefore, cooldownAfter, before.QueueRead, before.QueueWrite,
                after.QueueRead, after.QueueWrite, before.ResultX, before.ResultY,
                after.ResultX, after.ResultY);
            if (before.Generation == after.Generation)
            {
                string entryGate = ExtractArgumentValue(arguments, "entryGate");
                if (entryGate.StartsWith("demand-satisfied", StringComparison.Ordinal))
                    gateReason = "early-return-demand-satisfied";
                else if (entryGate.StartsWith("map-gate", StringComparison.Ordinal))
                    gateReason = "early-return-map-gate";
                else if (entryGate == "invalid-mode")
                    gateReason = "early-return-invalid-mode";
            }
            if (before.Generation == after.Generation)
            {
                // The generation is a monotonic invocation counter, not part of the search
                // outcome. Excluding it makes identical early returns aggregate losslessly.
                ulong skippedSignature = Hash(1469598103934665603UL, after.ResultX);
                skippedSignature = Hash(skippedSignature, after.ResultY);
                return new EconomySearchObservation(helper, arguments, before, after,
                    new List<EconomyCoordinate>(), new HashSet<int>(), skippedSignature,
                    candidateFoundOverride ?? false,
                    $"arguments={arguments}; before={before}; after={after}; traversalPerformed=false; " +
                    $"earlyExit={gateReason}; visited/frontier omitted because the visit generation did not change",
                    gateReason);
            }
            byte* memory = (byte*)state;
            var visited = new List<EconomyCoordinate>();
            var frontier = new HashSet<int>();
            var rawGroups = new SortedDictionary<string, List<EconomyCoordinate>>(StringComparer.Ordinal);
            ulong signature = 1469598103934665603UL;
            for (int index = 0; index < EconomyGridCellCount; index++)
            {
                byte* cell = memory + EconomyGridBaseOffset + index * EconomyGridCellStride;
                if (*(int*)cell != after.Generation) continue;
                int x = index / EconomyGridWidth;
                int y = index % EconomyGridWidth;
                var coordinate = new EconomyCoordinate(x, y);
                visited.Add(coordinate);
                string raw = RawCellPayload(cell);
                if (!rawGroups.TryGetValue(raw, out List<EconomyCoordinate> group))
                {
                    group = new List<EconomyCoordinate>();
                    rawGroups.Add(raw, group);
                }
                group.Add(coordinate);
                signature = Hash(signature, index);
                for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                    if (offset != 0x05) signature = Hash(signature, cell[offset]);
                AddFrontier(frontier, x - 1, y, memory, after.Generation);
                AddFrontier(frontier, x + 1, y, memory, after.Generation);
                AddFrontier(frontier, x, y - 1, memory, after.Generation);
                AddFrontier(frontier, x, y + 1, memory, after.Generation);
            }
            var frontierRawGroups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            var frontierPredicateGroups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (int index in frontier.OrderBy(value => value))
            {
                byte* cell = memory + EconomyGridBaseOffset + index * EconomyGridCellStride;
                signature = Hash(signature, index);
                for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                    if (offset != 0x05) signature = Hash(signature, cell[offset]);
                string raw = RawCellPayload(cell);
                if (!frontierRawGroups.TryGetValue(raw, out List<int> rawGroup))
                {
                    rawGroup = new List<int>();
                    frontierRawGroups.Add(raw, rawGroup);
                }
                rawGroup.Add(index);
                string predicate = DescribeExpansionPredicate(helper, cell);
                if (!frontierPredicateGroups.TryGetValue(predicate, out List<int> predicateGroup))
                {
                    predicateGroup = new List<int>();
                    frontierPredicateGroups.Add(predicate, predicateGroup);
                }
                predicateGroup.Add(index);
            }
            string visitedText = LosslessGridCoordinateFormatter.Format(
                visited.Select(c => c.X * EconomyGridWidth + c.Y), EconomyGridWidth);
            string frontierText = LosslessGridCoordinateFormatter.Format(frontier, EconomyGridWidth);
            string groups = string.Join("; ", rawGroups.Select(pair =>
                pair.Key + "=>[" + LosslessGridCoordinateFormatter.Format(
                    pair.Value.Select(c => c.X * EconomyGridWidth + c.Y), EconomyGridWidth) + "]"));
            string frontierGroups = string.Join("; ", frontierRawGroups.Select(pair =>
                pair.Key + "=>[" + LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]"));
            string predicateGroups = string.Join("; ", frontierPredicateGroups.Select(pair =>
                pair.Key + "=>[" + LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]"));
            bool candidateFound = candidateFoundOverride ?? (after.ResultX >= 0 && after.ResultY >= 0);
            return new EconomySearchObservation(helper, arguments, before, after, visited, frontier, signature,
                candidateFound,
                $"arguments={arguments}; before={before}; after={after}; visitedCount={visited.Count}; visited=[{visitedText}]; " +
                $"unvisitedOrthogonalFrontierCount={frontier.Count}; unvisitedOrthogonalFrontier=[{frontierText}]; " +
                $"visitedRawGroups=[{groups}]; frontierRawGroups=[{frontierGroups}]; " +
                $"frontierExpansionPredicates=[{predicateGroups}]", gateReason);
        }

        private static string DescribeExpansionPredicate(string helper, byte* cell)
        {
            if (helper == "wood-search")
                return "rva58020(sbyte+04<16&&byte+13==0)=" +
                    ((sbyte)cell[0x04] < WoodExpansionCellValueExclusive && cell[0x13] == 0 ? "pass" : "fail") +
                    $"/byte+04={cell[0x04]}/byte+13={cell[0x13]}";
            if (helper == "farm-search")
                return "rva575B0(sbyte+04<17)=" +
                    ((sbyte)cell[0x04] < FarmExpansionCellValueExclusive ? "pass" : "fail") +
                    $"/byte+04={cell[0x04]}";
            if (helper == "resource-search")
            {
                int difference = (sbyte)cell[0x04] - (sbyte)cell[0x16];
                return "rva57B80(sbyte+04-sbyte+16<16)=" +
                    (difference < ResourceExpansionDifferenceExclusive ? "pass" : "fail") +
                    $"/byte+04={cell[0x04]}/byte+16={cell[0x16]}/difference={difference}";
            }
            if (helper == "nearby-search")
                return "rva58950(sbyte+04<15)=" +
                    ((sbyte)cell[0x04] < NearbyExpansionCellValueExclusive ? "pass" : "fail") +
                    $"/byte+04={cell[0x04]}";
            return "raw-only-no-confirmed-helper-predicate";
        }

        private static void AddFrontier(HashSet<int> frontier, int x, int y, byte* memory, int generation)
        {
            if ((uint)x >= EconomyGridWidth || (uint)y >= EconomyGridWidth) return;
            int index = x * EconomyGridWidth + y;
            byte* cell = memory + EconomyGridBaseOffset + index * EconomyGridCellStride;
            if (*(int*)cell != generation) frontier.Add(index);
        }

        private static string RawCellPayload(byte* cell)
        {
            var builder = new StringBuilder((EconomyGridCellStride - sizeof(int)) * 2 + 16);
            builder.Append("raw+04=");
            for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                builder.Append(cell[offset].ToString("X2"));
            uint raw18 = *(uint*)(cell + 0x18);
            builder.Append($"/byte+04={cell[0x04]}/byte+05={cell[0x05]}/byte+0F={cell[0x0F]}/byte+11={cell[0x11]}/byte+12={cell[0x12]}/byte+13={cell[0x13]}/byte+16={cell[0x16]}/u32+18=0x{raw18:X8}");
            return builder.ToString();
        }

        private static string StableRoutingCellPayload(byte* cell)
        {
            var builder = new StringBuilder((EconomyGridCellStride - sizeof(int) - 1) * 2 + 24);
            builder.Append("raw+04-except+05=");
            for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                if (offset != 5) builder.Append(cell[offset].ToString("X2"));
            uint raw18 = *(uint*)(cell + 0x18);
            builder.Append($"/byte+04={cell[0x04]}/byte+0F={cell[0x0F]}/byte+11={cell[0x11]}/byte+12={cell[0x12]}/byte+13={cell[0x13]}/byte+16={cell[0x16]}/u32+18=0x{raw18:X8}");
            return builder.ToString();
        }

        private string DescribeSearchRoutes(int playerId, EconomySearchObservation observation)
        {
            if (!TryGetKeepPcl(playerId, out int keepPcl)) return "keep-pcl-unavailable";
            List<PortalConnection> portals = CapturePortalConnections(out _);
            var groups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            var visitedCoordinates = new HashSet<EconomyCoordinate>(observation.Visited);
            var routeCache = new Dictionary<int, PortalRouteKind>();
            HashSet<int> friendlyReachable = PortalRouteModel.ReachableFriendlyPcls(keepPcl, portals,
                playerId, IsAllied);
            var counterfactualGroups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
            IEnumerable<EconomyCoordinate> coordinates = observation.Visited.Concat(
                observation.Frontier.Select(EconomyCoordinate.FromIndex));
            foreach (EconomyCoordinate coordinate in coordinates)
            {
                int[] pcls = GetEconomyCellPcls(coordinate.X, coordinate.Y);
                PortalRouteKind kind = BestRouteKind(keepPcl, pcls, portals, playerId, routeCache);
                string key = (visitedCoordinates.Contains(coordinate) ? "vanilla-visited/" : "vanilla-frontier/") +
                    kind + "/pcls=" + string.Join(",", pcls);
                if (!groups.TryGetValue(key, out List<int> values))
                {
                    values = new List<int>();
                    groups.Add(key, values);
                }
                values.Add(coordinate.X * EconomyGridWidth + coordinate.Y);
                string projected = DescribeCounterfactualCell(observation.Helper, coordinate,
                    friendlyReachable, observation.Frontier.Contains(coordinate.X * EconomyGridWidth + coordinate.Y));
                if (!counterfactualGroups.TryGetValue(projected, out List<int> projectedValues))
                {
                    projectedValues = new List<int>();
                    counterfactualGroups.Add(projected, projectedValues);
                }
                projectedValues.Add(coordinate.X * EconomyGridWidth + coordinate.Y);
            }
            return "keepPcl=" + keepPcl + "; " + string.Join("; ", groups.Select(pair =>
                pair.Key + "=" + pair.Value.Count + "[" +
                LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]")) +
                "; friendlyReachablePcls=[" + string.Join(",", friendlyReachable.OrderBy(value => value)) + "]" +
                "; projectedCells=[" + string.Join(";", counterfactualGroups.Select(pair => pair.Key + "=" +
                    pair.Value.Count + "[" + LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]")) + "]" +
                "; portalRecordCount=" + portals.Count + "; full records are emitted only by PREPLACED_PORTAL_TOPOLOGY";
        }

        private string DescribeCounterfactualCell(string helper, EconomyCoordinate coordinate,
            HashSet<int> friendlyReachablePcls, bool frontier)
        {
            if (lastAivState == 0) return "state-unavailable";
            int outsideReachable = 0;
            int validTiles = 0;
            int beginX = coordinate.X * EconomyCoarseCellTileSize;
            int beginY = coordinate.Y * EconomyCoarseCellTileSize;
            for (int dy = 0; dy < EconomyCoarseCellTileSize; dy++)
                for (int dx = 0; dx < EconomyCoarseCellTileSize; dx++)
                {
                    if (!TryGetPclAt(beginX + dx, beginY + dy, out int pcl)) continue;
                    validTiles++;
                    if (!friendlyReachablePcls.Contains(pcl)) outsideReachable++;
                }
            int index = coordinate.X * EconomyGridWidth + coordinate.Y;
            byte* cell = (byte*)lastAivState + EconomyGridBaseOffset + index * EconomyGridCellStride;
            int raw04 = (sbyte)cell[0x04];
            int raw16 = (sbyte)cell[0x16];
            bool projectedPass = helper == "wood-search" ? outsideReachable < WoodExpansionCellValueExclusive && cell[0x13] == 0 :
                helper == "farm-search" ? outsideReachable < FarmExpansionCellValueExclusive :
                helper == "nearby-search" ? outsideReachable < NearbyExpansionCellValueExclusive :
                helper == "resource-search" ? outsideReachable - raw16 < ResourceExpansionDifferenceExclusive : false;
            return $"{(frontier ? "frontier" : "visited")}/validTiles={validTiles}/raw04={raw04}" +
                $"/projected04={outsideReachable}/raw16={raw16}/projected16=raw-vanilla-tile-logic" +
                $"/projectedExpansion={(projectedPass ? "pass" : "fail")}";
        }

        private static bool IsAllied(int firstPlayerId, int secondPlayerId) =>
            GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(firstPlayerId, secondPlayerId);

        private PortalRouteKind BestRouteKind(int keepPcl, int[] targetPcls, List<PortalConnection> portals, int playerId,
            Dictionary<int, PortalRouteKind> cache)
        {
            if (targetPcls.Length == 0) return PortalRouteKind.Unreachable;
            PortalRouteKind best = PortalRouteKind.Unreachable;
            foreach (int targetPcl in targetPcls)
            {
                if (!cache.TryGetValue(targetPcl, out PortalRouteKind candidate))
                {
                    candidate = PortalRouteModel.Evaluate(keepPcl, targetPcl, portals).Kind;
                    cache.Add(targetPcl, candidate);
                }
                if (RouteRank(candidate) < RouteRank(best)) best = candidate;
            }
            return best;
        }

        private static int RouteRank(PortalRouteKind kind)
        {
            switch (kind)
            {
                case PortalRouteKind.Direct: return 0;
                case PortalRouteKind.RawPortalGraph: return 1;
                default: return 5;
            }
        }

        private int[] GetEconomyCellPcls(int coarseX, int coarseY)
        {
            var result = new HashSet<int>();
            int beginX = coarseX * EconomyCoarseCellTileSize;
            int beginY = coarseY * EconomyCoarseCellTileSize;
            for (int dy = 0; dy < EconomyCoarseCellTileSize; dy++)
                for (int dx = 0; dx < EconomyCoarseCellTileSize; dx++)
                    if (TryGetPclAt(beginX + dx, beginY + dy, out int pcl)) result.Add(pcl);
            return result.OrderBy(value => value).ToArray();
        }

        private void CaptureRoutingSnapshot(ulong state, string reason, bool forceFull)
        {
            if (state == 0) return;
            CaptureAndAnalyzePclTopology(reason);
            if (lastRoutingSnapshot != null && !forceFull &&
                lastRoutingSnapshot.Signature == EconomyRoutingSnapshot.ProbeSignature((byte*)state,
                    nativePclGrid, NativePclEntryCount,
                    tileId => RecordInvalidPclAccess(tileId, "economy-routing-probe"))) return;
            EconomyRoutingSnapshot current = EconomyRoutingSnapshot.Capture((byte*)state,
                nativePclGrid, NativePclEntryCount,
                tileId => RecordInvalidPclAccess(tileId, "economy-routing-capture"));
            if (lastRoutingSnapshot == null || forceFull)
            {
                if (lastRoutingSnapshot != null && lastRoutingSnapshot.Signature == current.Signature)
                {
                    EmitChunked("PREPLACED_ROUTING_SNAPSHOT_REFERENCE: ",
                        $"reason={reason}; signature={current.Signature:X16}; unchangedFromPrevious=true");
                }
                else
                {
                    EmitChunked("PREPLACED_ROUTING_SNAPSHOT_FULL: ",
                        $"reason={reason}; signature={current.Signature:X16}; " + current.DescribeSummary());
                }
                lastRoutingSnapshot = current;
                EmitProactiveShadowSuite(state, reason);
                return;
            }
            if (lastRoutingSnapshot.Signature == current.Signature) return;
            string delta = current.DescribeDelta(lastRoutingSnapshot);
            EmitChunked("PREPLACED_ROUTING_CHANGE: ",
                $"reason={reason}; old={lastRoutingSnapshot.Signature:X16}; new={current.Signature:X16}; {delta}");
            foreach (PlayerSession session in players.Values)
            {
                session.Counters.Add("routing-state-change reason=" + reason);
            }
            lastRoutingSnapshot = current;
            EmitProactiveShadowSuite(state, reason);
        }

        private void CaptureAndAnalyzePclTopology(string reason)
        {
            if (nativePclGrid == null) return;
            var current = new ushort[NativePclEntryCount];
            for (int tileId = 0; tileId < current.Length; tileId++) current[tileId] = nativePclGrid[tileId];
            if (lastPclTopology == null)
            {
                lastPclTopology = current;
                return;
            }

            foreach (PlayerSession session in players.Values)
            {
                if (session.ConfirmedWallBreach) continue;
                if (!wallBaselines.TryGetValue(session.PlayerId, out WallTileBaseline baseline)) continue;
                List<WallTileDelta> wallDeltas = CaptureWallTileDeltas(baseline);
                WallTileDelta[] materialDeltas = wallDeltas.Where(value => value.MaterialChanged).ToArray();
                int pclOnlyChanges = wallDeltas.Count(value => value.PclOnlyChanged);
                if (materialDeltas.Length != 0)
                {
                    EmitChunked($"PREPLACED_WALL_TILE_CHANGE: player={session.PlayerId}; ",
                        $"reason={reason}; role={session.WallTestRole}; materialChanges=[{string.Join(";", materialDeltas.Select(value => value.ToString()))}]");
                }
                if (pclOnlyChanges != 0)
                    session.Counters.Add($"wall-pcl-relabel-only reason={reason} tiles={pclOnlyChanges}");
                bool selectedWallLost = baseline.LostWallTiles.Any(baseline.ComponentTiles.Contains);
                if (selectedWallLost && !session.WallLossStageEmitted)
                {
                    session.WallLossStageEmitted = true;
                    session.WallLossUtc = DateTime.UtcNow;
                    EmitChunked($"PREPLACED_WALL_BREACH_STAGE: player={session.PlayerId}; ",
                        $"stage=baseline-wall-loss; reason={reason}; role={session.WallTestRole}; " +
                        $"lostWallTiles=[{string.Join(",", baseline.LostWallTiles.Where(baseline.ComponentTiles.Contains).OrderBy(value => value))}]");
                }
                bool physicallyConnected = selectedWallLost && IsBaselineInteriorConnectedToExterior(baseline);
                WallAnchorPair confirmed = baseline.Anchors.FirstOrDefault(anchor =>
                    WallBreachConfirmation.IsConfirmed(true,
                        anchor.OldInsidePcl, anchor.OldOutsidePcl,
                        current[anchor.InsideTileId], current[anchor.OutsideTileId]));
                if (selectedWallLost && confirmed != null && !session.AnchorConnectionStageEmitted)
                {
                    session.AnchorConnectionStageEmitted = true;
                    EmitChunked($"PREPLACED_WALL_BREACH_STAGE: player={session.PlayerId}; ",
                        $"stage=stable-pcl-anchor-connected; reason={reason}; physicalFloodConnected={physicallyConnected}; " +
                        $"anchorWall={confirmed.WallTileId}; insideTile={confirmed.InsideTileId}; outsideTile={confirmed.OutsideTileId}; " +
                        $"pcl={confirmed.OldInsidePcl}+{confirmed.OldOutsidePcl}->{current[confirmed.InsideTileId]}");
                }
                if (!selectedWallLost || confirmed == null) continue;
                session.ConfirmedWallBreach = true;
                session.WallBreachUtc = DateTime.UtcNow;
                session.Counters.Add("confirmed-wall-breach reason=" + reason);
                EmitChunked($"PREPLACED_CONFIRMED_WALL_BREACH: player={session.PlayerId}; ",
                    $"reason={reason}; role={session.WallTestRole}; confirmation=selected-baseline-wall-lost+stable-anchor-connectivity; " +
                    $"physicalFloodConnected={physicallyConnected}; " +
                    $"lostWallTiles=[{string.Join(",", baseline.LostWallTiles.Where(baseline.ComponentTiles.Contains).OrderBy(value => value))}]; " +
                    $"anchorWall={confirmed.WallTileId}; insideTile={confirmed.InsideTileId}; outsideTile={confirmed.OutsideTileId}; " +
                    $"pcl={confirmed.OldInsidePcl}+{confirmed.OldOutsidePcl}->{current[confirmed.InsideTileId]}");
            }
            lastPclTopology = current;
        }

        private static string ExtractArgumentValue(string arguments, string name)
        {
            string marker = "/" + name + "=";
            int start = arguments.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            start += marker.Length;
            int end = arguments.IndexOf('/', start);
            return end < 0 ? arguments.Substring(start) : arguments.Substring(start, end - start);
        }

        private bool IsBaselineInteriorConnectedToExterior(WallTileBaseline baseline)
        {
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            var currentBlockers = new HashSet<int>();
            foreach (int tileId in baseline.ComponentTiles)
                if (CaptureWallTileState(tileId,
                    (int)api.GetTileVectorFromId(tileId).X,
                    (int)api.GetTileVectorFromId(tileId).Y).IsWall)
                    currentBlockers.Add(tileId);
            foreach (BuildingSnapshot enclosure in CaptureRawBuildings().Where(value =>
                value.OwnerId == baseline.PlayerId && IsLiving(value) && IsEnclosureBuilding(value.Type)))
                for (int x = enclosure.TileX; x <= enclosure.EndX; x++)
                    for (int y = enclosure.TileY; y <= enclosure.EndY; y++)
                        if (api.IsTileInsideMapBounds(x, y))
                        {
                            int tileId = api.GetTileId(x, y);
                            if (baseline.ComponentBlockers.Contains(tileId)) currentBlockers.Add(tileId);
                        }
            HashSet<int> reached = FloodPassable(new[] { api.GetTileId(baseline.KeepX, baseline.KeepY) },
                currentBlockers, api);
            return reached.Overlaps(baseline.ExteriorTiles);
        }

        private static ulong Hash(ulong value, int data)
        {
            unchecked
            {
                value ^= (uint)data;
                return value * 1099511628211UL;
            }
        }

        private Dictionary<int, PreplacedIdentity> CaptureOwnedIdentities(int playerId) =>
            CaptureBuildings(playerId).ToDictionary(building => building.Id, building => building.Identity);

        private static string DescribeIdentityDelta(Dictionary<int, PreplacedIdentity> before,
            Dictionary<int, PreplacedIdentity> after)
        {
            if (before == null) return "unavailable";
            string[] added = after.Where(pair => !before.TryGetValue(pair.Key, out PreplacedIdentity old) || !old.Equals(pair.Value))
                .Select(pair => pair.Key + "/" + pair.Value.GlobalId).ToArray();
            string[] removed = before.Where(pair => !after.TryGetValue(pair.Key, out PreplacedIdentity current) || !current.Equals(pair.Value))
                .Select(pair => pair.Key + "/" + pair.Value.GlobalId).ToArray();
            return "added=[" + string.Join(",", added) + "]/removed=[" + string.Join(",", removed) + "]";
        }

        private string ClassifyExecuteResult(int result, ExecuteContext context, FrameSnapshot before, FrameSnapshot after)
        {
            if (result != 0) return context.SpawnedBuildingIds.Count != 0 ? "success-building-spawn" :
                before.Status != after.Status ? "success-frame-advanced-no-building" : "success-command-or-nonbuilding";
            if (context.ResourceResults.Any(v => v == 0)) return "insufficient-resources-or-resource-gate";
            if (context.WaitRejectors.Count != 0) return "mapper-wait-gate:" + string.Join(",", context.WaitRejectors);
            if (context.ReachabilityResults.Any(v => v == 0)) return "placement-pcl-unreachable";
            if (context.ValidatorResults.Any(PlacementValidatorResult.IsRejected)) return "placement-validator-rejected";
            if (context.PlacementResults.Any(v => v == 0)) return "placement-helper-rejected";
            if (context.ValidatorResults.Count == 0 && context.PlacementResults.Count == 0) return "rejected-before-placement-helper";
            return "placement-failed-unspecified";
        }

        private void CorrelateExecuteSpawns(ExecuteContext context, int playerId)
        {
            foreach (BuildingSnapshot building in CaptureBuildings(playerId))
            {
                bool isNewIdentity = !context.BeforeIdentities.TryGetValue(building.Id, out PreplacedIdentity old) ||
                    !old.Equals(building.Identity);
                bool eligible = FirstAivBuildingEligibility.IsEligible(
                    isNewIdentity && building.GlobalId != 0 && building.Alive == AliveState.IsAlive,
                    IsWallStructure(building.Type));
                bool matchingSignal = context.SpawnSignals.Any(signal => FirstAivSpawnCorrelation.Matches(
                    building.TileX, building.TileY, building.EndX, building.EndY, (int)building.Type,
                    signal.X, signal.Y, (int)signal.Type));
                if (eligible && matchingSignal) context.SpawnedBuildingIds.Add(building.Id);
            }
            if (context.SpawnSignals.Count != 0 && context.SpawnedBuildingIds.Count == 0)
                RecordOwnerEvent(playerId, "spawn-signals-without-matching-new-building signals=[" +
                    string.Join(",", context.SpawnSignals) + "]");
        }

        private void OnMapLoad(MapLoadEventArgs args) => Safe(() => ProcessMapLoad(args));

        private void OnLoadSave(LoadSaveGameEventArgs args) => Safe(() =>
        {
            loadSaveEventObserved = true;
            loadingEditorMap = args.LoadingEditorMap;
            currentMapIsSave = !args.LoadingEditorMap;
            Shared.DebugLogHelper.LogInfo(log,
                $"PREPLACED_LOAD_SAVE_CONTEXT: phase={args.Phase}; loadingEditorMap={args.LoadingEditorMap}; " +
                $"file={args.FileName}; sequence={mapSequence}.");
        });

        private void ProcessMapLoad(MapLoadEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                ResetMap("OnLoadMap(Pre)");
                mapActive = true;
                currentMapIsSave = args.bMultiplayerSave != 0;
                Safe(() => ObservePhase("map-load.pre", false));
            }
            else Safe(() => ObservePhase("map-load.post", true));
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_MAP_LOAD: phase={args.Phase}, sequence={mapSequence}.");
        }

        private void OnMapStart(MapStartEventArgs args) => Safe(() => ProcessMapStart(args));

        private void ProcessMapStart(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre) initializationTracingActive = true;
            if (args.Phase == EventHookPhase.Pre && args.bMultiplayerSave != 0) currentMapIsSave = true;
            Safe(() => ObservePhase(args.Phase == EventHookPhase.Pre ? "map-start.pre" : "map-start.post", true));
            if (args.Phase == EventHookPhase.Post)
            {
                aiOwnershipResolved = true;
                CapturePreplacedBaseline();
                EmitLegacyCopyBuildingCorrelation();
                EmitLegacyTimerFixEligibility();
                ResolveWallTestRoles();
                DrainCrushedWriterSignals();
                for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
                {
                    string[] earlyEvents = earlyOwnerEvents.Drain(playerId);
                    if (IsAi(playerId))
                    {
                        PlayerSession session = Session(playerId);
                        session.Counters.Add("lifecycle.map-start");
                        foreach (string earlyEvent in earlyEvents) session.Counters.Add("early." + earlyEvent);
                        if (earlyEvents.Length != 0) Immediate(playerId, "EARLY_OWNER_EVENTS_REPLAYED: count=" + earlyEvents.Length);
                        AdoptEarlyInventories(session);
                        if (session.HasAivArea) ReclassifyPendingRawInventories(session);
                        CaptureAndEmitInventory(session, "MAP_START_POST");
                        EmitPortalTopology(playerId, "MAP_START_POST", CaptureReachability(playerId, -1, -1));
                    }
                    else
                    {
                        foreach (string earlyEvent in earlyEvents) RecordUnattributed("early-non-ai owner=" + playerId + " " + earlyEvent);
                        if (earlyOwnerInventories.TryGetValue(playerId, out List<InventoryRecord> inventories))
                        {
                            unattributedCounters.Add("early-non-ai-inventories owner=" + playerId, inventories.Count);
                            earlyOwnerInventories.Remove(playerId);
                        }
                    }
                }
                if (lastAivState != 0) CaptureRoutingSnapshot(lastAivState, "MAP_START_POST", true);
                else Shared.DebugLogHelper.LogInfo(log, "PREPLACED_ROUTING_SNAPSHOT_DEFERRED: reason=MAP_START_POST; AIV state is not known yet.");
            }
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_MAP_START: phase={args.Phase}, sequence={mapSequence}.");
        }

        private void OnMapUnload(MapUnloadEventArgs args) => Safe(() => ProcessMapUnload(args));

        private void ProcessMapUnload(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            mapActive = false;
            Safe(() => ObservePhase("map-unload.pre", true));
            foreach (int playerId in players.Keys.ToArray()) FinalizePlayer(playerId, "map-unload");
            FinalizeUnattributed("map-unload");
        }

        public void PollFrame()
        {
            if (!mapActive) return;
            Safe(() =>
            {
                DrainCrushedWriterSignals();
                if (activeLayoutIndexBase == 0) return;
                ObserveCrushedCounters("frame-poll");
                ObserveRawBuildingDeltasIfDue();
                if (lastAivState != 0 && DateTime.UtcNow >= nextRoutingPollUtc)
                {
                    CaptureRoutingSnapshot(lastAivState, "PERIODIC_ONE_SECOND", false);
                    ObservePortalTopology("PERIODIC_ONE_SECOND");
                    nextRoutingPollUtc = DateTime.UtcNow.AddSeconds(1);
                }
                foreach (PlayerSession session in players.Values.ToArray())
                {
                    if (session.Finalized || DateTime.UtcNow < session.NextFlushUtc) continue;
                    if (lastAivState != 0)
                        FlushIfDue(session.PlayerId, lastAivState, ReadSchedulerState(lastAivState, session.PlayerId));
                }
            });
        }

        private void OnBuildStructure(BuildStructureEventArgs args)
        {
            Safe(() =>
            {
                RecordOwnerEvent(args.PlayerId,
                    $"build-structure phase={args.Phase} mapper={args.Mappers} pos=({args.TileX},{args.TileY}) free={args.IsFree}");
                if (args.Phase == EventHookPhase.Post && lastAivState != 0 && aiOwnershipResolved)
                {
                    CaptureRoutingSnapshot(lastAivState, "BUILD_STRUCTURE_POST_" + args.Mappers, false);
                    ObservePortalTopology("BUILD_STRUCTURE_POST_" + args.Mappers);
                }
            });
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            Safe(() =>
            {
                if (args.Phase != EventHookPhase.Post) return;
                RecordOwnerEvent(args.PlayerId,
                    $"spawn type={args.Building} pos=({args.TileX},{args.TileY}) result={args.ReturnValue}");
                if (activeExecute != null && activeExecute.PlayerId == args.PlayerId)
                    activeExecute.SpawnSignals.Add(new BuildingSpawnSignal(args.Building, args.TileX, args.TileY, args.ReturnValue));
                EconomyContext economy = CurrentEconomy(args.PlayerId);
                economy?.SpawnSignals.Add($"{args.Building}@{args.TileX},{args.TileY}/return={args.ReturnValue}");
            });
        }

        private void OnPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            Safe(() => RecordOwnerEvent(args.PlayerId,
                $"placement-validation phase={args.Phase} mapper={args.Mappers} pos=({args.TileX},{args.TileY}) custom={args.CustomValidationRules} forceBlock={args.ForceBlockPlacementState}"));
        }

        private void OnBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            Safe(() => ProcessBuildingDamage(args));
        }

        private void ProcessBuildingDamage(BuildingTileTakeDamageEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                DamageContext context = CaptureDamageContext(args);
                pendingDamage.Push(context);
                RecordOwnerEvent(context.OwnerId, DescribeDamageAggregate("pre", context, args, null, context.DelayBefore));
                return;
            }

            DamageContext completed = pendingDamage.Count == 0 ? DamageContext.Unmatched(args) : pendingDamage.Pop();
            int afterCounter = IsValidOwner(completed.OwnerId) ? ReadPlayerGlobal(completed.OwnerId, CrushedCounterRelativeOffset) : -1;
            string postTarget = completed.Building.HasValue && TryCaptureBuilding(completed.Building.Value.Id, out BuildingSnapshot afterBuilding)
                ? afterBuilding.ToText(null) : "building=removed-or-unresolved";
            BuildingSnapshot? capturedAfter = completed.Building.HasValue && TryCaptureBuilding(completed.Building.Value.Id,
                out BuildingSnapshot postBuilding) ? (BuildingSnapshot?)postBuilding : null;
            RecordOwnerEvent(completed.OwnerId,
                DescribeDamageAggregate("post", completed, args, capturedAfter, afterCounter));
            bool lethalInput = completed.Building.HasValue &&
                DamageObservationModel.IsLethalInput(completed.Building.Value.CurrentHealth, args.Damage);
            if (lethalInput)
                Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_LETHAL_DAMAGE: player={completed.OwnerId}; " +
                    completed.Describe(args) + $"; postTarget={postTarget}; delay={completed.DelayBefore}->{afterCounter}.");
            if (CrushedTimerTransition.IsActivation(completed.DelayBefore, afterCounter))
            {
                Shared.DebugLogHelper.LogWarning(log,
                    $"PREPLACED_CRUSHED_TIMER_ACTIVATED_BY_DAMAGE: player={completed.OwnerId}; {completed.Describe(args)}; postTarget={postTarget}; delay=0->1.");
                CaptureOwnerInventory(completed.OwnerId, "CRUSHED_ACTIVATION");
            }
            if (completed.Building.HasValue)
            {
                BuildingSnapshot beforeBuilding = completed.Building.Value;
                bool wallOrPortal = IsWallStructure(beforeBuilding.Type) || IsPortalStructure(beforeBuilding.Type);
                bool mayHaveChangedRouting = wallOrPortal &&
                    (DamageObservationModel.IsLethalInput(beforeBuilding.CurrentHealth, args.Damage) ||
                     !TryCaptureBuilding(beforeBuilding.Id, out BuildingSnapshot remaining) ||
                     remaining.Alive != AliveState.IsAlive);
                if (mayHaveChangedRouting)
                {
                    if (lastAivState != 0)
                    {
                        CaptureRoutingSnapshot(lastAivState, "LETHAL_WALL_OR_PORTAL_DAMAGE_POST", false);
                        ObservePortalTopology("LETHAL_WALL_OR_PORTAL_DAMAGE_POST");
                    }
                    MarkPossibleBreach(completed.OwnerId, "lethal-or-removed-damage", beforeBuilding);
                }
            }
        }

        private string DescribeDamageAggregate(string phase, DamageContext context,
            BuildingTileTakeDamageEventArgs args, BuildingSnapshot? after, int afterCounter)
        {
            if (!context.Building.HasValue)
                return $"damage.{phase} target=unresolved amount={args.Damage} sourcePlayer={args.PlayerIdSource} " +
                    $"activationMode={args.Unknown3} unknown1={args.Unknown1} unknown4={args.Unknown4}";
            BuildingSnapshot building = context.Building.Value;
            bool lethal = DamageObservationModel.IsLethalInput(building.CurrentHealth, args.Damage);
            string postState = phase == "post" ?
                "/postState=" + (after.HasValue ? after.Value.Alive.ToString() : "removed-or-unresolved") +
                "/delay=" + context.DelayBefore + "->" + afterCounter : string.Empty;
            return $"damage.{phase} type={building.Type} preplaced={context.WasPreplaced} " +
                $"amount={args.Damage} lethalInput={lethal} sourcePlayer={args.PlayerIdSource} " +
                $"activationMode={args.Unknown3} unknown1={args.Unknown1} unknown4={args.Unknown4}{postState}";
        }

        private void OnBuildingBulldoze(BuildingBulldozeEventArgs args) => Safe(() => RecordRemoval("bulldoze", args.Phase, args.BuildingId));
        private void OnBuildingDelete(BuildingDeleteEventArgs args) => Safe(() => RecordRemoval("delete", args.Phase, args.BuildingId));

        private void RecordRemoval(string kind, EventHookPhase phase, int buildingId)
        {
            if (phase == EventHookPhase.Post)
            {
                if (lastAivState != 0)
                {
                    string reason = kind.ToUpperInvariant() + "_POST";
                    CaptureRoutingSnapshot(lastAivState, reason, false);
                    ObservePortalTopology(reason);
                }
                return;
            }
            if (!TryCaptureBuilding(buildingId, out BuildingSnapshot building)) return;
            RecordOwnerEvent(building.OwnerId, $"{kind} {building.ToText(TryGetArea(building.OwnerId, building))}");
            if (IsAi(building.OwnerId))
                Immediate(building.OwnerId, $"BUILDING_REMOVAL: kind={kind}, {building.ToText(TryGetArea(building.OwnerId, building))}");
            if (IsWallStructure(building.Type) || IsPortalStructure(building.Type))
                MarkPossibleBreach(building.OwnerId, kind, building);
        }

        private void MarkPossibleBreach(int ownerId, string reason, BuildingSnapshot building)
        {
            if (!IsAi(ownerId)) return;
            PlayerSession session = Session(ownerId);
            session.Counters.Add("possible-wall-breach reason=" + reason + " type=" + building.Type);
            if (session.FirstPossibleBreachObserved) return;
            session.FirstPossibleBreachObserved = true;
            Immediate(ownerId, "POSSIBLE_WALL_BREACH: reason=" + reason + "; " +
                building.ToText(TryGetArea(ownerId, building)));
        }

        private void ResolveWallTestRoles()
        {
            List<BuildingSnapshot> buildings = CaptureRawBuildings();
            WallOwnerEncoding encoding = ResolveWallOwnerEncoding(buildings, out int oneBasedMatches,
                out int zeroBasedMatches, out string correlation);
            var rows = new List<string>();
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                if (!IsAi(playerId)) continue;
                WallTileBaseline baseline = CaptureWallBaseline(playerId, encoding, buildings);
                if (baseline != null) wallBaselines[playerId] = baseline;
                int wallCount = baseline?.ComponentTiles.Count ?? 0;
                int portalCount = baseline == null ? 0 : buildings.Count(building =>
                    building.OwnerId == playerId && IsLiving(building) && IsPortalStructure(building.Type) &&
                    BuildingFootprintOverlaps(building, baseline.ComponentBlockers));
                WallTestRole role = WallTestRoleClassifier.Classify(wallCount, portalCount);
                PlayerSession session = Session(playerId);
                session.WallTestRole = role;
                session.Counters.Add($"wall-test-role role={role} walls={wallCount} portals={portalCount}");
                rows.Add($"player={playerId}/role={role}/wallTiles={wallCount}/portals={portalCount}" +
                    $"/componentTiles={(baseline?.ComponentTiles.Count ?? 0)}/anchors={(baseline?.Anchors.Count ?? 0)}" +
                    $"/geometryClosed={baseline?.GeometryClosed}/confidence={(baseline != null && baseline.GeometryClosed ? "geometry+pcl" : "pcl-component-candidate")}");
            }
            string ambiguity = rows.Count(row => row.Contains("role=GatedWallCandidate")) > 1 ||
                rows.Count(row => row.Contains("role=ClosedWallCandidate")) > 1
                ? "multiple-candidates-observed" : "none";
            EmitChunked("PREPLACED_DYNAMIC_WALL_ROLES: ",
                $"sequence={mapSequence}; wallOwnerEncoding={encoding}; oneBasedMatches={oneBasedMatches}; " +
                $"zeroBasedMatches={zeroBasedMatches}; correlation=[{correlation}]; ambiguity={ambiguity}; " +
                $"roles=[{string.Join("; ", rows)}]; roles are derived anew from tile walls and current portal-building owners");
        }

        private WallOwnerEncoding ResolveWallOwnerEncoding(List<BuildingSnapshot> buildings,
            out int oneBasedMatches, out int zeroBasedMatches, out string correlation)
        {
            oneBasedMatches = 0;
            zeroBasedMatches = 0;
            var rows = new List<string>();
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            var tiles = api.TileManager;
            foreach (BuildingSnapshot building in buildings.Where(value => IsLiving(value) && IsPortalStructure(value.Type)))
            {
                var rawCounts = new SortedDictionary<int, int>();
                for (int x = building.TileX; x <= building.EndX; x++)
                    for (int y = building.TileY; y <= building.EndY; y++)
                    {
                        if (!api.IsTileInsideMapBounds(x, y)) continue;
                        int tileId = api.GetTileId(x, y);
                        if ((tiles.LogicGrid[tileId] & (int)TilePropertyFlag.IsWall) == 0) continue;
                        byte raw = tiles.WallOwnerGrid[tileId];
                        rawCounts[raw] = rawCounts.TryGetValue(raw, out int count) ? count + 1 : 1;
                        if (raw == building.OwnerId) oneBasedMatches++;
                        if (raw + 1 == building.OwnerId) zeroBasedMatches++;
                    }
                rows.Add($"building={building.Id}/owner={building.OwnerId}/raw=[{string.Join(",", rawCounts.Select(pair => pair.Key + "=" + pair.Value))}]");
            }
            correlation = string.Join(";", rows);
            return WallOwnerEncodingResolver.Resolve(oneBasedMatches, zeroBasedMatches);
        }

        private static bool BuildingFootprintOverlaps(BuildingSnapshot building, HashSet<int> tileIds)
        {
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            for (int x = building.TileX; x <= building.EndX; x++)
                for (int y = building.TileY; y <= building.EndY; y++)
                    if (api.IsTileInsideMapBounds(x, y) && tileIds.Contains(api.GetTileId(x, y)))
                        return true;
            return false;
        }

        private WallTileBaseline CaptureWallBaseline(int playerId, WallOwnerEncoding encoding,
            List<BuildingSnapshot> buildings)
        {
            if (encoding == WallOwnerEncoding.Unresolved) return null;
            if (!TryGetKeepPosition(playerId, out int keepX, out int keepY)) return null;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            var manager = api.TileManager;
            var all = new Dictionary<int, WallTileState>();
            for (int x = 0; x < NativeTileGridWidth; x++)
                for (int y = 0; y < NativeTileGridWidth; y++)
                {
                    if (!api.IsTileInsideMapBounds(x, y)) continue;
                    int tileId = api.GetTileId(x, y);
                    if ((manager.LogicGrid[tileId] & (int)TilePropertyFlag.IsWall) == 0) continue;
                    byte rawOwner = manager.WallOwnerGrid[tileId];
                    if (WallOwnerEncodingResolver.Decode(rawOwner, encoding) != playerId) continue;
                    all[tileId] = CaptureWallTileState(tileId, x, y);
                }
            if (all.Count == 0) return null;

            // Keeps expose wall flags themselves. They are not part of a surrounding
            // player-built enclosure and previously caused the 25-tile false component.
            foreach (BuildingSnapshot building in buildings.Where(value => value.OwnerId == playerId &&
                IsLiving(value) && value.TileX <= keepX && keepX <= value.EndX &&
                value.TileY <= keepY && keepY <= value.EndY))
                for (int x = building.TileX; x <= building.EndX; x++)
                    for (int y = building.TileY; y <= building.EndY; y++)
                        if (api.IsTileInsideMapBounds(x, y)) all.Remove(api.GetTileId(x, y));
            if (all.Count == 0) return null;

            var wallOnlyBlockers = new HashSet<int>(all.Keys);
            int keepTileId = api.GetTileId(keepX, keepY);
            HashSet<int> wallOnlyInterior = FloodPassable(new[] { keepTileId }, wallOnlyBlockers, api);
            HashSet<int> wallOnlyExterior = FloodPassable(EnumerateMapBoundaryTiles(wallOnlyBlockers, api), wallOnlyBlockers, api);

            var blockers = new HashSet<int>(all.Keys);
            foreach (BuildingSnapshot enclosure in buildings.Where(value => value.OwnerId == playerId &&
                IsLiving(value) && IsEnclosureBuilding(value.Type)))
                for (int x = enclosure.TileX; x <= enclosure.EndX; x++)
                    for (int y = enclosure.TileY; y <= enclosure.EndY; y++)
                        if (api.IsTileInsideMapBounds(x, y)) blockers.Add(api.GetTileId(x, y));

            HashSet<int> interior = FloodPassable(new[] { keepTileId }, blockers, api);
            HashSet<int> exterior = FloodPassable(EnumerateMapBoundaryTiles(blockers, api), blockers, api);
            HashSet<int> componentBlockers = SelectEnclosureBlockerComponent(blockers, all,
                interior, exterior, api, out List<HashSet<int>> candidateComponents);
            var component = new HashSet<int>(componentBlockers.Where(all.ContainsKey));
            var anchors = new List<WallAnchorPair>();
            foreach (int wallTileId in component)
            {
                WallTileState wall = all[wallTileId];
                int insideTile = FindNearestRegionTile(wall.X, wall.Y, interior, api);
                int outsideTile = FindNearestRegionTile(wall.X, wall.Y, exterior, api);
                if (insideTile < 0 || outsideTile < 0) continue;
                int insidePcl = TryGetPclByTileId(insideTile, out int capturedInside, "wall-anchor-inside") ? capturedInside : 0;
                int outsidePcl = TryGetPclByTileId(outsideTile, out int capturedOutside, "wall-anchor-outside") ? capturedOutside : 0;
                if (insidePcl <= 0 || outsidePcl <= 0 || insidePcl == outsidePcl) continue;
                anchors.Add(new WallAnchorPair(wallTileId, insideTile, outsideTile, insidePcl, outsidePcl));
            }
            AddPclAdjacencyAnchors(component, anchors, keepTileId, api);
            EmitChunked($"PREPLACED_WALL_BASELINE: player={playerId}; ",
                $"encoding={encoding}; keep=({keepX},{keepY}); ownedWallTiles={all.Count}; " +
                $"wallOnlyClosed={!wallOnlyInterior.Overlaps(wallOnlyExterior)}; wallAwareClosed={!interior.Overlaps(exterior)}; " +
                $"interiorTiles={interior.Count}; exteriorTiles={exterior.Count}; " +
                $"selectedComponentTiles={component.Count}; selectedBlockers={componentBlockers.Count}; anchors={anchors.Count}; " +
                $"candidateComponents=[{string.Join(";", candidateComponents.Select((value, index) =>
                    index + ":blockers=" + value.Count + "/walls=" + value.Count(all.ContainsKey) +
                    "/selected=" + ReferenceEquals(value, componentBlockers)))}]; " +
                $"tiles=[{string.Join(",", component.OrderBy(value => value))}]; " +
                $"anchorPairs=[{string.Join(";", anchors.Select(value => value.ToString()))}]");
            return new WallTileBaseline(playerId, encoding, keepX, keepY, all, component,
                componentBlockers, candidateComponents, interior, exterior, !interior.Overlaps(exterior), anchors);
        }

        private void AddPclAdjacencyAnchors(HashSet<int> component, List<WallAnchorPair> anchors,
            int keepTileId, GameTileManagerAPI api)
        {
            if (!TryGetPclByTileId(keepTileId, out int keepPcl, "wall-anchor-keep")) keepPcl = 0;
            var known = new HashSet<string>(anchors.Select(value => value.InsideTileId + "/" + value.OutsideTileId));
            int before = anchors.Count;
            AddPclAdjacencyAnchors(component, anchors, known, api, keepPcl);
            // Some enclosures fragment the keep-side label before the first snapshot. Preserve an unbiased fallback.
            if (anchors.Count == before && keepPcl > 0)
                AddPclAdjacencyAnchors(component, anchors, known, api, 0);
        }

        private void AddPclAdjacencyAnchors(HashSet<int> component, List<WallAnchorPair> anchors,
            HashSet<string> known, GameTileManagerAPI api, int requiredInsidePcl)
        {
            foreach (int wallTileId in component)
            {
                var vector = api.GetTileVectorFromId(wallTileId);
                var adjacent = new List<(int TileId, int Pcl)>();
                foreach (int tileId in GetAdjacentTileIds((int)vector.X, (int)vector.Y, api, true))
                    if (!component.Contains(tileId) && TryGetPclByTileId(tileId, out int pcl, "wall-anchor-adjacent") && pcl > 0)
                        adjacent.Add((tileId, pcl));
                foreach (var inside in adjacent)
                    foreach (var outside in adjacent)
                    {
                        if (inside.TileId == outside.TileId || inside.Pcl == outside.Pcl) continue;
                        if (requiredInsidePcl > 0 && inside.Pcl != requiredInsidePcl) continue;
                        string key = inside.TileId + "/" + outside.TileId;
                        if (!known.Add(key)) continue;
                        anchors.Add(new WallAnchorPair(wallTileId, inside.TileId, outside.TileId,
                            inside.Pcl, outside.Pcl));
                    }
            }
        }

        private static HashSet<int> SelectEnclosureBlockerComponent(HashSet<int> blockers,
            Dictionary<int, WallTileState> walls, HashSet<int> interior, HashSet<int> exterior,
            GameTileManagerAPI api, out List<HashSet<int>> candidateComponents)
        {
            var remaining = new HashSet<int>(blockers);
            HashSet<int> best = new HashSet<int>();
            int bestWallCount = 0;
            candidateComponents = new List<HashSet<int>>();
            while (remaining.Count != 0)
            {
                int seed = remaining.First();
                remaining.Remove(seed);
                var component = new HashSet<int> { seed };
                var queue = new Queue<int>();
                queue.Enqueue(seed);
                bool touchesInterior = false;
                bool touchesExterior = false;
                while (queue.Count != 0)
                {
                    int tileId = queue.Dequeue();
                    var vector = api.GetTileVectorFromId(tileId);
                    foreach (int neighbor in GetOrthogonalTileIds((int)vector.X, (int)vector.Y, api))
                    {
                        if (interior.Contains(neighbor)) touchesInterior = true;
                        if (exterior.Contains(neighbor)) touchesExterior = true;
                    }
                    foreach (int neighbor in GetAdjacentTileIds((int)vector.X, (int)vector.Y, api, true))
                        if (remaining.Remove(neighbor) && blockers.Contains(neighbor))
                        { component.Add(neighbor); queue.Enqueue(neighbor); }
                }
                int wallCount = component.Count(walls.ContainsKey);
                if (wallCount != 0) candidateComponents.Add(component);
                if (touchesInterior && touchesExterior && wallCount > bestWallCount)
                { bestWallCount = wallCount; best = component; }
            }
            if (best.Count == 0 && candidateComponents.Count != 0)
                best = candidateComponents.OrderByDescending(value => value.Count(walls.ContainsKey)).First();
            return best;
        }

        private static HashSet<int> FloodPassable(IEnumerable<int> seeds, HashSet<int> blockers,
            GameTileManagerAPI api)
        {
            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (int seed in seeds)
                if (!blockers.Contains(seed) && reached.Add(seed)) queue.Enqueue(seed);
            while (queue.Count != 0)
            {
                int tileId = queue.Dequeue();
                var vector = api.GetTileVectorFromId(tileId);
                foreach (int neighbor in GetOrthogonalTileIds((int)vector.X, (int)vector.Y, api))
                    if (!blockers.Contains(neighbor) && reached.Add(neighbor)) queue.Enqueue(neighbor);
            }
            return reached;
        }

        private static IEnumerable<int> EnumerateMapBoundaryTiles(HashSet<int> blockers,
            GameTileManagerAPI api)
        {
            for (int x = 0; x < NativeTileGridWidth; x++)
                for (int y = 0; y < NativeTileGridWidth; y++)
                {
                    if (!api.IsTileInsideMapBounds(x, y)) continue;
                    int tileId = api.GetTileId(x, y);
                    if (blockers.Contains(tileId)) continue;
                    if (!api.IsTileInsideMapBounds(x - 1, y) || !api.IsTileInsideMapBounds(x + 1, y) ||
                        !api.IsTileInsideMapBounds(x, y - 1) || !api.IsTileInsideMapBounds(x, y + 1))
                        yield return tileId;
                }
        }

        private static int FindNearestRegionTile(int x, int y, HashSet<int> region,
            GameTileManagerAPI api)
        {
            for (int distance = 1; distance < NativeTileGridWidth; distance++)
            {
                for (int dx = -distance; dx <= distance; dx++)
                {
                    int dy = distance - Math.Abs(dx);
                    if (api.IsTileInsideMapBounds(x + dx, y + dy))
                    {
                        int tileId = api.GetTileId(x + dx, y + dy);
                        if (region.Contains(tileId)) return tileId;
                    }
                    if (dy != 0 && api.IsTileInsideMapBounds(x + dx, y - dy))
                    {
                        int tileId = api.GetTileId(x + dx, y - dy);
                        if (region.Contains(tileId)) return tileId;
                    }
                }
            }
            return -1;
        }

        private static IEnumerable<int> GetAdjacentTileIds(int x, int y, GameTileManagerAPI api,
            bool includeDiagonals)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!includeDiagonals && dx != 0 && dy != 0) continue;
                    if (api.IsTileInsideMapBounds(x + dx, y + dy)) yield return api.GetTileId(x + dx, y + dy);
                }
        }

        private static IEnumerable<int> GetOrthogonalTileIds(int x, int y, GameTileManagerAPI api)
        {
            if (api.IsTileInsideMapBounds(x - 1, y)) yield return api.GetTileId(x - 1, y);
            if (api.IsTileInsideMapBounds(x + 1, y)) yield return api.GetTileId(x + 1, y);
            if (api.IsTileInsideMapBounds(x, y - 1)) yield return api.GetTileId(x, y - 1);
            if (api.IsTileInsideMapBounds(x, y + 1)) yield return api.GetTileId(x, y + 1);
        }

        private WallTileState CaptureWallTileState(int tileId, int x, int y)
        {
            var manager = GameTileManagerAPI.Instance.TileManager;
            return new WallTileState(tileId, x, y, manager.LogicGrid[tileId], manager.WallOwnerGrid[tileId],
                manager.DamageGrid[tileId], manager.StructureWasGrid[tileId], manager.GatePathGrid[tileId],
                TryGetPclByTileId(tileId, out int pcl, "wall-tile") ? pcl : 0);
        }

        private List<WallTileDelta> CaptureWallTileDeltas(WallTileBaseline baseline)
        {
            var result = new List<WallTileDelta>();
            foreach (WallTileState original in baseline.Tiles.Values)
            {
                WallTileState before = baseline.LastObservedTiles[original.TileId];
                WallTileState after = CaptureWallTileState(original.TileId, original.X, original.Y);
                if (!before.DataEquals(after)) result.Add(new WallTileDelta(before, after));
                if (original.IsWall && !after.IsWall) baseline.LostWallTiles.Add(original.TileId);
                baseline.LastObservedTiles[original.TileId] = after;
            }
            return result;
        }

        private static bool TryGetKeepPosition(int playerId, out int x, out int y)
        {
            x = 0; y = 0;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) || resources == null)
                return false;
            x = checked((int)resources->r_KeepTilePositionX);
            y = checked((int)resources->r_KeepTilePositionY);
            return GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y);
        }

        private static bool IsLiving(BuildingSnapshot building) =>
            building.Alive == AliveState.IsAlive || building.Alive == AliveState.NeedsInit;

        private void ResetMap(string reason)
        {
            if (players.Count != 0)
                foreach (int playerId in players.Keys.ToArray()) FinalizePlayer(playerId, "map-transition");
            FinalizeUnattributed("map-transition");
            players.Clear(); mapSequence++; activeExecute = null; activeSelectionPlayerId = 0;
            activeEconomyContexts?.Clear(); lastRoutingSnapshot = null; lastPclTopology = null;
            earlyOwnerEvents.Clear(); earlyOwnerInventories.Clear(); pendingRawInventories.Clear(); pendingDamage.Clear(); unattributedCounters.Clear();
            preplacedBuildings.Clear(); preplacedByOwnerAndType.Clear(); lastRawBuildings.Clear(); lastCrushedCounters.Clear();
            lastObservedPhase = "map-reset"; activeAccessibilityPlayerId = 0; mapActive = false;
            aiOwnershipResolved = false;
            initializationTracingActive = false;
            currentMapIsSave = false;
            loadSaveEventObserved = false;
            loadingEditorMap = false;
            lastLegacyCopyMapVersion = -1;
            lastLegacyCopySourceBefore = null;
            lastLegacyCopyDestinationBefore = null;
            lastLegacyCopySource = null;
            lastLegacyCopyDestination = null;
            legacyCopyBuildings.Clear();
            crushedActivationBuildings.Clear();
            activeAccessibilityCalls = null;
            emittedGridUpdateSignatures.Clear();
            emittedDominantPclSignatures.Clear();
            emittedInvalidPclAccesses.Clear();
            emittedShadowSearchSignatures.Clear();
            emittedInitializationCheckpoints.Clear();
            wallBaselines.Clear();
            lock (crushedWriterSync) pendingCrushedWriterSignals.Clear();
            lastAivState = 0;
            nextUnattributedFlushUtc = DateTime.UtcNow.AddSeconds(1);
            nextRawDeltaUtc = DateTime.UtcNow.AddSeconds(1);
            nextRoutingPollUtc = DateTime.UtcNow.AddSeconds(1);
            unattributedFinalized = false;
            Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_SESSION_RESET: reason={reason}, sequence={mapSequence}.");
        }

        private PlayerSession Session(int playerId)
        {
            if (!IsAi(playerId))
                throw new InvalidOperationException("AI session requested for unresolved player " + playerId + ".");
            if (!players.TryGetValue(playerId, out PlayerSession session))
            {
                session = new PlayerSession(playerId); players.Add(playerId, session);
                Immediate(playerId, "AI_OBSERVATION_STARTED");
            }
            return session;
        }

        private bool TryGetAiSession(int playerId, string source, out PlayerSession session)
        {
            if (!IsAi(playerId))
            {
                session = null;
                if (IsValidOwner(playerId) && !aiOwnershipResolved)
                    earlyOwnerEvents.Add(playerId, "native." + source);
                else
                    RecordUnattributed(source + " player=" + playerId);
                return false;
            }
            session = Session(playerId);
            return true;
        }

        private void FlushIfDue(int playerId, ulong state, SchedulerGateState gate)
        {
            PlayerSession session = Session(playerId); DateTime now = DateTime.UtcNow;
            if (now < session.NextFlushUtc) return;
            session.NextFlushUtc = now.AddSeconds(1);
            int pauseIndex = ReadPlayerGlobal(playerId, PauseIndexRelativeOffset);
            string pauseThreshold = pauseIndex >= 0 && pauseIndex < PauseTableEntryCount
                ? ReadPlayerInt16(playerId, PauseTableRelativeOffset, pauseIndex).ToString()
                : "unresolved-invalid-index";
            string stateText = $"state activeAiv={gate.ActiveAivSlot}, activeAic={ReadPlayerGlobal(playerId, ActiveAicRelativeOffset)}, crushed={gate.CrushedCounter}/{gate.CrushedDelay}, gold={gate.Gold}, build={gate.BuildCounter}/{gate.BuildRate}, pause={gate.PauseCounter}, pauseIndex={pauseIndex}, pauseThreshold={pauseThreshold}, pauseConfigured={ReadPlayerGlobal(playerId, PauseConfiguredRelativeOffset)}, economyPhase={ReadPlayerGlobal(playerId, EconomyPhaseRelativeOffset)}, goal={gate.CurrentStepGoal}, highest={gate.HighestPreparedFrame}";
            KeyValuePair<string, long>[] interval = session.Counters.DrainInterval();
            if (interval.Length != 0) EmitCounters(playerId, "INTERVAL", interval, stateText);
            if (!session.StartSummaryEmitted && session.FirstBuilding.FollowUpComplete(now))
            {
                session.StartSummaryEmitted = true;
                EmitCounters(playerId, "AIV_START_TOTAL", session.Counters.SnapshotTotal(),
                    "reason=first-building-follow-up-complete; observationContinues=true");
            }
        }

        private void FinalizePlayer(int playerId, string reason)
        {
            if (!players.TryGetValue(playerId, out PlayerSession session) || session.Finalized) return;
            session.Finalized = true;
            KeyValuePair<string, long>[] remaining = session.Counters.DrainInterval();
            if (remaining.Length != 0) EmitCounters(playerId, "FINAL_PENDING", remaining, "reason=" + reason);
            EmitCounters(playerId, "FINAL_TOTAL", session.Counters.SnapshotTotal(), "reason=" + reason);
            session.Counters.Stop();
        }

        private void EmitCounters(int playerId, string label, IEnumerable<KeyValuePair<string, long>> counters, string prefix)
        {
            string payload = prefix + "; " + string.Join("; ", counters.Select(p => p.Key + "=" + p.Value));
            EmitChunked($"PREPLACED_{label}: player={playerId}; ", payload);
        }

        private void EmitBuildingInventory(int playerId, string label, IList<BuildingSnapshot> buildings, Func<BuildingSnapshot, bool> inArea)
        {
            IEnumerable<string> items = buildings.Select(b => b.ToText(inArea == null ? (bool?)null : inArea(b)));
            EmitChunked($"PREPLACED_{label}: player={playerId}; count={buildings.Count}; ", string.Join("; ", items));
        }

        private void EmitChunked(string prefix, string payload)
        {
            if (payload.Length == 0) { Shared.DebugLogHelper.LogInfo(log, prefix + "<empty>"); return; }
            string[] chunks = DiagnosticChunker.Split(payload, LogPayloadLength);
            for (int index = 0; index < chunks.Length; index++)
            {
                Shared.DebugLogHelper.LogInfo(log, $"{prefix}part={index + 1}/{chunks.Length}; {chunks[index]}");
            }
        }

        private void Immediate(int playerId, string text) => Shared.DebugLogHelper.LogInfo(log, $"PREPLACED_EVENT: player={playerId}; {text}");

        private SchedulerGateState ReadSchedulerState(ulong state, int playerId)
        {
            int active = ReadPlayerGlobal(playerId, 0), crushed = ReadPlayerGlobal(playerId, CrushedCounterRelativeOffset);
            int crushedDelay = -1;
            try
            {
                int activeAic = ReadPlayerGlobal(playerId, ActiveAicRelativeOffset);
                var aics = GameAIManagerAPI.Instance.GetAICArray();
                if (AicSlotIndexResolver.TryResolve(activeAic, aics.Length, out int aicIndex))
                    crushedDelay = aics.GetValue(aicIndex).crushed_building_delay;
                else
                    crushedDelay = -1;
            }
            catch { }
            int gold = 0;
            try { gold = GamePlayerManagerAPI.Instance.GetPlayerGold(playerId); } catch { }
            if (active <= 0 || active > MaxAivSpecIndex)
                return new SchedulerGateState(active, crushed, crushedDelay, gold, 0, 0,
                    ReadPlayerGlobal(playerId, PauseCounterRelativeOffset), 0, 0);
            byte* spec = (byte*)state + active * AivSpecStride;
            return new SchedulerGateState(active, crushed, crushedDelay, gold,
                *(int*)(spec + BuildCounterOffset), *(int*)(spec + BuildRateOffset),
                ReadPlayerGlobal(playerId, PauseCounterRelativeOffset), *(int*)(spec + CurrentStepGoalOffset),
                *(int*)(spec + HighestPreparedFrameOffset));
        }

        private FrameSnapshot ReadFrame(ulong state, int playerId, int frame)
        {
            int active = ReadPlayerGlobal(playerId, 0);
            if (active <= 0 || active > MaxAivSpecIndex || frame < 0 || frame >= PreparedLayoutFrameCount)
                return new FrameSnapshot(frame, -1, 0, 0, 0, "invalid");
            byte* entry = (byte*)state + PreparedEntryBaseOffset + ((active * PreparedLayoutFrameCount + frame) * PreparedEntrySize);
            int firstPosition = *(int*)(entry + 8);
            return new FrameSnapshot(frame, *entry, *(short*)(entry + 2), *(short*)(entry + 4), firstPosition,
                $"firstPositionIndex={firstPosition}");
        }

        private int ReadPlayerGlobal(int playerId, int relativeOffset)
        {
            if (activeLayoutIndexBase == 0 || playerId < 0 || playerId > MaxPlayablePlayerId) return 0;
            return *(int*)(activeLayoutIndexBase + (ulong)(playerId * PlayerRuntimeStateStride + relativeOffset));
        }

        private short ReadPlayerInt16(int playerId, int relativeOffset, int elementIndex)
        {
            if (activeLayoutIndexBase == 0 || playerId < 0 || playerId > MaxPlayablePlayerId || elementIndex < 0)
                return 0;
            return *(short*)(activeLayoutIndexBase + (ulong)(playerId * PlayerRuntimeStateStride + relativeOffset + elementIndex * sizeof(short)));
        }

        private static int ReadSpec(ulong state, int spec, int offset) =>
            state == 0 || spec < 0 || spec > MaxAivSpecIndex ? 0 : *(int*)((byte*)state + spec * AivSpecStride + offset);

        private static int SafePlayerFromSpec(ulong state, int spec) => ReadSpec(state, spec, PlayerIdOffset);

        private static string DescribeSpec(ulong state, int spec) =>
            $"spec={spec}, player={ReadSpec(state, spec, PlayerIdOffset)}, candidate={ReadSpec(state, spec, CandidateIdOffset)}, orientation={ReadSpec(state, spec, OrientationOffset)}, placementState={ReadSpec(state, spec, PlacementStateOffset)}, origin=({ReadSpec(state, spec, OriginXOffset)},{ReadSpec(state, spec, OriginYOffset)}), keep=({ReadSpec(state, spec, KeepXOffset)},{ReadSpec(state, spec, KeepYOffset)}), goal={ReadSpec(state, spec, CurrentStepGoalOffset)}, highest={ReadSpec(state, spec, HighestPreparedFrameOffset)}";

        private void ObservePhase(string phase, bool captureRawBuildings)
        {
            lastObservedPhase = phase;
            ObserveCrushedCounters(phase);
            if (captureRawBuildings) EmitRawBuildingInventory(phase.ToUpperInvariant().Replace('-', '_').Replace('.', '_'));
        }

        private void MarkPhase(string phase) => lastObservedPhase = phase;

        private void ObserveCrushedCounters(string source)
        {
            for (int playerId = 1; playerId <= MaxPlayablePlayerId; playerId++)
            {
                int current = ReadPlayerGlobal(playerId, CrushedCounterRelativeOffset);
                if (!lastCrushedCounters.TryGetValue(playerId, out int previous))
                {
                    lastCrushedCounters[playerId] = current;
                    if (current != 0)
                    {
                        Shared.DebugLogHelper.LogWarning(log,
                            $"PREPLACED_CRUSHED_FIRST_OBSERVATION: player={playerId}; value={current}; phase={lastObservedPhase}; source={source}.");
                        EmitRawBuildingInventory("FIRST_ACTIVE_CRUSHED_DELAY_RAW");
                    }
                    continue;
                }
                if (current == previous) continue;
                lastCrushedCounters[playerId] = current;
                Shared.DebugLogHelper.LogInfo(log,
                    $"PREPLACED_CRUSHED_TRANSITION: player={playerId}; {previous}->{current}; phase={lastObservedPhase}; source={source}.");
                if (previous == 0 && current != 0)
                {
                    crushedActivationBuildings[playerId] = CaptureRawBuildings();
                    EmitRawBuildingInventory("CRUSHED_ACTIVATION_RAW");
                }
            }
        }

        private void EmitRawBuildingInventory(string label)
        {
            Span<GameBuilding> span = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            List<BuildingSnapshot> current = CaptureRawBuildings();
            Dictionary<int, BuildingSnapshot> currentById = current.ToDictionary(b => b.Id);
            string payload = $"allocatedSlots={span.Length}; nonEmpty={current.Count}; empty={span.Length - current.Count}; " +
                string.Join("; ", current.Select(DescribeRawBuilding));
            EmitChunked($"PREPLACED_RAW_BUILDINGS: sequence={mapSequence}; label={label}; ", payload);
            bool needsLaterAreaClassification = !aiOwnershipResolved || current.Any(building =>
                IsAi(building.OwnerId) &&
                (!players.TryGetValue(building.OwnerId, out PlayerSession session) || !session.HasAivArea));
            if (needsLaterAreaClassification)
                pendingRawInventories.Add(new InventoryRecord("RAW_" + label, current));
            if (lastRawBuildings.Count != 0)
            {
                string[] changes = DescribeBuildingChanges(lastRawBuildings, currentById).ToArray();
                if (changes.Length != 0)
                    EmitChunked($"PREPLACED_RAW_DELTA: sequence={mapSequence}; label={label}; count={changes.Length}; ", string.Join("; ", changes));
            }
            lastRawBuildings.Clear();
            foreach (KeyValuePair<int, BuildingSnapshot> pair in currentById) lastRawBuildings.Add(pair.Key, pair.Value);
        }

        private void ObserveRawBuildingDeltasIfDue()
        {
            DateTime now = DateTime.UtcNow;
            if (now < nextRawDeltaUtc) return;
            nextRawDeltaUtc = now.AddSeconds(1);
            List<BuildingSnapshot> current = CaptureRawBuildings();
            Dictionary<int, BuildingSnapshot> currentById = current.ToDictionary(building => building.Id);
            string[] changes = DescribeBuildingChanges(lastRawBuildings, currentById).ToArray();
            if (changes.Length != 0)
                EmitChunked($"PREPLACED_RAW_DELTA: sequence={mapSequence}; label=PERIODIC; count={changes.Length}; ",
                    string.Join("; ", changes));
            lastRawBuildings.Clear();
            foreach (KeyValuePair<int, BuildingSnapshot> pair in currentById)
                lastRawBuildings.Add(pair.Key, pair.Value);
        }

        private List<BuildingSnapshot> CaptureRawBuildings()
        {
            List<BuildingSnapshot> result = new List<BuildingSnapshot>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (HasAnyNonZeroByte(ref building))
                    result.Add(Snapshot(spanIndex + 1, ref building));
            }
            return result;
        }

        private static bool HasAnyNonZeroByte(ref GameBuilding building)
        {
            fixed (GameBuilding* buildingPointer = &building)
            {
                byte* bytes = (byte*)buildingPointer;
                for (int offset = 0; offset < sizeof(GameBuilding); offset++)
                    if (bytes[offset] != 0) return true;
            }
            return false;
        }

        private void CapturePreplacedBaseline()
        {
            preplacedBuildings.Clear();
            preplacedByOwnerAndType.Clear();
            List<BuildingSnapshot> rawBuildings = CaptureRawBuildings();
            foreach (BuildingSnapshot building in rawBuildings)
            {
                // A zero Global-ID cannot distinguish later reuse of the same native slot.
                if (building.GlobalId == 0) continue;
                preplacedBuildings[building.Id] = building.Identity;
                long key = GetOwnerTypeKey(building.OwnerId, building.Type);
                if (!preplacedByOwnerAndType.TryGetValue(key, out List<PreplacedIdentity> identities))
                {
                    identities = new List<PreplacedIdentity>();
                    preplacedByOwnerAndType.Add(key, identities);
                }
                identities.Add(building.Identity);
            }
            string unstable = string.Join("; ", rawBuildings.Where(building => building.GlobalId == 0)
                .Select(building => building.ToText(TryGetArea(building.OwnerId, building))));
            EmitChunked($"PREPLACED_BASELINE: sequence={mapSequence}; stable={preplacedBuildings.Count}; unstableWithoutGlobalId={rawBuildings.Count - preplacedBuildings.Count}; ",
                string.Join("; ", preplacedBuildings.Values.Select(p =>
                    $"id={p.BuildingId},global={p.GlobalId},owner={p.OwnerId},type={(eStructs)p.StructureType}")) +
                    (unstable.Length == 0 ? string.Empty : "; unstableRecords=" + unstable));
        }

        private static IEnumerable<string> DescribeBuildingChanges(
            IReadOnlyDictionary<int, BuildingSnapshot> before,
            IReadOnlyDictionary<int, BuildingSnapshot> after)
        {
            foreach (int id in before.Keys.Union(after.Keys).OrderBy(id => id))
            {
                bool hadBefore = before.TryGetValue(id, out BuildingSnapshot oldValue);
                bool hasAfter = after.TryGetValue(id, out BuildingSnapshot newValue);
                if (!hadBefore) yield return "added:" + newValue.ToText(null);
                else if (!hasAfter) yield return "removed:" + oldValue.ToText(null);
                else if (!oldValue.DataEquals(newValue))
                    yield return "changed:before{" + oldValue.ToText(null) + "},after{" + newValue.ToText(null) + "}";
            }
        }

        private int CountMatchingPreplaced(int playerId, eStructs structureType, int mode)
        {
            int count = 0;
            if (!preplacedByOwnerAndType.TryGetValue(GetOwnerTypeKey(playerId, structureType),
                    out List<PreplacedIdentity> identities))
                return 0;
            foreach (PreplacedIdentity identity in identities)
            {
                if (identity.OwnerId != playerId || identity.StructureType != (int)structureType ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(identity.BuildingId, out GameBuilding* building) ||
                    building == null || building->r_AliveState != AliveState.IsAlive ||
                    !identity.Matches(identity.BuildingId, building->r_GlobalId, building->r_PlayerIdOwner, (int)building->r_BuildingType))
                    continue;
                if (mode != 0 && *(short*)((byte*)building + BuildingCountModeFieldOffset) != 0) continue;
                count++;
            }
            return count;
        }

        private static long GetOwnerTypeKey(int ownerId, eStructs structureType) =>
            ((long)(uint)ownerId << 32) | (uint)(int)structureType;

        private bool IsCurrentPreplaced(int buildingId)
        {
            return preplacedBuildings.TryGetValue(buildingId, out PreplacedIdentity identity) &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) && building != null &&
                identity.Matches(buildingId, building->r_GlobalId, building->r_PlayerIdOwner, (int)building->r_BuildingType);
        }

        private ReachabilitySnapshot CaptureReachability(int playerId, int x, int y)
        {
            int keepPcl = TryGetKeepPcl(playerId, out int capturedKeep) ? capturedKeep : 0;
            int targetPcl = TryGetPclAt(x, y, out int capturedTarget) ? capturedTarget : 0;
            List<PortalConnection> portals = CapturePortalConnections(out string details);
            PortalRouteResult route = PortalRouteModel.EvaluateEconomyModeZero(keepPcl, targetPcl, portals);
            return new ReachabilitySnapshot(keepPcl, targetPcl, route, portals, details);
        }

        private List<PortalConnection> CapturePortalConnections(out string details)
        {
            var result = new List<PortalConnection>();
            if (nativePathManagerBase == 0)
            {
                details = "path-manager-unavailable";
                return result;
            }
            int* pathManager = (int*)nativePathManagerBase;
            int count = pathManager[0];
            if (count < 1 || count > MaximumPortalRecordCount)
            {
                details = "portal-count-out-of-range:" + count;
                return result;
            }
            var rows = new List<string>();
            for (int portalId = 1; portalId < count; portalId++)
            {
                int offset = portalId * PortalRecordStrideDwords;
                int state = pathManager[offset + PortalStateOffsetDwords];
                int active = pathManager[offset + PortalActiveOffsetDwords];
                int kind = pathManager[offset + PortalKindOffsetDwords];
                if (state != NativePortalLiveState || active == 0) continue;
                int buildingId = pathManager[offset + PortalBuildingIdOffsetDwords];
                int rawOwnerValue = pathManager[offset + PortalOwnerOffsetDwords];
                int first = pathManager[offset + PortalFirstPclOffsetDwords];
                int second = pathManager[offset + PortalSecondPclOffsetDwords];
                int third = pathManager[offset + PortalThirdPclOffsetDwords];
                bool validBuilding = TryCaptureBuilding(buildingId, out BuildingSnapshot portalBuilding) &&
                    portalBuilding.Alive == AliveState.IsAlive && portalBuilding.GlobalId != 0 &&
                    IsPortalStructure(portalBuilding.Type);
                bool validPcls = IsValidPcl(first) && IsValidPcl(second) && (third == 0 || IsValidPcl(third));
                // C3BF0 calls E2610 with route mode 0. That mode excludes native portal kind 1,
                // while other callers (including unit routing) can select a different set.
                bool eligibleForEconomyModeZero = kind != NativePortalExcludedKindForEconomyModeZero;
                int actualOwnerId = validBuilding ? portalBuilding.OwnerId : 0;
                if (validBuilding && validPcls)
                    result.Add(new PortalConnection(portalId, first, second, third, rawOwnerValue, buildingId,
                        actualOwnerId, eligibleForEconomyModeZero));
                rows.Add($"portal={portalId},building={buildingId},rawOwnerValue={rawOwnerValue},pcl=({first},{second},{third}),state={state},active={active},kind={kind},economyMode0Eligible={eligibleForEconomyModeZero},validBuilding={validBuilding},validPcls={validPcls},buildingDetails={TryDescribePortalBuilding(buildingId)}");
            }
            details = "nativeCount=" + count + "; " + string.Join("; ", rows);
            return result;
        }

        private string TryDescribePortalBuilding(int buildingId)
        {
            if (!TryCaptureBuilding(buildingId, out BuildingSnapshot building)) return "false";
            return $"true/type={building.Type}/global={building.GlobalId}/actualOwner={building.OwnerId}/alive={building.Alive}/gatehouseId={building.GatehouseId}/preplaced={IsCurrentPreplaced(buildingId)}";
        }

        private void EmitPortalTopology(int playerId, string label, ReachabilitySnapshot snapshot)
        {
            if (!IsAi(playerId)) return;
            string gatehouseDetails = CaptureGatehouseEntries();
            string signature = snapshot.KeepPcl + "/" + snapshot.TargetPcl + "/" + snapshot.Route.Kind + "/" + snapshot.Details + "/" + gatehouseDetails;
            PlayerSession session = Session(playerId);
            if (!session.EmittedPortalSignatures.Add(signature)) return;
            session.Counters.Add($"portal-topology transition={label} route={snapshot.Route.Kind}");
            EmitChunked($"PREPLACED_PORTAL_TOPOLOGY: player={playerId}; label={label}; ",
                $"keepPcl={snapshot.KeepPcl}; targetPcl={snapshot.TargetPcl}; route={snapshot.Route.Kind}; usedPortalIds=[{string.Join(",", snapshot.Route.UsedPortalIds)}]; {snapshot.Details}; gatehouseEntries={gatehouseDetails}");
        }

        private void ObservePortalTopology(string label)
        {
            foreach (PlayerSession session in players.Values.ToArray())
                if (!session.Finalized)
                    EmitPortalTopology(session.PlayerId, label, CaptureReachability(session.PlayerId, -1, -1));
        }

        private string CaptureGatehouseEntries()
        {
            var array = GamePathingManagerAPI.Instance.GetPathConnectionArray();
            var rows = new List<string>();
            var linkedBuildings = new HashSet<int>();
            for (int index = 0; index < array.Length; index++)
            {
                PathConnectionRecord* entry = array.GetValuePointer(index);
                if (entry == null || entry->r_IsActive == 0) continue;
                bool idInRange = entry->r_BuildingId > 0;
                int buildingId = idInRange ? entry->r_BuildingId : 0;
                BuildingSnapshot building = default;
                bool resolved = idInRange && TryCaptureBuilding(buildingId, out building);
                bool identityMatches = resolved && entry->r_SubjectGlobalId != 0 && building.GlobalId == entry->r_SubjectGlobalId &&
                    IsPortalStructure(building.Type) &&
                    (building.Alive == AliveState.IsAlive || building.Alive == AliveState.NeedsInit);
                if (identityMatches) linkedBuildings.Add(buildingId);
                string buildingDetails = resolved ? building.ToText(TryGetArea(building.OwnerId, building)) : "unresolved";
                string entryDiagnostic = DescribeGateEndpoint(entry->r_EntryTilePositionX, entry->r_EntryTilePositionY, entry->r_EntryTileId);
                string exitDiagnostic = DescribeGateEndpoint(entry->r_ExitTilePositionX, entry->r_ExitTilePositionY, entry->r_ExitTileId);
                rows.Add($"index={index},recordGlobal={entry->r_RecordGlobalId},building={entry->r_BuildingId},subjectGlobal={entry->r_SubjectGlobalId},enabledOrOpen={entry->r_IsEnabledOrOpen},entry={entryDiagnostic},exit={exitDiagnostic},identityMatches={identityMatches},buildingDetails={buildingDetails}");
            }
            string[] missing = CaptureRawBuildings().Where(b => IsPortalStructure(b.Type) &&
                (b.Alive == AliveState.IsAlive || b.Alive == AliveState.NeedsInit) && !linkedBuildings.Contains(b.Id))
                .Select(b => $"id={b.Id}/global={b.GlobalId}/owner={b.OwnerId}/type={b.Type}/alive={b.Alive}/rawGatehouseId={b.GatehouseId}").ToArray();
            return "count=" + rows.Count + "[" + string.Join("; ", rows) + "]; missingForPortalBuildings=" +
                missing.Length + "[" + string.Join("; ", missing) + "]";
        }

        private string DescribeGateEndpoint(int rawX, int rawY, int tileId)
        {
            int x = rawX;
            int y = rawY;
            int pcl = TryGetPclByTileId(tileId, out int value) ? value : 0;
            int coarseX = x / EconomyCoarseCellTileSize;
            int coarseY = y / EconomyCoarseCellTileSize;
            var neighborhood = new List<string>();
            if (lastAivState != 0)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int currentX = coarseX + dx;
                        int currentY = coarseY + dy;
                        if ((uint)currentX >= EconomyGridWidth || (uint)currentY >= EconomyGridWidth) continue;
                        int index = currentX * EconomyGridWidth + currentY;
                        string raw = StableRoutingCellPayload((byte*)lastAivState + EconomyGridBaseOffset + index * EconomyGridCellStride);
                        neighborhood.Add($"({currentX},{currentY})/{raw}/pcls=[{string.Join(",", GetEconomyCellPcls(currentX, currentY))}]");
                    }
                }
            }
            return $"({x},{y})/{tileId}/coarse=({coarseX},{coarseY})/pcl={pcl}/neighbors=[{string.Join(";", neighborhood)}]";
        }

        private bool TryGetKeepPcl(int playerId, out int pcl)
        {
            pcl = 0;
            return GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) &&
                resources != null && TryGetPclByTileId(checked((int)resources->r_KeepTileId), out pcl, "keep");
        }

        private bool TryGetPclAt(int x, int y, out int pcl)
        {
            pcl = 0;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            return tiles.IsTileInsideMapBounds(x, y) &&
                TryGetPclByTileId(tiles.GetTileId(x, y), out pcl, "coordinate");
        }

        private bool TryGetPclByTileId(int tileId, out int pcl, string source = "tile-id")
        {
            pcl = 0;
            if (nativePclGrid == null) return false;
            if ((uint)tileId >= NativePclEntryCount)
            {
                RecordInvalidPclAccess(tileId, source);
                return false;
            }
            pcl = nativePclGrid[tileId];
            return pcl > 0;
        }

        private void RecordInvalidPclAccess(int tileId, string source)
        {
            string signature = source + "/" + tileId;
            unattributedCounters.Add("invalid-pcl-tile-access source=" + source + " tileId=" + tileId);
            if (emittedInvalidPclAccesses.Add(signature))
                Shared.DebugLogHelper.LogWarning(log,
                    $"PREPLACED_INVALID_PCL_TILE_ACCESS: source={source}; tileId={tileId}; valid=0..{NativePclEntryCount - 1}.");
        }

        private static bool IsValidPcl(int pcl) => pcl > 0 && pcl <= ushort.MaxValue;

        private bool TryIsPointInsideAiv(int playerId, int x, int y, out bool inside)
        {
            inside = false;
            if (!players.TryGetValue(playerId, out PlayerSession session) || !session.HasAivArea) return false;
            inside = AivAreaClassifier.Intersects(session.AivOriginX, session.AivOriginY, AivGridSize, x, y, x, y);
            return true;
        }

        private bool? TryGetArea(int playerId, BuildingSnapshot building) =>
            players.TryGetValue(playerId, out PlayerSession session) && session.HasAivArea
                ? (bool?)session.IsInsideAivArea(building)
                : null;

        private string DescribeRawBuilding(BuildingSnapshot building)
        {
            string ownerKind = !IsValidOwner(building.OwnerId) ? "unassigned-or-special" :
                IsAi(building.OwnerId) ? "ai" : "non-ai";
            return building.ToText(TryGetArea(building.OwnerId, building)) + ",ownerKind=" + ownerKind;
        }

        private List<BuildingSnapshot> CaptureBuildings(int playerId)
        {
            List<BuildingSnapshot> result = new List<BuildingSnapshot>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_PlayerIdOwner == playerId && HasAnyNonZeroByte(ref building))
                    result.Add(Snapshot(spanIndex + 1, ref building));
            }
            return result;
        }

        private bool TryCaptureBuilding(int buildingId, out BuildingSnapshot snapshot)
        {
            snapshot = default;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building)) return false;
            snapshot = Snapshot(buildingId, ref *building);
            return true;
        }

        private static BuildingSnapshot Snapshot(int buildingId, ref GameBuilding building) =>
            new BuildingSnapshot(buildingId, building.r_GlobalId, building.r_PlayerIdOwner, building.r_BuildingType,
                building.r_AliveState, building.r_TilePositionXBegin, building.r_TilePositionYBegin,
                building.r_TilePositionXEnd, building.r_TilePositionYEnd, building.r_WorldPositionX,
                building.r_WorldPositionY, building.r_CurrentHealth, building.r_MaxHealth,
                building.r_IsSleeping, building.r_GatehouseId);

        private DamageContext CaptureDamageContext(BuildingTileTakeDamageEventArgs args)
        {
            try
            {
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
                if (buildingId > 0 && TryCaptureBuilding(buildingId, out BuildingSnapshot building))
                    return new DamageContext(building, ReadPlayerGlobal(building.OwnerId, CrushedCounterRelativeOffset),
                        IsCurrentPreplaced(buildingId));
            }
            catch (Exception ex)
            {
                RecordUnattributed("damage-context-error type=" + ex.GetType().Name);
            }
            return DamageContext.Unmatched(args);
        }

        private void RecordOwnerEvent(int ownerId, string value)
        {
            if (!IsValidOwner(ownerId))
            {
                RecordUnattributed(value);
                return;
            }
            if (IsAi(ownerId)) Session(ownerId).Counters.Add("event." + value);
            else if (!aiOwnershipResolved) earlyOwnerEvents.Add(ownerId, value);
            else RecordUnattributed("owner=" + ownerId + " " + value);
        }

        private void RecordUnattributed(string key)
        {
            if (unattributedFinalized) return;
            unattributedCounters.Add(key);
            if (DateTime.UtcNow >= nextUnattributedFlushUtc)
            {
                FlushUnattributed("interval");
                nextUnattributedFlushUtc = DateTime.UtcNow.AddSeconds(1);
            }
        }

        private void FlushUnattributed(string reason)
        {
            KeyValuePair<string, long>[] interval = unattributedCounters.DrainInterval();
            if (interval.Length == 0) return;
            string payload = "reason=" + reason + "; " + string.Join("; ", interval.Select(p => p.Key + "=" + p.Value));
            EmitChunked("PREPLACED_UNATTRIBUTED: ", payload);
        }

        private void FinalizeUnattributed(string reason)
        {
            if (unattributedFinalized) return;
            FlushUnattributed(reason);
            KeyValuePair<string, long>[] total = unattributedCounters.SnapshotTotal();
            if (total.Length != 0)
            {
                string payload = "reason=" + reason + "; " + string.Join("; ", total.Select(p => p.Key + "=" + p.Value));
                EmitChunked("PREPLACED_UNATTRIBUTED_TOTAL: ", payload);
            }
            unattributedFinalized = true;
            unattributedCounters.Stop();
        }

        private void CaptureOwnerInventory(int ownerId, string label)
        {
            if (!IsValidOwner(ownerId)) return;
            if (IsAi(ownerId))
            {
                CaptureAndEmitInventory(Session(ownerId), label);
                return;
            }
            List<BuildingSnapshot> buildings = CaptureBuildings(ownerId);
            EmitBuildingInventory(ownerId, label, buildings, null);
            if (!earlyOwnerInventories.TryGetValue(ownerId, out List<InventoryRecord> inventories))
            {
                inventories = new List<InventoryRecord>();
                earlyOwnerInventories.Add(ownerId, inventories);
            }
            inventories.Add(new InventoryRecord(label, buildings));
        }

        private List<BuildingSnapshot> CaptureAndEmitInventory(PlayerSession session, string label)
        {
            List<BuildingSnapshot> buildings = CaptureBuildings(session.PlayerId);
            if (session.HasAivArea)
                EmitBuildingInventory(session.PlayerId, label, buildings, session.IsInsideAivArea);
            else
            {
                EmitBuildingInventory(session.PlayerId, label, buildings, null);
                session.PendingInventories.Add(new InventoryRecord(label, buildings));
            }
            return buildings;
        }

        private void AdoptEarlyInventories(PlayerSession session)
        {
            if (!earlyOwnerInventories.TryGetValue(session.PlayerId, out List<InventoryRecord> inventories)) return;
            session.PendingInventories.AddRange(inventories);
            earlyOwnerInventories.Remove(session.PlayerId);
            ReclassifyPendingInventories(session);
        }

        private void SetAivArea(PlayerSession session, int originX, int originY)
        {
            session.AivOriginX = originX;
            session.AivOriginY = originY;
            session.HasAivArea = true;
            ReclassifyPendingInventories(session);
            ReclassifyPendingRawInventories(session);
        }

        private void ReclassifyPendingRawInventories(PlayerSession session)
        {
            foreach (InventoryRecord inventory in pendingRawInventories)
            {
                if (!inventory.ReclassifiedPlayers.Add(session.PlayerId)) continue;
                List<BuildingSnapshot> owned = inventory.Buildings
                    .Where(building => building.OwnerId == session.PlayerId).ToList();
                if (owned.Count != 0)
                    EmitBuildingInventory(session.PlayerId, inventory.Label + "_RECLASSIFIED", owned,
                        session.IsInsideAivArea);
            }
        }

        private void ReclassifyPendingInventories(PlayerSession session)
        {
            if (!session.HasAivArea || session.PendingInventories.Count == 0) return;
            foreach (InventoryRecord inventory in session.PendingInventories)
                EmitBuildingInventory(session.PlayerId, inventory.Label + "_RECLASSIFIED", inventory.Buildings, session.IsInsideAivArea);
            session.PendingInventories.Clear();
        }

        private bool IsAi(int playerId)
        {
            try { return playerId >= 1 && playerId <= MaxPlayablePlayerId && GamePlayerManagerAPI.Instance.IsAIPlayer(playerId); }
            catch { return false; }
        }

        private static bool IsValidOwner(int playerId) => playerId >= 1 && playerId <= MaxPlayablePlayerId;

        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "PREPLACED_CALLBACK_ERROR: Vanilla result was preserved. " + ex); }
        }

        private readonly struct NativeDefinition
        {
            public NativeDefinition(string name, string pattern, int rva) { Name = name; Pattern = pattern; Rva = rva; }
            public string Name { get; } public string Pattern { get; } public int Rva { get; }
        }

        private readonly struct CrushedWriterSignal
        {
            public CrushedWriterSignal(DateTime utc, int mapSequence, string phase, int timerOwnerId,
                int buildingId, int aliveStateValue, int structureType, int buildingOwnerId, uint globalId,
                int healthAfterDamage, int maxHealth, int tileX, int tileY, int endX, int endY,
                int damage, int unknown1, int sourcePlayerId, int activationMode, int unknown4)
            {
                Utc = utc; MapSequence = mapSequence; Phase = phase ?? "unknown";
                TimerOwnerId = timerOwnerId; BuildingId = buildingId; AliveStateValue = aliveStateValue;
                StructureType = structureType;
                BuildingOwnerId = buildingOwnerId; GlobalId = globalId;
                HealthAfterDamage = healthAfterDamage; MaxHealth = maxHealth;
                TileX = tileX; TileY = tileY; EndX = endX; EndY = endY;
                Damage = damage; Unknown1 = unknown1; SourcePlayerId = sourcePlayerId;
                ActivationMode = activationMode; Unknown4 = unknown4;
            }

            public DateTime Utc { get; }
            public int MapSequence { get; }
            public string Phase { get; }
            public int TimerOwnerId { get; }
            public int BuildingId { get; }
            public int AliveStateValue { get; }
            public int StructureType { get; }
            public int BuildingOwnerId { get; }
            public uint GlobalId { get; }
            public int HealthAfterDamage { get; }
            public int MaxHealth { get; }
            public int TileX { get; }
            public int TileY { get; }
            public int EndX { get; }
            public int EndY { get; }
            public int Damage { get; }
            public int Unknown1 { get; }
            public int SourcePlayerId { get; }
            public int ActivationMode { get; }
            public int Unknown4 { get; }
        }

        private sealed class PlayerSession
        {
            public PlayerSession(int playerId) { PlayerId = playerId; NextFlushUtc = DateTime.UtcNow.AddSeconds(1); FirstBuilding = new FirstBuildingWindow(TimeSpan.FromSeconds(10)); }
            public int PlayerId { get; } public DiagnosticCounterSet Counters { get; } = new DiagnosticCounterSet();
            public FirstBuildingWindow FirstBuilding { get; } public DateTime NextFlushUtc { get; set; }
            public int NestedExecuteCalls { get; set; } public int AlternativeCalls { get; set; } public bool Finalized { get; set; }
            public bool StartSummaryEmitted { get; set; }
            public bool FirstSchedulerSnapshotEmitted { get; set; } public bool FirstActiveDelaySnapshotEmitted { get; set; }
            public bool FirstEconomyRoutingSnapshotEmitted { get; set; }
            public bool FirstPossibleBreachObserved { get; set; }
            public WallTestRole WallTestRole { get; set; }
            public bool ConfirmedWallBreach { get; set; }
            public DateTime WallBreachUtc { get; set; }
            public bool WallLossStageEmitted { get; set; }
            public DateTime WallLossUtc { get; set; }
            public bool AnchorConnectionStageEmitted { get; set; }
            public bool ShadowExpansionStageEmitted { get; set; }
            public Dictionary<string, ShadowSearchSummary> ShadowBaselines { get; } =
                new Dictionary<string, ShadowSearchSummary>(StringComparer.Ordinal);
            public EconomyBarrierObservation LastEconomyBarrier { get; set; }
            public List<EconomyBarrierObservation> EconomyBarriers { get; } = new List<EconomyBarrierObservation>();
            public bool HasAivArea { get; set; } public int AivOriginX { get; set; } public int AivOriginY { get; set; }
            public List<InventoryRecord> PendingInventories { get; } = new List<InventoryRecord>();
            public HashSet<string> EmittedPortalSignatures { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> EmittedSearchSignatures { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> EmittedPostBreachSearchKinds { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> EmittedOracleSignatures { get; } = new HashSet<string>(StringComparer.Ordinal);

            public bool IsInsideAivArea(BuildingSnapshot building) =>
                AivAreaClassifier.Intersects(AivOriginX, AivOriginY, AivGridSize,
                    building.TileX, building.TileY, building.EndX, building.EndY);
        }

        private sealed class ExecuteContext
        {
            public ExecuteContext(int playerId, int frame, int status, Dictionary<int, PreplacedIdentity> beforeIdentities)
            { PlayerId = playerId; Frame = frame; Status = status; BeforeIdentities = beforeIdentities; }
            public int PlayerId { get; } public int Frame { get; } public int Status { get; }
            public Dictionary<int, PreplacedIdentity> BeforeIdentities { get; }
            public List<int> SpawnedBuildingIds { get; } = new List<int>(); public List<long> PlacementResults { get; } = new List<long>();
            public List<BuildingSpawnSignal> SpawnSignals { get; } = new List<BuildingSpawnSignal>();
            public List<int> ValidatorResults { get; } = new List<int>(); public List<int> ResourceResults { get; } = new List<int>();
            public List<int> ReachabilityResults { get; } = new List<int>();
            public List<string> WaitRejectors { get; } = new List<string>();
        }

        private sealed class EconomyContext
        {
            public EconomyContext(int playerId, string phase, eStructs desiredType, ulong state, bool afterConfirmedBreach)
            { PlayerId = playerId; Phase = phase; DesiredType = desiredType; State = state; AfterConfirmedBreach = afterConfirmedBreach; }
            public int PlayerId { get; }
            public string Phase { get; }
            public eStructs DesiredType { get; }
            public ulong State { get; }
            public bool AfterConfirmedBreach { get; }
            public List<string> ResourceResults { get; } = new List<string>();
            public List<string> CountResults { get; } = new List<string>();
            public List<string> ReachabilityResults { get; } = new List<string>();
            public List<string> ConstructionCalls { get; } = new List<string>();
            public List<string> PclCalls { get; } = new List<string>();
            public List<string> SpawnSignals { get; } = new List<string>();
            public List<EconomySearchObservation> Searches { get; } = new List<EconomySearchObservation>();
        }

        private sealed class EconomyBarrierObservation
        {
            public EconomyBarrierObservation(string helper, eStructs? desiredType,
                HashSet<int> visitedPcls, HashSet<int> frontierPcls,
                HashSet<int> visitedTileIds, HashSet<int> frontierTileIds)
            {
                Helper = helper ?? "unknown";
                DesiredTypeText = desiredType.HasValue ? desiredType.Value.ToString() : "unknown";
                VisitedPcls = visitedPcls ?? new HashSet<int>();
                FrontierPcls = frontierPcls ?? new HashSet<int>();
                VisitedTileIds = visitedTileIds ?? new HashSet<int>();
                FrontierTileIds = frontierTileIds ?? new HashSet<int>();
            }

            public string Helper { get; }
            public string DesiredTypeText { get; }
            public HashSet<int> VisitedPcls { get; }
            public HashSet<int> FrontierPcls { get; }
            public HashSet<int> VisitedTileIds { get; }
            public HashSet<int> FrontierTileIds { get; }
        }

        private readonly struct BuildingSpawnSignal
        {
            public BuildingSpawnSignal(eStructs type, int x, int y, long rawReturn)
            { Type = type; X = x; Y = y; RawReturn = rawReturn; }
            public eStructs Type { get; }
            public int X { get; }
            public int Y { get; }
            public long RawReturn { get; }
            public override string ToString() => $"{Type}@({X},{Y})/rawReturn={RawReturn}";
        }

        private readonly struct EconomyGridState
        {
            public EconomyGridState(int generation, int depth, int queueRead, int queueWrite, int resultX, int resultY)
            { Generation = generation; Depth = depth; QueueRead = queueRead; QueueWrite = queueWrite; ResultX = resultX; ResultY = resultY; }
            public int Generation { get; }
            public int Depth { get; }
            public int QueueRead { get; }
            public int QueueWrite { get; }
            public int ResultX { get; }
            public int ResultY { get; }
            public static EconomyGridState Unavailable => new EconomyGridState(-1, -1, -1, -1, -1, -1);
            public override string ToString() => $"generation={Generation}/depth={Depth}/queue={QueueRead}->{QueueWrite}/result=({ResultX},{ResultY})";
        }

        private readonly struct ShadowSearchSummary
        {
            public ShadowSearchSummary(int reachable, int candidates)
            { Reachable = reachable; Candidates = candidates; }
            public int Reachable { get; }
            public int Candidates { get; }
        }

        private readonly struct EconomyCoordinate : IEquatable<EconomyCoordinate>
        {
            public EconomyCoordinate(int x, int y) { X = x; Y = y; }
            public int X { get; }
            public int Y { get; }
            // Vanilla indexes the 160x160 economy grid as x * 160 + y.
            public static EconomyCoordinate FromIndex(int index) =>
                new EconomyCoordinate(index / EconomyGridWidth, index % EconomyGridWidth);
            public bool Equals(EconomyCoordinate other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is EconomyCoordinate other && Equals(other);
            public override int GetHashCode() => unchecked(X * 397 ^ Y);
            public override string ToString() => $"({X},{Y})";
        }

        private sealed class EconomySearchObservation
        {
            public EconomySearchObservation(string helper, string arguments, EconomyGridState before, EconomyGridState after,
                List<EconomyCoordinate> visited, HashSet<int> frontier, ulong signature, bool candidateFound,
                string fullText, string gateReason = "unavailable")
            {
                Helper = helper; Arguments = arguments; Before = before; After = after;
                Visited = visited ?? new List<EconomyCoordinate>(); Frontier = frontier ?? new HashSet<int>();
                Signature = signature; CandidateFound = candidateFound; FullText = fullText ?? string.Empty;
                GateReason = gateReason ?? "unavailable";
            }
            public string Helper { get; }
            public string Arguments { get; }
            public EconomyGridState Before { get; }
            public EconomyGridState After { get; }
            public List<EconomyCoordinate> Visited { get; }
            public HashSet<int> Frontier { get; }
            public ulong Signature { get; }
            public bool CandidateFound { get; }
            public string GateReason { get; }

            public int ResultX => After.ResultX;
            public int ResultY => After.ResultY;
            public bool PerformedTraversal => Before.Generation != After.Generation;
            public string CompactText => $"arguments={Arguments} generation={Before.Generation}->{After.Generation} traversal={PerformedTraversal} gate={GateReason} visited={Visited.Count} frontier={Frontier.Count} result=({ResultX},{ResultY}) candidate={CandidateFound} signature={Signature:X16}";
            public string FullText { get; }
            public static EconomySearchObservation Unavailable(string helper, string arguments, EconomyGridState before, EconomyGridState after) =>
                new EconomySearchObservation(helper, arguments, before, after, null, null, 0, false,
                    "state-unavailable");
        }

        private sealed class EconomyGridBuildSnapshot
        {
            private const int StoredBytesPerCell = EconomyGridCellStride - sizeof(int) - 1;
            private readonly byte[] payloads;

            private EconomyGridBuildSnapshot(int referencePcl, ulong signature, byte[] payloads)
            {
                ReferencePcl = referencePcl;
                Signature = signature;
                this.payloads = payloads;
            }

            public int ReferencePcl { get; }
            public ulong Signature { get; }

            public static EconomyGridBuildSnapshot Capture(byte* state)
            {
                if (state == null)
                    return new EconomyGridBuildSnapshot(0, 0, Array.Empty<byte>());
                var payloads = new byte[EconomyGridCellCount * StoredBytesPerCell];
                ulong signature = 1469598103934665603UL;
                int write = 0;
                for (int cellIndex = 0; cellIndex < EconomyGridCellCount; cellIndex++)
                {
                    byte* cell = state + EconomyGridBaseOffset + cellIndex * EconomyGridCellStride;
                    for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                    {
                        if (offset == 0x05) continue;
                        byte value = cell[offset];
                        payloads[write++] = value;
                        signature = Hash(signature, value);
                    }
                }
                int referencePcl = *(int*)(state + EconomyReferencePclOffset);
                signature = Hash(signature, referencePcl);
                return new EconomyGridBuildSnapshot(referencePcl, signature, payloads);
            }

            public string DescribeDelta(EconomyGridBuildSnapshot previous)
            {
                if (previous == null || previous.payloads.Length != payloads.Length)
                    return "previous-grid-unavailable";
                var groups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
                int changed = 0;
                for (int cellIndex = 0; cellIndex < EconomyGridCellCount; cellIndex++)
                {
                    int start = cellIndex * StoredBytesPerCell;
                    bool differs = false;
                    for (int offset = 0; offset < StoredBytesPerCell; offset++)
                    {
                        if (previous.payloads[start + offset] != payloads[start + offset])
                        {
                            differs = true;
                            break;
                        }
                    }
                    if (!differs) continue;
                    changed++;
                    string transition = DescribePayload(previous.payloads, start) + "->" + DescribePayload(payloads, start);
                    if (!groups.TryGetValue(transition, out List<int> coordinates))
                    {
                        coordinates = new List<int>();
                        groups.Add(transition, coordinates);
                    }
                    coordinates.Add(cellIndex);
                }
                return "changedCellCount=" + changed + "; transitionGroupCount=" + groups.Count +
                    "; transitions=[" + string.Join("; ", groups.Select(pair =>
                        pair.Key + "=>[" + LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]")) + "]";
            }

            private static string DescribePayload(byte[] values, int start)
            {
                var builder = new StringBuilder(StoredBytesPerCell * 2 + 72);
                for (int index = 0; index < StoredBytesPerCell; index++)
                    builder.Append(values[start + index].ToString("X2"));
                // Stored byte index 0 is native cell offset +04; +05 is deliberately omitted.
                int byte04 = values[start];
                int byte0F = values[start + 0x0A];
                int byte11 = values[start + 0x0C];
                int byte12 = values[start + 0x0D];
                int byte13 = values[start + 0x0E];
                int byte16 = values[start + 0x11];
                return $"raw={builder}/byte+04={byte04}/byte+0F={byte0F}/byte+11={byte11}/byte+12={byte12}/byte+13={byte13}/byte+16={byte16}";
            }
        }

        private sealed class EconomyRoutingSnapshot
        {
            private const int StoredRawBytesPerCell = EconomyGridCellStride - sizeof(int) - 1;
            private readonly ulong[] rawSignatures;
            private readonly byte[] rawPayloads;
            private readonly byte[] pclCounts;
            private readonly ushort[] pclValues;

            private EconomyRoutingSnapshot(ulong signature, ulong[] rawSignatures, byte[] rawPayloads,
                byte[] pclCounts, ushort[] pclValues)
            {
                Signature = signature; this.rawSignatures = rawSignatures; this.rawPayloads = rawPayloads;
                this.pclCounts = pclCounts; this.pclValues = pclValues;
            }

            public ulong Signature { get; }

            public static ulong ProbeSignature(byte* state, ushort* tilePcls, int tilePclCount,
                Action<int> invalidTileId)
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                var pclScratch = new int[EconomyCoarseCellTileSize * EconomyCoarseCellTileSize];
                Dictionary<int, int> canonicalPcls = BuildCanonicalPclMap(tilePcls, tilePclCount);
                ulong total = 1469598103934665603UL;
                for (int index = 0; index < EconomyGridCellCount; index++)
                {
                    byte* cell = state + EconomyGridBaseOffset + index * EconomyGridCellStride;
                    ulong cellHash = 1469598103934665603UL;
                    for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                    {
                        if (offset != 5) cellHash = Hash(cellHash, cell[offset]);
                    }
                    EconomyCoordinate coordinate = EconomyCoordinate.FromIndex(index);
                    int count = ReadDistinctPcls(tiles, tilePcls, tilePclCount,
                        coordinate.X, coordinate.Y, pclScratch, invalidTileId);
                    for (int pclIndex = 0; pclIndex < count; pclIndex++)
                        cellHash = Hash(cellHash, canonicalPcls[pclScratch[pclIndex]]);
                    total = Hash(total, unchecked((int)cellHash));
                    total = Hash(total, unchecked((int)(cellHash >> 32)));
                }
                return total;
            }

            public static EconomyRoutingSnapshot Capture(byte* state, ushort* tilePcls, int tilePclCount,
                Action<int> invalidTileId)
            {
                var signatures = new ulong[EconomyGridCellCount];
                var payloads = new byte[EconomyGridCellCount * StoredRawBytesPerCell];
                var counts = new byte[EconomyGridCellCount];
                var flattenedPcls = new ushort[EconomyGridCellCount * EconomyCoarseCellTileSize * EconomyCoarseCellTileSize];
                var pclScratch = new int[EconomyCoarseCellTileSize * EconomyCoarseCellTileSize];
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                Dictionary<int, int> canonicalPcls = BuildCanonicalPclMap(tilePcls, tilePclCount);
                ulong total = 1469598103934665603UL;
                for (int index = 0; index < EconomyGridCellCount; index++)
                {
                    byte* cell = state + EconomyGridBaseOffset + index * EconomyGridCellStride;
                    ulong cellHash = 1469598103934665603UL;
                    int rawIndex = index * StoredRawBytesPerCell;
                    for (int offset = sizeof(int); offset < EconomyGridCellStride; offset++)
                    {
                        if (offset == 5) continue; // Per-search distance changes are captured by search observations.
                        byte value = cell[offset];
                        payloads[rawIndex++] = value;
                        cellHash = Hash(cellHash, value);
                    }
                    EconomyCoordinate coordinate = EconomyCoordinate.FromIndex(index);
                    int cellPclCount = ReadDistinctPcls(tiles, tilePcls, tilePclCount,
                        coordinate.X, coordinate.Y, pclScratch, invalidTileId);
                    counts[index] = checked((byte)cellPclCount);
                    int pclBase = index * EconomyCoarseCellTileSize * EconomyCoarseCellTileSize;
                    for (int pclIndex = 0; pclIndex < cellPclCount; pclIndex++)
                    {
                        int pcl = pclScratch[pclIndex];
                        flattenedPcls[pclBase + pclIndex] = checked((ushort)pcl);
                        cellHash = Hash(cellHash, canonicalPcls[pcl]);
                    }
                    signatures[index] = cellHash;
                    total = Hash(total, unchecked((int)cellHash));
                    total = Hash(total, unchecked((int)(cellHash >> 32)));
                }
                return new EconomyRoutingSnapshot(total, signatures, payloads, counts, flattenedPcls);
            }

            private static Dictionary<int, int> BuildCanonicalPclMap(ushort* tilePcls, int tilePclCount)
            {
                var result = new Dictionary<int, int>();
                if (tilePcls == null || tilePclCount <= 0) return result;
                // First native tile occurrence is stable when Vanilla merely renumbers otherwise identical regions.
                for (int tileId = 0; tileId < tilePclCount; tileId++)
                {
                    int pcl = tilePcls[tileId];
                    if (pcl > 0 && !result.ContainsKey(pcl)) result.Add(pcl, result.Count + 1);
                }
                return result;
            }

            private static int ReadDistinctPcls(GameTileManagerAPI tiles, ushort* tilePcls, int tilePclCount,
                int coarseX, int coarseY, int[] scratch, Action<int> invalidTileId)
            {
                int count = 0;
                int beginX = coarseX * EconomyCoarseCellTileSize;
                int beginY = coarseY * EconomyCoarseCellTileSize;
                for (int dy = 0; dy < EconomyCoarseCellTileSize; dy++)
                {
                    for (int dx = 0; dx < EconomyCoarseCellTileSize; dx++)
                    {
                        int x = beginX + dx;
                        int y = beginY + dy;
                        if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                        int tileId = tiles.GetTileId(x, y);
                        if (tilePcls == null || (uint)tileId >= (uint)tilePclCount)
                        {
                            invalidTileId?.Invoke(tileId);
                            continue;
                        }
                        int pcl = tilePcls[tileId];
                        if (pcl == 0) continue;
                        bool duplicate = false;
                        for (int existing = 0; existing < count; existing++)
                            if (scratch[existing] == pcl) { duplicate = true; break; }
                        if (!duplicate) scratch[count++] = pcl;
                    }
                }
                Array.Sort(scratch, 0, count);
                return count;
            }

            public string DescribeAll()
            {
                var groups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
                for (int index = 0; index < EconomyGridCellCount; index++)
                    AddToGroup(groups, DescribeCellState(index), index);
                return "cellCount=" + EconomyGridCellCount + "; stateGroupCount=" + groups.Count +
                    "; stateGroups=[" + string.Join("; ", groups.Select(pair =>
                        "state{" + pair.Key + "}=>coordinates=[" +
                        LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]")) + "]";
            }

            public string DescribeSummary()
            {
                var byte04 = new SortedDictionary<int, int>();
                var byte16 = new SortedDictionary<int, int>();
                var pclCountDistribution = new SortedDictionary<int, int>();
                for (int index = 0; index < EconomyGridCellCount; index++)
                {
                    int rawBase = index * StoredRawBytesPerCell;
                    int value04 = rawPayloads[rawBase];
                    int value16 = rawPayloads[rawBase + 0x11];
                    byte04[value04] = byte04.TryGetValue(value04, out int count04) ? count04 + 1 : 1;
                    byte16[value16] = byte16.TryGetValue(value16, out int count16) ? count16 + 1 : 1;
                    int pclCount = pclCounts[index];
                    pclCountDistribution[pclCount] = pclCountDistribution.TryGetValue(pclCount, out int countPcl)
                        ? countPcl + 1 : 1;
                }
                return "cellCount=" + EconomyGridCellCount +
                    "; byte04Distribution=[" + string.Join(",", byte04.Select(pair => pair.Key + "=" + pair.Value)) + "]" +
                    "; byte16Distribution=[" + string.Join(",", byte16.Select(pair => pair.Key + "=" + pair.Value)) + "]" +
                    "; distinctPclCountPerCell=[" + string.Join(",", pclCountDistribution.Select(pair => pair.Key + "=" + pair.Value)) + "]" +
                    "; complete coordinates are emitted for changes and economy-search relevant cells";
            }

            public string DescribeDelta(EconomyRoutingSnapshot previous)
            {
                var groups = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
                int changed = 0;
                int rawChanged = 0;
                int pclOnlyChanged = 0;
                for (int index = 0; index < EconomyGridCellCount; index++)
                {
                    if (previous.rawSignatures[index] == rawSignatures[index]) continue;
                    changed++;
                    bool rawDiffers = !RawPayloadEquals(previous, index);
                    if (rawDiffers) rawChanged++; else pclOnlyChanged++;
                    string transition = "before{" + previous.DescribeCellState(index) + "}->after{" +
                        DescribeCellState(index) + "}";
                    AddToGroup(groups, transition, index);
                }
                return "changedCellCount=" + changed + "; rawPayloadChanged=" + rawChanged +
                    "; pclOnlyChanged=" + pclOnlyChanged + "; transitionGroupCount=" + groups.Count +
                    "; transitionGroups=[" + string.Join("; ", groups.Select(pair => pair.Key +
                        "=>coordinates=[" + LosslessGridCoordinateFormatter.Format(pair.Value, EconomyGridWidth) + "]")) + "]";
            }

            private static void AddToGroup(SortedDictionary<string, List<int>> groups, string key, int index)
            {
                if (!groups.TryGetValue(key, out List<int> values))
                {
                    values = new List<int>();
                    groups.Add(key, values);
                }
                values.Add(index);
            }

            private bool RawPayloadEquals(EconomyRoutingSnapshot other, int index)
            {
                int rawBase = index * StoredRawBytesPerCell;
                for (int offset = 0; offset < StoredRawBytesPerCell; offset++)
                    if (rawPayloads[rawBase + offset] != other.rawPayloads[rawBase + offset]) return false;
                return true;
            }

            private string DescribeCellState(int index)
            {
                var raw = new StringBuilder(StoredRawBytesPerCell * 2);
                int rawBase = index * StoredRawBytesPerCell;
                for (int offset = 0; offset < StoredRawBytesPerCell; offset++)
                    raw.Append(rawPayloads[rawBase + offset].ToString("X2"));
                int pclBase = index * EconomyCoarseCellTileSize * EconomyCoarseCellTileSize;
                string pclText = string.Join(",", Enumerable.Range(0, pclCounts[index])
                    .Select(pclIndex => pclValues[pclBase + pclIndex].ToString()));
                return $"raw+04-except+05={raw},pcls=[{pclText}]";
            }
        }

        private sealed class WallTileBaseline
        {
            public WallTileBaseline(int playerId, WallOwnerEncoding encoding, int keepX, int keepY,
                Dictionary<int, WallTileState> tiles, HashSet<int> componentTiles,
                HashSet<int> componentBlockers, List<HashSet<int>> candidateComponents, HashSet<int> interiorTiles,
                HashSet<int> exteriorTiles, bool geometryClosed, List<WallAnchorPair> anchors)
            {
                PlayerId = playerId; Encoding = encoding; KeepX = keepX; KeepY = keepY;
                Tiles = tiles; LastObservedTiles = new Dictionary<int, WallTileState>(tiles);
                ComponentTiles = componentTiles; ComponentBlockers = componentBlockers;
                CandidateComponents = candidateComponents ?? new List<HashSet<int>>();
                InteriorTiles = interiorTiles; ExteriorTiles = exteriorTiles;
                GeometryClosed = geometryClosed; Anchors = anchors;
            }
            public int PlayerId { get; }
            public WallOwnerEncoding Encoding { get; }
            public int KeepX { get; }
            public int KeepY { get; }
            public Dictionary<int, WallTileState> Tiles { get; }
            public Dictionary<int, WallTileState> LastObservedTiles { get; }
            public HashSet<int> LostWallTiles { get; } = new HashSet<int>();
            public HashSet<int> ComponentTiles { get; }
            public HashSet<int> ComponentBlockers { get; }
            public List<HashSet<int>> CandidateComponents { get; }
            public HashSet<int> InteriorTiles { get; }
            public HashSet<int> ExteriorTiles { get; }
            public bool GeometryClosed { get; }
            public List<WallAnchorPair> Anchors { get; }
        }

        private readonly struct WallTileState
        {
            public WallTileState(int tileId, int x, int y, int logic, byte rawOwner, byte damage,
                byte structureWas, byte gatePath, int pcl)
            {
                TileId = tileId; X = x; Y = y; Logic = logic; RawOwner = rawOwner;
                Damage = damage; StructureWas = structureWas; GatePath = gatePath; Pcl = pcl;
            }
            public int TileId { get; }
            public int X { get; }
            public int Y { get; }
            public int Logic { get; }
            public byte RawOwner { get; }
            public byte Damage { get; }
            public byte StructureWas { get; }
            public byte GatePath { get; }
            public int Pcl { get; }
            public bool IsWall => (Logic & (int)TilePropertyFlag.IsWall) != 0;
            public bool DataEquals(WallTileState other) => Logic == other.Logic && RawOwner == other.RawOwner &&
                Damage == other.Damage && StructureWas == other.StructureWas && GatePath == other.GatePath && Pcl == other.Pcl;
            public override string ToString() =>
                $"tile={TileId}@({X},{Y})/logic=0x{Logic:X8}/rawOwner={RawOwner}/damage={Damage}/structureWas={StructureWas}/gatePath={GatePath}/pcl={Pcl}";
        }

        private sealed class WallAnchorPair
        {
            public WallAnchorPair(int wallTileId, int insideTileId, int outsideTileId,
                int oldInsidePcl, int oldOutsidePcl)
            {
                WallTileId = wallTileId; InsideTileId = insideTileId; OutsideTileId = outsideTileId;
                OldInsidePcl = oldInsidePcl; OldOutsidePcl = oldOutsidePcl;
            }
            public int WallTileId { get; }
            public int InsideTileId { get; }
            public int OutsideTileId { get; }
            public int OldInsidePcl { get; }
            public int OldOutsidePcl { get; }
            public override string ToString() =>
                $"wall={WallTileId}/inside={InsideTileId}:{OldInsidePcl}/outside={OutsideTileId}:{OldOutsidePcl}";
        }

        private readonly struct WallTileDelta
        {
            public WallTileDelta(WallTileState before, WallTileState after) { Before = before; After = after; }
            public WallTileState Before { get; }
            public WallTileState After { get; }
            public int TileId => Before.TileId;
            public bool WallLost => Before.IsWall && !After.IsWall;
            public bool MaterialChanged => Before.Logic != After.Logic || Before.RawOwner != After.RawOwner ||
                Before.Damage != After.Damage || Before.StructureWas != After.StructureWas ||
                Before.GatePath != After.GatePath;
            public bool PclOnlyChanged => !MaterialChanged && Before.Pcl != After.Pcl;
            public override string ToString() => $"{Before}->{After}/wallLost={WallLost}";
        }

        private readonly struct FrameSnapshot
        {
            public FrameSnapshot(int frame, int status, int mapper, int count, int first, string position) { Frame = frame; Status = status; Mapper = mapper; PositionCount = count; FirstPosition = first; Position = position; }
            public int Frame { get; } public int Status { get; } public int Mapper { get; } public int PositionCount { get; } public int FirstPosition { get; } public string Position { get; }
        }

        private readonly struct BuildingSnapshot
        {
            public BuildingSnapshot(int id, uint globalId, int ownerId, eStructs type, AliveState alive, int x, int y,
                int endX, int endY, int worldX, int worldY, int currentHealth, int maxHealth, byte sleeping, int gatehouseId)
            { Id = id; GlobalId = globalId; OwnerId = ownerId; Type = type; Alive = alive; TileX = x; TileY = y; EndX = endX; EndY = endY; WorldX = worldX; WorldY = worldY; CurrentHealth = currentHealth; MaxHealth = maxHealth; Sleeping = sleeping; GatehouseId = gatehouseId; }
            public int Id { get; } public uint GlobalId { get; } public int OwnerId { get; } public eStructs Type { get; } public AliveState Alive { get; }
            public int TileX { get; } public int TileY { get; } public int EndX { get; } public int EndY { get; } public int WorldX { get; } public int WorldY { get; }
            public int CurrentHealth { get; } public int MaxHealth { get; }
            public byte Sleeping { get; } public int GatehouseId { get; }
            public PreplacedIdentity Identity => new PreplacedIdentity(Id, GlobalId, OwnerId, (int)Type);
            public bool DataEquals(BuildingSnapshot other) => Identity.Equals(other.Identity) && Alive == other.Alive &&
                TileX == other.TileX && TileY == other.TileY && EndX == other.EndX && EndY == other.EndY &&
                WorldX == other.WorldX && WorldY == other.WorldY && CurrentHealth == other.CurrentHealth &&
                MaxHealth == other.MaxHealth && Sleeping == other.Sleeping && GatehouseId == other.GatehouseId;
            public string ToText(bool? inArea) => $"id={Id},global={GlobalId},owner={OwnerId},type={Type},category={ClassifyStructure(Type)},alive={Alive}({(int)Alive}),health={CurrentHealth}/{MaxHealth},sleep={Sleeping},gatehouseId={GatehouseId},tile=({TileX},{TileY})-({EndX},{EndY}),world=({WorldX},{WorldY}),area={(inArea.HasValue ? (inArea.Value ? "inside" : "outside") : "pending")}";
        }

        private readonly struct ReachabilitySnapshot
        {
            public ReachabilitySnapshot(int keepPcl, int targetPcl, PortalRouteResult route,
                List<PortalConnection> portals, string details)
            { KeepPcl = keepPcl; TargetPcl = targetPcl; Route = route; Portals = portals; Details = details ?? string.Empty; }
            public int KeepPcl { get; }
            public int TargetPcl { get; }
            public PortalRouteResult Route { get; }
            public List<PortalConnection> Portals { get; }
            public string Details { get; }
            public static ReachabilitySnapshot Unavailable => new ReachabilitySnapshot(0, 0,
                new PortalRouteResult(PortalRouteKind.Unreachable, null), new List<PortalConnection>(), "unavailable");
        }

        private readonly struct AccessibilityCallSnapshot
        {
            public AccessibilityCallSnapshot(int buildingId, string beforeState, int result)
            { BuildingId = buildingId; BeforeState = beforeState ?? "unknown"; Result = result; }
            public int BuildingId { get; }
            public string BeforeState { get; }
            public int Result { get; }
        }

        private static string ClassifyStructure(eStructs type)
        {
            switch (type)
            {
                case eStructs.STRUCT_TOWER1_DESTROYED:
                case eStructs.STRUCT_TOWER2_DESTROYED:
                case eStructs.STRUCT_TOWER3_DESTROYED:
                case eStructs.STRUCT_TOWER4_DESTROYED:
                case eStructs.STRUCT_TOWER5_DESTROYED:
                    return "ruin-destroyed-tower";
                case eStructs.STRUCT_RUINS:
                case eStructs.STRUCT_RUINS01:
                case eStructs.STRUCT_RUINS02:
                case eStructs.STRUCT_RUINS03:
                case eStructs.STRUCT_RUINS04:
                case eStructs.STRUCT_RUINS05:
                case eStructs.STRUCT_RUINS06:
                case eStructs.STRUCT_RUINS07:
                case eStructs.STRUCT_RUINS08:
                case eStructs.STRUCT_RUINS09:
                case eStructs.STRUCT_RUINS10:
                case eStructs.STRUCT_RUINS11:
                case eStructs.STRUCT_RUINS12:
                case eStructs.STRUCT_RUINS13:
                case eStructs.STRUCT_RUINS14:
                case eStructs.STRUCT_RUINS15:
                case eStructs.STRUCT_RUINS16:
                case eStructs.STRUCT_RUINS17:
                case eStructs.STRUCT_RUINS18:
                case eStructs.STRUCT_RUINS19:
                case eStructs.STRUCT_RUINS20:
                case eStructs.STRUCT_RUINS21:
                case eStructs.STRUCT_RUINS22:
                case eStructs.STRUCT_RUINS23:
                case eStructs.STRUCT_RUINS24:
                case eStructs.STRUCT_RUINS25:
                case eStructs.STRUCT_RUINS26:
                case eStructs.STRUCT_RUINS27:
                case eStructs.STRUCT_RUINS28:
                case eStructs.STRUCT_RUINS29:
                case eStructs.STRUCT_RUINS30:
                case eStructs.STRUCT_RUINS31:
                case eStructs.STRUCT_RUINS32:
                case eStructs.STRUCT_RUINS33:
                case eStructs.STRUCT_RUINS34:
                    return "ruin";
                case eStructs.STRUCT_WOOD_WALL:
                case eStructs.STRUCT_STONE_WALL:
                case eStructs.STRUCT_CRENAL_WALL:
                case eStructs.STRUCT_WAS_WALL:
                    return "wall";
                case eStructs.STRUCT_GATE_MAIN:
                case eStructs.STRUCT_GATE_INNER:
                case eStructs.STRUCT_GATE_WOOD:
                case eStructs.STRUCT_GATE_POSTERN:
                case eStructs.STRUCT_DRAWBRIDGE:
                case eStructs.STRUCT_GATEHOUSE:
                    return "portal";
                default:
                    return "other";
            }
        }

        private static bool IsPortalStructure(eStructs type) =>
            type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER ||
            type == eStructs.STRUCT_GATE_WOOD || type == eStructs.STRUCT_GATE_POSTERN ||
            type == eStructs.STRUCT_DRAWBRIDGE || type == eStructs.STRUCT_GATEHOUSE;

        private static bool IsWallStructure(eStructs type) =>
            type == eStructs.STRUCT_WOOD_WALL || type == eStructs.STRUCT_STONE_WALL ||
            type == eStructs.STRUCT_CRENAL_WALL || type == eStructs.STRUCT_WAS_WALL;

        private static bool IsDestroyedTower(eStructs type) =>
            type == eStructs.STRUCT_TOWER1_DESTROYED || type == eStructs.STRUCT_TOWER2_DESTROYED ||
            type == eStructs.STRUCT_TOWER3_DESTROYED || type == eStructs.STRUCT_TOWER4_DESTROYED ||
            type == eStructs.STRUCT_TOWER5_DESTROYED;

        private static bool IsEnclosureBuilding(eStructs type) =>
            IsPortalStructure(type) || type == eStructs.STRUCT_TOWER ||
            type == eStructs.STRUCT_TOWER1 || type == eStructs.STRUCT_TOWER2 ||
            type == eStructs.STRUCT_TOWER3 || type == eStructs.STRUCT_TOWER4 ||
            type == eStructs.STRUCT_TOWER5;

        private sealed class InventoryRecord
        {
            public InventoryRecord(string label, List<BuildingSnapshot> buildings) { Label = label; Buildings = buildings; }
            public string Label { get; } public List<BuildingSnapshot> Buildings { get; }
            public HashSet<int> ReclassifiedPlayers { get; } = new HashSet<int>();
        }

        private sealed class DamageContext
        {
            private DamageContext(BuildingSnapshot? building, int delayBefore, bool wasPreplaced)
            {
                Building = building;
                DelayBefore = delayBefore;
                WasPreplaced = wasPreplaced;
            }

            public BuildingSnapshot? Building { get; }
            public int OwnerId => Building.HasValue ? Building.Value.OwnerId : 0;
            public int DelayBefore { get; }
            public bool WasPreplaced { get; }

            public static DamageContext Unmatched(BuildingTileTakeDamageEventArgs args) => new DamageContext(null, -1, false);

            public DamageContext(BuildingSnapshot building, int delayBefore, bool wasPreplaced) :
                this((BuildingSnapshot?)building, delayBefore, wasPreplaced) { }

            public string Describe(BuildingTileTakeDamageEventArgs args)
            {
                string target = Building.HasValue ? Building.Value.ToText(null) : "building=unresolved";
                string lethalCandidate = Building.HasValue ? DamageObservationModel.IsLethalInput(Building.Value.CurrentHealth, args.Damage).ToString() : "unknown";
                return $"{target},damageTile={args.TileId}@({args.TileX},{args.TileY}),amount={args.Damage},lethalInput={lethalCandidate},unknown1={args.Unknown1},sourcePlayer={args.PlayerIdSource},activationMode={args.Unknown3},unknown4={args.Unknown4}";
            }
        }
    }
}
