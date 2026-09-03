using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BugfixesAndQoL;
using Shared;

internal static class Program
{
    private const string ExpectedDllHash =
        "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
    private const string DllPath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

    private static readonly Dictionary<string, int> PatternRvas =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ConstructingFailureStatusPattern"] = 0x9129E,
            ["EuropeanPlacementRejectPattern"] = 0x929D3,
            ["MercenaryPlacementRejectPattern"] = 0x928E0,
            ["EngineerPlacementRejectPattern"] = 0x926FA,
            ["TunnelerPlacementRejectPattern"] = 0x912E0,
            ["KnightPlacementRejectPattern"] = 0x913CF,
            ["BedouinPlacementRejectPattern"] = 0x927ED,
            ["CursorMountedClassificationPattern"] = 0x8F209,
            ["FeedbackMountedClassificationPattern"] = 0x195F5E,
            ["MountedEndpointWallGatePattern"] = 0x196483,
            ["CreateHerdPattern"] = 0xD17D0,
            ["PopularityExitPattern"] = 0xCB55C,
            ["AreaTreatmentPattern"] = 0xA0470,
            ["DiseaseSearchPattern"] = 0x9F700,
            ["HealerUpdateExitPattern"] = 0x1501A7,
            ["PeriodicDiseaseFoundPattern"] = 0x14F8CC,
            ["WorkingBuildingExitReferencePattern"] = 0x14F768,
            ["SpearmanMovementDecisionPattern"] = 0x143BD9,
            ["PreTerrainSpeedAdjustmentPattern"] = 0x19B506,
            ["UnitTypeUpdateDispatchPattern"] = 0x18410C,
            ["MovementCadencePattern"] = 0x184203,
            ["MarketValidatorPattern"] = 0xD7080,
            ["MarketPacketTailPattern"] = 0xD7324,
            ["MarketStorageCallPattern"] = 0xD7119,
            ["AutoMarketSellStatisticPattern"] = 0xD0484,
            ["RecruitEuropeanUnitPattern"] = 0x190CA0,
            ["SellerReservePattern"] = 0x3F14F,
            ["AivSlotLayoutPattern"] = 0x5068A,
            ["AivStepLayoutPattern"] = 0x517C2,
            ["AivHighestFramePattern"] = 0x55F64,
            ["AivInitialFirstBuildStatePattern"] = 0x53F0B,
            ["AivResourceShortageReturnPattern"] = 0x51842,
            ["AivFirstBuildSuccessPattern"] = 0x5216D,
            ["AivPlacementRetryPattern"] = 0x5217A,
            ["SleepStateComparisonPattern"] = 0xC7DCB,
            ["SleepStateSynchronizationFunctionPattern"] = 0xC7D50,
            ["EmergencyDemolitionComparisonPattern"] = 0x2F454,
            ["AIHovelDemolitionFunctionPattern"] = 0x3B1D0,
            ["InaccessibleBuildingComparisonPattern"] = 0x3B2FF,
            ["SetupBuildingEntrancesOffsetPattern"] = 0xC0270,
            ["NarrowRuinClassifierPattern"] = 0x5D055,
            ["BroadRuinClassifierPattern"] = 0x5D025,
            ["MapperSelectionPattern"] = 0x5CEAB,
            ["BroadBlockerLoadPattern"] = 0x5D016,
            ["NarrowBlockerLoadPattern"] = 0x5D045,
            ["AssassinBuilderPattern"] = 0xD9C40,
            ["EndpointBuildingGuardsPattern"] = 0xE19D4,
            ["DispatcherAssassinBranchPattern"] = 0xF4B0C,
            ["State106CombatFinishCallSequence"] = 0x16DFCE,
            ["CombatFinishHelperSequence"] = 0x1853F0,
            ["PostCombatRepathPrologueSequence"] = 0x1976C0,
            ["PostCombatPathRequestSequence"] = 0x197702,
            ["CommonPathContextReadSequence"] = 0x1964EE,
            ["CommonPathSuccessClearSequence"] = 0x196734,
            ["CommonPathFailureClearSequence"] = 0x19676C
        };

    private static readonly FunctionContract[] Functions =
    {
        new FunctionContract(0xC7D50, 315, "07807D9F9E8BE5ABE37CD522213B0C1A59E2BC84D1FD682E9236DB0250F38A37"),
        new FunctionContract(0x2F080, 1520, "C06FA5ABB5B3BEF713391158BF7ED326245526F16BF7C74D5DB059231874F38E"),
        new FunctionContract(0x3B1D0, 160, "AC37E9A8205EDA52D0591BAB84A4DC0FD4BF389B3DB2A768786FC42A7FD6E3AC"),
        new FunctionContract(0x3B270, 229, "EFF1F3C1FB0BB922746F17F266D62EAA252E6FF7C64610EF50856EA2833AAD9D"),
        new FunctionContract(0xC0270, 590, "8C851D48BC5579727AD53C1C1CC3A835E95C57CE8E68DBFD8B23C43BDBFEF97F"),
        new FunctionContract(0x3EE10, 1105, "B1F7DF14291D0D4C0AE544204E279BC57BBC8E617C29E3A269EBB405FF114765"),
        new FunctionContract(0x50680, 159, "B6DAA534A93D19F9EFC032A8CA604E12C3E6087A61D3615EC4E1476D0708283E"),
        new FunctionContract(0x51790, 2774, "69731F77776995C9FC452A7A9A41408385B757B461F0E7FAB76E291BE64C3ECF"),
        new FunctionContract(0x55F50, 144, "707C57D1FEBE76D9AF6E535B4D4A7068B5FC2D305C901E7CB3CC3582163AB502"),
        new FunctionContract(0x5CD90, 1077, "099D5E8B4AB0B93EB2BE39501D06AE0FC38F481035AF50650654F6F233B23A17"),
        new FunctionContract(0x90CD0, 8126, "A47403466994BB1D1D3476C81E9511AAC9653A3CFC475FFA6D76E65E150110B6"),
        new FunctionContract(0x9F700, 525, "D4C059E5AED1B7FFCFA334E0A361EDA4DC7B49EF1FBAE9F8972E231FC4A0BC6A"),
        new FunctionContract(0xA0470, 198, "5AA2550296CF94BD7180F240144C0353A7ECE97021975CA94F3DC85F25B3202A"),
        new FunctionContract(0xCB090, 1891, "A131D1CA8B25B95C2AF694CD94D3A4CBFA92DEBE1F12990647146D44E4FEAE05"),
        new FunctionContract(0xD0380, 555, "F61D65B94E3089FA60BE490EF828FA48375B2A226C9FFEB4CA54B01864BC7CC0"),
        new FunctionContract(0xD17D0, 494, "9ED8D8B10616413BC5FC3F2CEB060E56964CA0147FD6146992D2B300289C55F6"),
        new FunctionContract(0xDB650, 1537, "890403C9C8A9114EEAA2CA33A681BCDEC3BD3C2503E0C4110A3EC0A33C801B68"),
        new FunctionContract(0xD7080, 734, "3A931C5FEB5FB9D324C12CE53ADE9648D2E26FFB9EF62B75D0C3BD8AAAA3C924"),
        new FunctionContract(0xD9C40, 990, "5596B8DBF622F8C44085BAE06C5E318A61B84BE6F4D9A0F2A73113C616B3A65E"),
        new FunctionContract(0x107160, 50, "4A83B91AC728B7DB6E746997635D2B96B8895D81B67B2D8DC32598B4C5D4FF44"),
        new FunctionContract(0x143400, 7001, "F39AFDE7543E274058168DD080F96621592AEABE0CDE897BCFBDB3A983F25C53"),
        new FunctionContract(0x14F3C0, 3588, "EE4650DA6F0D11CFAAB97A1CD8124A7DD1291C0E89B8D6FB3EDA6B00A8BE4602"),
        new FunctionContract(0x182B00, 9137, "F640FE9609EEC3199B9C675B91CCF488310B0A23B832BD010FDC80AB00DF153F"),
        new FunctionContract(0x1853F0, 55, "A7B2D84B7487FA73BF4A94C91536BB89F15E898F4525CCCD08B4818980DEA82E"),
        new FunctionContract(0x190CA0, 938, "8F397249E08A12338327322581CC17F0B9FE6507A426D2B2A29079A102471C6B"),
        new FunctionContract(0x196280, 1293, "D81EEBC55A1FB0CFEEB25D0B0D1CCEDE5C9F545E3CCCCAC5119A556C7B43E9E1"),
        new FunctionContract(0x196810, 33, "FA0090EB160121E461BDBD72FAF66A24F519A7049BE8BA5C2FC7DEBAE554FA8A"),
        new FunctionContract(0x1976C0, 211, "39E4EE6EF688BA664742C592585D2EFF99FF0CBDA16E60B6093DF9BBA64A0469"),
        new FunctionContract(0x19B260, 966, "15CEB13D6FF56A004CF35CB77A868035410076F63742231CAD5C999AB9B45A9C")
    };

    private static int assertions;

    private static int Main()
    {
        try
        {
            string workspace = FindWorkspace();
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == ExpectedDllHash, "canonical DLL hash");
            PeImage pe = PeImage.Load(file);
            CheckGatehouseQueryUnitIdContract();
            CheckMountedStockpilePolicy();
            CheckFunctions(pe.Image);
            CheckProductionPatterns(workspace, pe);
            CheckCriticalSpans(pe.Image);
            CheckUnknownHashPolicy(workspace);
            Console.WriteLine($"PASS: BugfixesAndQoL native tests ({assertions} assertions, {PatternRvas.Count} signatures).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void CheckGatehouseQueryUnitIdContract()
    {
        Check(GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId(0, 10000, out int first) && first == 1,
            "gatehouse first unit span index converts to game ID 1");
        Check(GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId(9999, 10000, out int last) && last == 10000,
            "gatehouse last unit span index converts once");
        Check(!GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId(-1, 10000, out _),
            "negative gatehouse unit span index rejected");
        Check(!GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId(10000, 10000, out _),
            "out-of-range gatehouse unit span index rejected");
        Check(GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(null, true),
            "corrected Vanilla true decision supplies missing event default");
        Check(!GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(null, false),
            "corrected Vanilla false decision supplies missing event default");
        Check(GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(true, false),
            "earlier subscriber true decision is preserved");
        Check(!GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(false, true),
            "earlier subscriber false decision is preserved");
    }

    private static void CheckMountedStockpilePolicy()
    {
        const uint goodsyard = MountedStockpileMovementPolicy.GoodsyardRelated;
        Check(MountedStockpileMovementPolicy.IsWallOrElevated == 0x10000100,
            "Vanilla endpoint IsWall-or-IsElevated mask retained");
        Check(MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, true, goodsyard, 1, 1, true),
            "single mounted stockpile selection corrected");
        Check(MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, true, goodsyard | 0x400, 4, 4, true),
            "mixed mounted stockpile selection corrected");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, true, goodsyard, 2, 1, true),
            "selection containing infantry retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, true, goodsyard, 1, 1, false),
            "partially unresolved selection retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, true, goodsyard, 0, 0, true),
            "empty selection retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, false, true, goodsyard, 1, 1, true),
            "invalid target coordinate retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, false, goodsyard, 1, 1, true),
            "unavailable stockpile target retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                1, true, true, 0, 1, 1, true),
            "non-stockpile target retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                0, true, true, goodsyard, 1, 1, true),
            "ordinary Vanilla classification retained");
        Check(!MountedStockpileMovementPolicy.ShouldUseNormalMovementClassification(
                -1, true, true, goodsyard, 1, 1, true),
            "negative Vanilla classification retained");
        Check(MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                true, true, true, goodsyard | 0x100, true),
            "mounted Goodsyard endpoint wall rejection bypassed");
        Check(!MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                false, true, true, goodsyard, true),
            "non-rejected endpoint was not modified");
        Check(MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                true, true, true, goodsyard | 0x100, true),
            "mounted member of a mixed selection uses the same Goodsyard endpoint rule");
        Check(!MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                true, true, true, goodsyard, false),
            "non-mounted current unit endpoint retained");
        Check(!MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                true, true, false, goodsyard, true),
            "unavailable mounted endpoint retained");
        Check(!MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                true, true, true, 0x100, true),
            "non-Goodsyard wall endpoint retained");
        Check(!MountedStockpileMovementPolicy.ShouldBypassMountedEndpointWallGate(
                true, false, true, goodsyard, true),
            "invalid mounted endpoint coordinate retained");
    }

    private static void CheckFunctions(byte[] image)
    {
        foreach (FunctionContract function in Functions)
        {
            Check(function.Rva >= 0 && function.Rva <= image.Length - function.Size,
                $"function 0x{function.Rva:X} bounds");
            byte[] bytes = new byte[function.Size];
            Buffer.BlockCopy(image, function.Rva, bytes, 0, function.Size);
            string actualHash = Hash(bytes);
            Check(actualHash == function.Hash,
                $"function 0x{function.Rva:X} hash: expected {function.Hash}, actual {actualHash}");
        }
    }

    private static void CheckProductionPatterns(string workspace, PeImage pe)
    {
        Dictionary<string, string> patterns = ReadConstStrings(
            Path.Combine(workspace, "BugfixesAndQoL", "src"));
        foreach (KeyValuePair<string, int> contract in PatternRvas)
        {
            Check(patterns.TryGetValue(contract.Key, out string pattern), contract.Key + " source constant");
            PatternToken[] tokens = ParsePattern(pattern);
            Check(Matches(pe.Image, contract.Value, tokens), contract.Key + " reference RVA");
            Check(pe.IsExecutable(contract.Value, tokens.Length), contract.Key + " executable section");
            List<int> matches = FindMatches(pe, tokens);
            Check(matches.Count == 1 && matches[0] == contract.Value,
                contract.Key + " unique executable match");
        }
    }

    private static void CheckCriticalSpans(byte[] image)
    {
        CheckBytes(image, 0x912B4, "0F 44 D8", "assembly preview original span");
        foreach (int rva in new[] { 0x929D5, 0x928E2, 0x926FC, 0x912E2, 0x913D1, 0x927EF })
            CheckBytes(image, rva, "0F 84", $"assembly rejection original span 0x{rva:X}");
        CheckBytes(image, 0x3F156,
            "42 8D 14 18 45 85 E4 7E 34 41 81 BE CC F0 12 00 F4 01 00 00",
            "AI stone full 20-byte hook span");
        CheckBytes(image, 0x19B506,
            "0F B6 83 C8 06 00 00 45 85 C9 74 42 3C 18",
            "pre-terrain movement full 14-byte hook span");
        CheckBytes(image, 0x197716,
            "66 89 8B 4E 07 00 00 89 4C 24 20 48 8B CE",
            "Assassin combat-context full 14-byte hook span");
        CheckBytes(image, 0xE19D8, "0F 85 B1 00 00 00", "Assassin current-tile rejection jump");
        CheckBytes(image, 0xE19F9, "0F 85 88 00 00 00", "Assassin neighbor-tile rejection jump");
        CheckBytes(image, 0x8E6E4,
            "F6 84 87 00 84 89 00 02 0F 85 96 08 00 00",
            "primary Goodsyard flag test and Vanilla movement jump");
        CheckBytes(image, 0x8EB47,
            "42 F6 84 8F 00 84 89 00 02 0F 85 F4 01 00 00",
            "alternate Goodsyard flag test and Vanilla movement jump");
        CheckBytes(image, 0x8F209,
            "E8 12 79 10 00 85 C0 74 66 4C 63 0D 1F 2C 98 03 48 8D 15 A0 2B 98 03",
            "cursor classifier call and full 18-byte hook span");
        CheckBytes(image, 0x195F5E,
            "E8 FD AB FE FF 85 C0 74 6A 4C 63 0D BA BE 87 03 48 8D 15 8B A0 E6 FF",
            "order-feedback classifier call and full 18-byte hook span");
        CheckCallTarget(image, 0x8F209, 0x196B20, "cursor mounted-classifier call target");
        CheckCallTarget(image, 0x195F5E, 0x180B60, "order-feedback mounted-classifier call target");
        CheckBytes(image, 0x196483,
            "44 39 94 24 80 00 00 00 75 11 F7 84 8A B0 71 8F 04 00 01 00 10 " +
            "0F 85 CE 02 00 00 44 8B AC 24 90 00 00 00 44 3B E3 0F 84 D6 00 00 00",
            "per-unit endpoint wall gate and full hook context");
        CheckBytes(image, 0x19648D,
            "F7 84 8A B0 71 8F 04 00 01 00 10 0F 85 CE 02 00 00",
            "complete per-unit endpoint test and rejection hook span");
        CheckRelativeJump(image, 0x196498, 0x19676C, "per-unit endpoint rejection target");
        CheckBytes(image, 0x196294,
            "48 63 F2 45 33 D2 48 69 FE 90 04 00 00 4D 63 F0 48 8D 15 55 9D E6 FF 48 03 F9 " +
            "49 63 E9 4C 8B F9 48 0F BF 87 E6 06 00 00",
            "path builder 1-based unit ID and manager-relative unit view setup");
        foreach (int mountedType in new[] { 28, 74, 78, 83 })
            Check(ReadInt32(image, 0x322540 + mountedType * 4) == 0,
                $"mounted type {mountedType} uses Vanilla endpoint-restricted movement class zero");
        foreach (int ordinaryType in new[] { 22, 23, 24, 25, 26, 27 })
            Check(ReadInt32(image, 0x322540 + ordinaryType * 4) == 1,
                $"ordinary troop type {ordinaryType} uses Vanilla movement class one");
        CheckBytes(image, 0x8F121, "81 E5 00 01 00 10", "cursor IsWall-or-IsElevated mask");
        CheckBytes(image, 0x195ED1, "41 81 E5 00 01 00 10", "command IsWall-or-IsElevated mask");
    }

    private static void CheckUnknownHashPolicy(string workspace)
    {
        string plague = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "PlagueNativePatternValidator.cs"));
        string recruitment = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "AiRecruitmentHorseDemandFix.cs"));
        string mountedStockpile = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "MountedStockpileMovementPatch.cs"));
        Check(plague.Contains("if (!referenceHashMatches)"), "plague fixed-layout unknown-hash gate");
        Check(recruitment.Contains("if (!referenceHashMatches)"), "AI recruitment result-layout unknown-hash gate");
        Check(mountedStockpile.Contains("if (!referenceHashMatches)"), "mounted-stockpile unknown-hash gate");
        Check(mountedStockpile.Contains("ClassificationHookSize = 18"),
            "mounted-stockpile complete classification hook size");
        Check(mountedStockpile.Contains("MountedEndpointWallGateHookSize = 17"),
            "mounted-stockpile complete endpoint hook size");
        Check(mountedStockpile.Contains("MountedEndpointWallGateHookOffset = 0xA") &&
              mountedStockpile.Contains("MountedEndpointWallGateJumpOffset = 11"),
            "mounted-stockpile endpoint hook starts on the complete test/jne pair");
        Check(mountedStockpile.Contains("TransactionFailureMode.RollbackAndThrow"),
            "mounted-stockpile atomic hook transaction");
        Check(mountedStockpile.Contains("transaction?.Unload()") &&
              mountedStockpile.Contains("transaction?.Dispose()") &&
              mountedStockpile.Contains("FreeEndpointZeroFlags()"),
            "mounted-stockpile reversible hook disposal");
        Check(mountedStockpile.Contains("!cursorClassificationHook.Success") &&
              mountedStockpile.Contains("!feedbackClassificationHook.Success") &&
              mountedStockpile.Contains("!mountedEndpointWallGateHook.Success"),
            "mounted-stockpile all three hooks must install atomically");
        Check(mountedStockpile.Contains("UnitFromManagerRelativeBaseOffset = 0x65C") &&
              mountedStockpile.Contains("NativeUnitCount = 10000") &&
              mountedStockpile.Contains("(uint)unitId - 1u < NativeUnitCount") &&
              mountedStockpile.Contains("int unitId = unchecked((int)context.Pointer->RSI);") &&
              mountedStockpile.Contains("TryGetUnitById(unitId") &&
              mountedStockpile.Contains("context.Pointer->RDI + UnitFromManagerRelativeBaseOffset") &&
              !mountedStockpile.Contains("unitSpanIndex + 1"),
            "mounted-stockpile endpoint maps Vanilla's 1-based ID and manager-relative unit view correctly");
        Check(mountedStockpile.Contains("OverwrittenInstructionPlacement.AfterCallback"),
            "mounted-stockpile callback runs before displaced test and branch");
        Check(mountedStockpile.Contains("X64SmartCPUContextRegs.Volatile") &&
              mountedStockpile.Contains("context.Pointer->RCX = 0") &&
              mountedStockpile.Contains("context.Pointer->RDX = unchecked"),
            "mounted-stockpile endpoint hook preserves live volatile registers and redirects only the audited read");
        Check(!mountedStockpile.Contains("context.Pointer->Rflags") &&
              !mountedStockpile.Contains("private const ulong ZeroFlag"),
            "mounted-stockpile endpoint hook does not depend on pre-hook flags");
        Check(!mountedStockpile.Contains("loggedCorrections") &&
              !mountedStockpile.Contains("mounted classification corrected") &&
              !mountedStockpile.Contains("per-unit endpoint wall gate bypassed"),
            "mounted-stockpile per-event diagnostic logging removed");
        Check(mountedStockpile.Contains("classificationCallbackFailureLogged") &&
              mountedStockpile.Contains("endpointCallbackFailureLogged") &&
              !mountedStockpile.Contains("private bool callbackFailureLogged;"),
            "mounted-stockpile callback failures remain independently visible");
        Check(mountedStockpile.Contains("!targetAvailable || !vanillaWallGateRejected"),
            "mounted-stockpile endpoint rejects irrelevant tiles before unit lookup");
        foreach (string mountedType in new[]
        {
            "eChimps.CHIMP_TYPE_KNIGHT",
            "eChimps.CHIMP_TYPE_ARAB_HORSEMAN",
            "eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER",
            "eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL"
        })
        {
            Check(mountedStockpile.Contains(mountedType),
                "mounted-stockpile playable type policy includes " + mountedType);
        }
        Check(!mountedStockpile.Contains("SixNops") && !mountedStockpile.Contains("NativeCodePatch"),
            "obsolete Goodsyard NOP patch removed");
    }

    private static void CheckCallTarget(byte[] image, int callRva, int expectedTargetRva, string label)
    {
        Check(callRva >= 0 && callRva <= image.Length - 5, label + " bounds");
        Check(image[callRva] == 0xE8, label + " opcode");
        int displacement = ReadInt32(image, callRva + 1);
        Check(callRva + 5 + displacement == expectedTargetRva, label);
    }

    private static void CheckRelativeJump(byte[] image, int jumpRva, int expectedTargetRva, string label)
    {
        Check(jumpRva >= 0 && jumpRva <= image.Length - 6, label + " bounds");
        Check(image[jumpRva] == 0x0F && image[jumpRva + 1] == 0x85, label + " opcode");
        int displacement = ReadInt32(image, jumpRva + 2);
        Check(jumpRva + 6 + displacement == expectedTargetRva, label);
    }

    private static Dictionary<string, string> ReadConstStrings(string sourceDirectory)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var declaration = new Regex(
            "const\\s+string\\s+(?<name>[A-Za-z0-9_]+)\\s*=\\s*(?<value>(?:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*\\+?)+)\\s*;",
            RegexOptions.Singleline);
        var literal = new Regex("\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");
        foreach (string file in Directory.GetFiles(sourceDirectory, "*.cs"))
        {
            string source = File.ReadAllText(file);
            foreach (Match match in declaration.Matches(source))
            {
                string name = match.Groups["name"].Value;
                string value = string.Concat(literal.Matches(match.Groups["value"].Value)
                    .Cast<Match>().Select(x => Regex.Unescape(x.Groups["text"].Value)));
                result[name] = value;
            }
        }
        return result;
    }

    private static PatternToken[] ParsePattern(string pattern) =>
        pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x == "?" || x == "??"
                ? new PatternToken(0, true)
                : new PatternToken(Convert.ToByte(x, 16), false))
            .ToArray();

    private static List<int> FindMatches(PeImage pe, PatternToken[] pattern)
    {
        var result = new List<int>();
        foreach (Section section in pe.Sections.Where(x => x.Executable))
        {
            int end = Math.Min(pe.Image.Length, section.Rva + section.Size) - pattern.Length;
            for (int rva = section.Rva; rva <= end; rva++)
            {
                if (Matches(pe.Image, rva, pattern))
                    result.Add(rva);
            }
        }
        return result;
    }

    private static bool Matches(byte[] image, int rva, PatternToken[] pattern)
    {
        if (rva < 0 || rva > image.Length - pattern.Length)
            return false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (!pattern[i].Wildcard && image[rva + i] != pattern[i].Value)
                return false;
        }
        return true;
    }

    private static void CheckBytes(byte[] image, int rva, string expected, string label)
    {
        byte[] bytes = expected.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray();
        Check(rva >= 0 && rva <= image.Length - bytes.Length, label + " bounds");
        for (int i = 0; i < bytes.Length; i++)
            Check(image[rva + i] == bytes[i], label + $" byte +0x{i:X}");
    }

    private static string FindWorkspace()
    {
        DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "BugfixesAndQoL", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    private static int ReadInt32(byte[] value, int offset) =>
        value[offset] | value[offset + 1] << 8 | value[offset + 2] << 16 | value[offset + 3] << 24;
    private static int ReadUInt16(byte[] value, int offset) => value[offset] | value[offset + 1] << 8;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct FunctionContract
    {
        public FunctionContract(int rva, int size, string hash) { Rva = rva; Size = size; Hash = hash; }
        public int Rva { get; }
        public int Size { get; }
        public string Hash { get; }
    }

    private readonly struct PatternToken
    {
        public PatternToken(byte value, bool wildcard) { Value = value; Wildcard = wildcard; }
        public byte Value { get; }
        public bool Wildcard { get; }
    }

    private readonly struct Section
    {
        public Section(int rva, int size, bool executable) { Rva = rva; Size = size; Executable = executable; }
        public int Rva { get; }
        public int Size { get; }
        public bool Executable { get; }
    }

    private sealed class PeImage
    {
        private PeImage(byte[] image, List<Section> sections) { Image = image; Sections = sections; }
        public byte[] Image { get; }
        public List<Section> Sections { get; }
        public bool IsExecutable(int rva, int length) => Sections.Any(x => x.Executable && rva >= x.Rva && rva + length <= x.Rva + x.Size);

        public static PeImage Load(byte[] file)
        {
            int pe = ReadInt32(file, 0x3C);
            int count = ReadUInt16(file, pe + 6);
            int optionalSize = ReadUInt16(file, pe + 20);
            int optional = pe + 24;
            int imageSize = ReadInt32(file, optional + 56);
            int headers = ReadInt32(file, optional + 60);
            byte[] image = new byte[imageSize];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headers, file.Length));
            int table = optional + optionalSize;
            var sections = new List<Section>();
            for (int i = 0; i < count; i++)
            {
                int h = table + i * 40;
                int virtualSize = ReadInt32(file, h + 8);
                int rva = ReadInt32(file, h + 12);
                int rawSize = ReadInt32(file, h + 16);
                int raw = ReadInt32(file, h + 20);
                int characteristics = ReadInt32(file, h + 36);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, rva, Math.Min(rawSize, file.Length - raw));
                sections.Add(new Section(rva, Math.Max(virtualSize, rawSize), (characteristics & 0x20000000) != 0));
            }
            return new PeImage(image, sections);
        }
    }
}
