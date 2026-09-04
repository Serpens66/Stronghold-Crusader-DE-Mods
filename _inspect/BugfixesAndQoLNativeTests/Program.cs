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
            ["CommonPathFailureClearSequence"] = 0x19676C,
            ["FirstClassifierPattern"] = 0x11EBF5,
            ["SecondClassifierPattern"] = 0x11EF39,
            ["AddClassifierPattern"] = 0xCAEF2,
            ["ReplaceClassifierPattern"] = 0xD0FF7,
            ["SummaryClassifierPattern"] = 0x18645E,
            ["ControlGroupStoragePattern"] = 0x186338
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
        new FunctionContract(0x19B260, 966, "15CEB13D6FF56A004CF35CB77A868035410076F63742231CAD5C999AB9B45A9C"),
        new FunctionContract(0xCAE40, 520, "8BF1FFB2D60B5E9DF7A13D9DB78C0CB131F858307338B4F8B189B4DF0DEF33A3"),
        new FunctionContract(0xD0F80, 432, "DE52839A245D148BD248EAC134F94987587D399124A65798D9C19624CD2079A5"),
        new FunctionContract(0x186300, 940, "F37C29D155B60C761054DED08860805ED6F414B1C716036CD9A7B0819EB10167"),
        new FunctionContract(0x1915C0, 642, "A1866D7DC80173A61656918F519B4DFAAD4F7FEAFF4BA828EE06DD0DC15A4405")
    };

    private static int assertions;

    private static int Main()
    {
        try
        {
            string workspace = FindWorkspace();
            byte[] file = File.ReadAllBytes(DllPath);
            Check(Hash(file) == ExpectedDllHash, "canonical DLL hash");
            Check(ExpectedDllHash == LordControlGroupNativeDefinition.ReferenceSha256,
                "Lord native contracts use the canonical DLL hash");
            Check(ExpectedDllHash == ControlGroupNativeDefinition.ReferenceSha256,
                "shared control-group contracts use the canonical DLL hash");
            PeImage pe = PeImage.Load(file);
            CheckGatehouseQueryUnitIdContract();
            CheckMountedStockpilePolicy();
            CheckFunctions(pe.Image);
            CheckProductionPatterns(workspace, pe);
            CheckCriticalSpans(pe.Image);
            CheckHealerAttackCommandContracts(pe.Image);
            CheckLordControlGroupContracts(pe.Image);
            CheckMixedLordDisbandContract(pe.Image);
            CheckDisbandCleanupPolicyAndWiring(workspace);
            CheckLordControlGroupTransactionModel(pe.Image);
            CheckLordControlGroupIconPolicy();
            CheckLordControlGroupUiContracts(workspace);
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

    private static void CheckHealerAttackCommandContracts(byte[] image)
    {
        int firstTable = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.FirstClassifierRva +
                HealerAttackCommandFixNativeDefinition.FirstTableInstructionOffset +
                HealerAttackCommandFixNativeDefinition.TableDisplacementOffset);
        int secondTable = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.SecondClassifierRva +
                HealerAttackCommandFixNativeDefinition.SecondTableInstructionOffset +
                HealerAttackCommandFixNativeDefinition.TableDisplacementOffset);
        int firstDispatch = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.FirstClassifierRva +
                HealerAttackCommandFixNativeDefinition.FirstDispatchInstructionOffset +
                HealerAttackCommandFixNativeDefinition.DispatchDisplacementOffset);
        int secondDispatch = ReadInt32(
            image,
            HealerAttackCommandFixNativeDefinition.SecondClassifierRva +
                HealerAttackCommandFixNativeDefinition.SecondDispatchInstructionOffset +
                HealerAttackCommandFixNativeDefinition.DispatchDisplacementOffset);

        Check(firstTable == HealerAttackCommandFixNativeDefinition.FirstTableRva,
            "first AttackUnit classifier resolves its audited table");
        Check(secondTable == HealerAttackCommandFixNativeDefinition.SecondTableRva,
            "second AttackUnit classifier resolves its audited table");
        Check(firstDispatch == HealerAttackCommandFixNativeDefinition.FirstDispatchTableRva,
            "first AttackUnit classifier resolves its audited dispatch table");
        Check(secondDispatch == HealerAttackCommandFixNativeDefinition.SecondDispatchTableRva,
            "second AttackUnit classifier resolves its audited dispatch table");

        int engineerIndex = HealerAttackCommandFixNativeDefinition.EngineerType -
            HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;
        int healerIndex = HealerAttackCommandFixNativeDefinition.BedouinHealerType -
            HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;
        Check(HealerAttackCommandFixNativeDefinition.AttackUnitCommand == 4,
            "TribeAICommand.AttackUnit remains command 4");
        Check(image[firstTable + engineerIndex] == HealerAttackCommandFixNativeDefinition.FirstNoOpClass,
            "Engineer already uses the first no-op class");
        Check(image[secondTable + engineerIndex] == HealerAttackCommandFixNativeDefinition.SecondNoOpClass,
            "Engineer already uses the second no-op class");
        Check(image[firstTable + healerIndex] == HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass,
            "Bedouin Healer starts in the first melee class");
        Check(image[secondTable + healerIndex] == HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass,
            "Bedouin Healer starts in the second melee class");
        Check(firstTable + healerIndex == HealerAttackCommandFixNativeDefinition.FirstHealerEntryRva,
            "first Bedouin Healer entry RVA");
        Check(secondTable + healerIndex == HealerAttackCommandFixNativeDefinition.SecondHealerEntryRva,
            "second Bedouin Healer entry RVA");

        Check(ReadInt32(image, firstDispatch) == HealerAttackCommandFixNativeDefinition.FirstMeleeTargetRva,
            "first class zero enters melee-group counting");
        Check(ReadInt32(image, firstDispatch +
                HealerAttackCommandFixNativeDefinition.FirstNoOpClass * sizeof(int)) ==
            HealerAttackCommandFixNativeDefinition.FirstNoOpTargetRva,
            "first replacement class enters its no-op branch");
        Check(ReadInt32(image, secondDispatch) == HealerAttackCommandFixNativeDefinition.SecondMeleeTargetRva,
            "second class zero assigns a melee-group position");
        Check(ReadInt32(image, secondDispatch +
                HealerAttackCommandFixNativeDefinition.SecondNoOpClass * sizeof(int)) ==
            HealerAttackCommandFixNativeDefinition.SecondNoOpTargetRva,
            "second replacement class enters its no-op branch");

        Check(HashSlice(image, firstTable, 81) ==
            "0C7BFCEC367534FD52395382F291EDBE8F444FB9B906205C7823DD3FC32FAE9F",
            "complete first classifier table remains canonical before patching");
        Check(HashSlice(image, secondTable, 81) ==
            "5B7439039A0725E57D8840DDF234CD59B48C2FC6CE2F35C079446CB8144D8C3E",
            "complete second classifier table remains canonical before patching");
    }

    private static void CheckLordControlGroupContracts(byte[] image)
    {
        Check(LordControlGroupNativeDefinition.LordUnitType == 0x37,
            "Lord unit type remains the audited value");
        Check(LordControlGroupNativeDefinition.EuropeanArcherUnitType == 0x16,
            "European Archer placeholder type remains the audited value");
        CheckBytes(
            image,
            LordControlGroupNativeDefinition.AddLordBranchRva,
            LordControlGroupNativeDefinition.VanillaAddLordBranch,
            "complete Add-to-control-group Lord exclusion instruction");
        CheckBytes(
            image,
            LordControlGroupNativeDefinition.ReplaceLordBranchRva,
            LordControlGroupNativeDefinition.VanillaReplaceLordBranch,
            "complete Replace-control-group Lord exclusion instruction");
        CheckRelativeConditionalJump(
            image,
            LordControlGroupNativeDefinition.AddLordBranchRva,
            0x84,
            0xCB01A,
            "Add-to-control-group Lord exclusion target");
        CheckRelativeConditionalJump(
            image,
            LordControlGroupNativeDefinition.ReplaceLordBranchRva,
            0x84,
            0xD1100,
            "Replace-control-group Lord exclusion target");

        int typeTable = ReadInt32(
            image,
            LordControlGroupNativeDefinition.SummaryClassifierPatternRva +
                LordControlGroupNativeDefinition.SummaryTypeTableDisplacementOffset);
        int dispatchTable = ReadInt32(
            image,
            LordControlGroupNativeDefinition.SummaryClassifierPatternRva +
                LordControlGroupNativeDefinition.SummaryDispatchTableDisplacementOffset);
        Check(typeTable == LordControlGroupNativeDefinition.SummaryTypeTableRva,
            "control-group summary resolves its audited unit-type table");
        Check(dispatchTable == LordControlGroupNativeDefinition.SummaryDispatchTableRva,
            "control-group summary resolves its audited dispatch table");
        Check(typeTable + LordControlGroupNativeDefinition.LordUnitType -
                LordControlGroupNativeDefinition.UnitTypeTableMinimum ==
              LordControlGroupNativeDefinition.LordSummaryEntryRva,
            "Lord summary entry RVA");
        Check(typeTable + LordControlGroupNativeDefinition.EuropeanArcherUnitType -
                LordControlGroupNativeDefinition.UnitTypeTableMinimum ==
              LordControlGroupNativeDefinition.EuropeanArcherSummaryEntryRva,
            "European Archer summary entry RVA");
        Check(image[LordControlGroupNativeDefinition.LordSummaryEntryRva] ==
              LordControlGroupNativeDefinition.VanillaUnmappedSummaryClass,
            "Vanilla Lord uses the unmapped summary class");
        Check(image[LordControlGroupNativeDefinition.EuropeanArcherSummaryEntryRva] ==
              LordControlGroupNativeDefinition.EuropeanArcherSummaryClass,
            "European Archer uses the placeholder summary class");
        Check(ReadInt32(
                image,
                dispatchTable + LordControlGroupNativeDefinition.EuropeanArcherSummaryClass * sizeof(int)) ==
              LordControlGroupNativeDefinition.EuropeanArcherSummaryTargetRva,
            "European Archer summary class dispatch target");
        Check(ReadInt32(
                image,
                dispatchTable + LordControlGroupNativeDefinition.VanillaUnmappedSummaryClass * sizeof(int)) ==
              LordControlGroupNativeDefinition.UnmappedSummaryTargetRva,
            "Vanilla unmapped summary class dispatch target");

        int storageRva = checked(
            LordControlGroupNativeDefinition.ControlGroupStoragePatternRva +
            LordControlGroupNativeDefinition.ControlGroupStorageNextInstructionOffset +
            ReadInt32(
                image,
                LordControlGroupNativeDefinition.ControlGroupStoragePatternRva +
                LordControlGroupNativeDefinition.ControlGroupStorageDisplacementOffset));
        Check(storageRva == LordControlGroupNativeDefinition.ControlGroupStorageRva,
            "control-group storage reference resolves its audited global array");
        Check(LordControlGroupNativeDefinition.ControlGroupCount == 10 &&
              LordControlGroupNativeDefinition.ControlGroupCapacity == 10000 &&
              LordControlGroupNativeDefinition.ControlGroupRecordIntCount == 2,
            "control-group storage dimensions match the audited ten-by-10000 ID/global-ID layout");
    }

    private static void CheckLordControlGroupIconPolicy()
    {
        Check(LordControlGroupIconPolicy.IsGroupMutationCommand("Add_1"),
            "control-group Add command requests a UI refresh");
        Check(LordControlGroupIconPolicy.IsGroupMutationCommand("Create_0"),
            "control-group Create command requests a UI refresh");
        Check(LordControlGroupIconPolicy.IsGroupMutationCommand("Delete_9"),
            "control-group Delete command requests a UI refresh");
        Check(!LordControlGroupIconPolicy.IsGroupMutationCommand("Select_1") &&
              !LordControlGroupIconPolicy.IsGroupMutationCommand("Add_X") &&
              !LordControlGroupIconPolicy.IsGroupMutationCommand("Add_10") &&
              !LordControlGroupIconPolicy.IsGroupMutationCommand(null),
            "non-mutating or malformed control-group commands do not request a UI refresh");

        int[] lordOnlyTypes = { 0, 0, 0, 0 };
        int[] lordOnlyCounts = { 1, 0, 0, 0 };
        LordControlGroupIconPolicy.InsertLord(lordOnlyTypes, lordOnlyCounts);
        Check(lordOnlyTypes[0] == LordControlGroupIconPolicy.LordVisualType &&
              lordOnlyCounts.SequenceEqual(new[] { 1, 0, 0, 0 }) &&
              LordControlGroupIconPolicy.CalculateExtraCount(1, lordOnlyCounts) == 0,
            "Lord-only group replaces the internal Archer bridge without changing its count");

        int[] mixedTypes = { 0, 4, 0, 0 };
        int[] mixedCounts = { 6, 4, 0, 0 };
        LordControlGroupIconPolicy.InsertLord(mixedTypes, mixedCounts);
        Check(mixedTypes[0] == 0 && mixedCounts[0] == 5 &&
              mixedTypes[2] == LordControlGroupIconPolicy.LordVisualType && mixedCounts[2] == 1 &&
              LordControlGroupIconPolicy.CalculateExtraCount(10, mixedCounts) == 0,
            "mixed Archer/Lord summary splits the Lord into a free visual slot");

        int[] macemenTypes = { 4, 0, 0, 0 };
        int[] macemenCounts = { 10, 1, 0, 0 };
        LordControlGroupIconPolicy.InsertLord(macemenTypes, macemenCounts);
        Check(macemenTypes[0] == 4 && macemenCounts[0] == 10 &&
              macemenTypes[1] == LordControlGroupIconPolicy.LordVisualType && macemenCounts[1] == 1 &&
              LordControlGroupIconPolicy.CalculateExtraCount(11, macemenCounts) == 0,
            "fresh Lord and ten Macemen summary keeps both Vanilla counts visible");

        int[] hiddenLordTypes = { 1, 2, 3, 4 };
        int[] hiddenLordCounts = { 10, 9, 8, 7 };
        LordControlGroupIconPolicy.InsertLord(hiddenLordTypes, hiddenLordCounts);
        Check(hiddenLordTypes[3] == LordControlGroupIconPolicy.LordVisualType &&
              hiddenLordCounts.SequenceEqual(new[] { 10, 9, 8, 1 }) &&
              LordControlGroupIconPolicy.CalculateExtraCount(35, hiddenLordCounts) == 7,
            "Lord hidden behind four larger classes takes the last slot and preserves displaced units in +N");

        int[] fullMixedTypes = { 1, 2, 3, 0 };
        int[] fullMixedCounts = { 10, 9, 8, 3 };
        LordControlGroupIconPolicy.InsertLord(fullMixedTypes, fullMixedCounts);
        Check(fullMixedTypes[3] == LordControlGroupIconPolicy.LordVisualType &&
              fullMixedCounts.SequenceEqual(new[] { 10, 9, 8, 1 }) &&
              LordControlGroupIconPolicy.CalculateExtraCount(30, fullMixedCounts) == 2,
            "full mixed Archer/Lord summary keeps the dedicated Lord icon and moves Archers into +N");
    }

    private static void CheckDisbandCleanupPolicyAndWiring(string workspace)
    {
        Check(ControlGroupDisbandCleanupPolicy.ShouldClean(true, true),
            "disband cleanup is active when both local switches are enabled");
        Check(!ControlGroupDisbandCleanupPolicy.ShouldClean(false, true) &&
              !ControlGroupDisbandCleanupPolicy.ShouldClean(true, false),
            "disband cleanup respects both local switches");

        int[] records =
        {
            7, 100,
            -1, 0,
            7, 200,
            8, 300,
            7, 100
        };
        Check(ControlGroupDisbandCleanupPolicy.RemoveUnit(records, 7) == 3,
            "disband cleanup removes every membership for one unit ID");
        Check(records[0] == -1 && records[4] == -1 && records[8] == -1 &&
              records[1] == 100 && records[5] == 200 && records[6] == 8,
            "disband cleanup invalidates only unit-ID fields and preserves other records");
        Check(ControlGroupDisbandCleanupPolicy.RemoveUnit(records, 7) == 0 &&
              ControlGroupDisbandCleanupPolicy.RemoveUnit(null, 7) == 0,
            "disband cleanup is idempotent and rejects missing storage");

        string runtime = File.ReadAllText(Path.Combine(
            workspace, "BugfixesAndQoL", "src", "ControlGroupDisbandCleanupRuntime.cs"));
        Check(runtime.IndexOf("original(unitManager, unitId, playSound)", StringComparison.Ordinal) <
                  runtime.IndexOf("RemoveUnitFromAllGroups(unitId)", StringComparison.Ordinal) &&
              runtime.Contains("settings.EnableClientFeatures") &&
              runtime.Contains("settings.EnableDisbandedUnitControlGroupCleanup") &&
              runtime.Contains("record[0] = -1;") &&
              runtime.Contains("NativeDetourConfig { ManualApply = true }") &&
              runtime.Contains("DisbandCallRva") &&
              runtime.Contains("DisbandFunctionRva"),
            "native cleanup calls Vanilla first, is locally gated, validates its target, and invalidates memberships");

        string viewModel = File.ReadAllText(Path.Combine(
            workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLViewModel.cs"));
        Check(viewModel.Contains("[Shared.PresetLocal]\r\n        public bool EnableDisbandedUnitControlGroupCleanup") &&
              viewModel.Contains("EnableDisbandedUnitControlGroupCleanup = true;"),
            "disband cleanup setting is preset-local and enabled by default");

        string lordFeature = File.ReadAllText(Path.Combine(
            workspace, "BugfixesAndQoL", "src", "LordUnitControlsFeature.cs"));
        Check(!lordFeature.Contains("RemoveUnitFromAllGroups") &&
              lordFeature.Contains("disbandOriginal(self, parameter);"),
            "Lord HUD disband routing remains separate from control-group cleanup");
    }

    private static void CheckMixedLordDisbandContract(byte[] image)
    {
        CheckBytes(
            image,
            LordControlGroupNativeDefinition.DisbandDispatcherRva,
            LordControlGroupNativeDefinition.DisbandDispatcherInstructions,
            "complete UIT_DISBAND unit-type dispatcher");
        CheckBytes(
            image,
            LordControlGroupNativeDefinition.DisbandBranchRva,
            LordControlGroupNativeDefinition.DisbandBranchInstructions,
            "complete normal-unit UIT_DISBAND block");
        Check(image[LordControlGroupNativeDefinition.LordDisbandClassEntryRva] ==
              LordControlGroupNativeDefinition.LordDisbandClass,
            "Lord maps to audited UIT_DISBAND class 2");
        Check(image[LordControlGroupNativeDefinition.EuropeanArcherDisbandClassEntryRva] ==
              LordControlGroupNativeDefinition.EuropeanArcherDisbandClass,
            "European Archer maps to audited UIT_DISBAND class 0");
        Check(ReadInt32(
                image,
                LordControlGroupNativeDefinition.DisbandTargetTableRva +
                    LordControlGroupNativeDefinition.LordDisbandClass * sizeof(int)) ==
              LordControlGroupNativeDefinition.DisbandDefaultTargetRva,
            "Lord UIT_DISBAND class targets the no-op/default loop path");
        Check(ReadInt32(
                image,
                LordControlGroupNativeDefinition.DisbandTargetTableRva +
                    LordControlGroupNativeDefinition.EuropeanArcherDisbandClass * sizeof(int)) ==
              LordControlGroupNativeDefinition.DisbandBranchRva,
            "European Archer UIT_DISBAND class targets the normal disband block");
        CheckCallTarget(
            image,
            LordControlGroupNativeDefinition.DisbandCallRva,
            LordControlGroupNativeDefinition.DisbandFunctionRva,
            "normal UIT_DISBAND helper call");
        Check(MatchesMixedLordDisbandContract(image),
            "canonical mixed Lord disband layout is accepted");

        byte[] changedDispatcher = (byte[])image.Clone();
        changedDispatcher[LordControlGroupNativeDefinition.DisbandDispatcherRva + 4] ^= 0x01;
        Check(!MatchesMixedLordDisbandContract(changedDispatcher),
            "changed UIT_DISBAND dispatcher is rejected");
        byte[] changedTable = (byte[])image.Clone();
        changedTable[LordControlGroupNativeDefinition.LordDisbandClassEntryRva] ^= 0x01;
        Check(!MatchesMixedLordDisbandContract(changedTable),
            "changed Lord UIT_DISBAND class is rejected");
    }

    private static bool MatchesMixedLordDisbandContract(byte[] image) =>
        BytesMatch(
            image,
            LordControlGroupNativeDefinition.DisbandDispatcherRva,
            ParseExactBytes(LordControlGroupNativeDefinition.DisbandDispatcherInstructions)) &&
        BytesMatch(
            image,
            LordControlGroupNativeDefinition.DisbandBranchRva,
            ParseExactBytes(LordControlGroupNativeDefinition.DisbandBranchInstructions)) &&
        image[LordControlGroupNativeDefinition.LordDisbandClassEntryRva] ==
            LordControlGroupNativeDefinition.LordDisbandClass &&
        image[LordControlGroupNativeDefinition.EuropeanArcherDisbandClassEntryRva] ==
            LordControlGroupNativeDefinition.EuropeanArcherDisbandClass &&
        ReadInt32(
            image,
            LordControlGroupNativeDefinition.DisbandTargetTableRva +
                LordControlGroupNativeDefinition.LordDisbandClass * sizeof(int)) ==
            LordControlGroupNativeDefinition.DisbandDefaultTargetRva &&
        ReadInt32(
            image,
            LordControlGroupNativeDefinition.DisbandTargetTableRva +
                LordControlGroupNativeDefinition.EuropeanArcherDisbandClass * sizeof(int)) ==
            LordControlGroupNativeDefinition.DisbandBranchRva;

    private static void CheckLordControlGroupUiContracts(string workspace)
    {
        string modRoot = Path.Combine(workspace, "BugfixesAndQoL");
        string iconPath = Path.Combine(
            modRoot,
            "Override",
            "Assets",
            "GUI",
            "Sprites",
            "BugfixesAndQoL-Lord.png");
        Check(File.Exists(iconPath) && Hash(File.ReadAllBytes(iconPath)) ==
              "AE353606A5F6C0F21BAD85F02BB2B2D2793ACB969572B755C11BF861484FF80E",
            "packaged Lord icon is the unchanged Vanilla chimp55_lord v3.png asset");

        string atlasPatch = File.ReadAllText(Path.Combine(
            modRoot, "Patches", "Assets", "GUI", "Sprites", "UI-MasterAtlas.xaml"));
        Check(atlasPatch.Contains("BugfixesAndQoL-Lord.png") &&
              atlasPatch.Contains("x:Key=\"BugfixesAndQoL-LordIcon\"") &&
              atlasPatch.Contains("SourceRect=\"67,42,105,179\""),
            "Lord sprite patch registers and crops the dedicated Vanilla asset");

        string troopPatch = File.ReadAllText(Path.Combine(
            modRoot, "Patches", "Assets", "GUI", "XAMLResources", "HUD_Troops.xaml"));
        Check(troopPatch.Contains("XPath=\"//n:Grid[@Name='TroopSelectionControls']\"") &&
              troopPatch.Contains("x:Name=\"BugfixesAndQoLLordSelected\"") &&
              troopPatch.Contains("Command=\"{Binding LeftClickSelectedTroopCommand}\"") &&
              troopPatch.Contains("Command=\"{Binding RightClickSelectedTroopCommand}\"") &&
              troopPatch.Contains("CommandParameter=\"CHIMP_TYPE_LORD\"") &&
              troopPatch.Contains("bugfixes:TroopHudMiddleClickBehavior.IsEnabled=\"True\"") &&
              troopPatch.Contains("<Trigger Property=\"IsMouseOver\" Value=\"True\">") &&
              troopPatch.Contains("<Setter Property=\"Opacity\" Value=\"0.72\" />") &&
              !troopPatch.Contains("ButtonTroopPanelMouseEnterCommand") &&
              !troopPatch.Contains("ButtonTroopPanelMouseLeaveCommand") &&
              troopPatch.Contains("local:PropEx.Sprite1=\"{StaticResource BugfixesAndQoL-LordIcon}\"") &&
              !troopPatch.Contains("BugfixesAndQoLLordSelectionHost") &&
              !troopPatch.Contains("BugfixesAndQoLLordHealthHost") &&
              !troopPatch.Contains("LordHealthVisibility"),
            "full troop HUD exposes one interactive Lord slot without compact or separate-health remnants");

        string lordHudFeature = File.ReadAllText(Path.Combine(
            modRoot, "src", "LordUnitControlsFeature.cs"));
        Check(lordHudFeature.IndexOf("setupSelectedTroopsOriginal(self);", StringComparison.Ordinal) <
                  lordHudFeature.IndexOf("ApplyLordAwareLayout(self);", StringComparison.Ordinal) &&
              lordHudFeature.Contains("panel.HideAllSelectedTroops();") &&
              lordHudFeature.Contains("panel.ShowSelectedTroopsNumber(slot, selectedTypeCounts[type]);") &&
              lordHudFeature.Contains("selectedTypeCounts[(int)eChimps.CHIMP_TYPE_LORD] = 1;") &&
              lordHudFeature.Contains("Enums.eTextValues.BHELP_TEXT_SELECT_LORD") &&
              lordHudFeature.Contains("lordSelectionButton.MouseEnter += OnLordSelectionMouseEnter;") &&
              lordHudFeature.Contains("lordSelectionButton.MouseLeave += OnLordSelectionMouseLeave;") &&
              lordHudFeature.Contains("ButtonTroopPanelMouseEnterHook(main, \"BugfixesAndQoLLordSelected\");") &&
              lordHudFeature.Contains("troopPanelMouseLeaveMethod.Invoke(main, new object[] { null });") &&
              lordHudFeature.IndexOf("if (!activeGameUi)", StringComparison.Ordinal) <
                  lordHudFeature.IndexOf("MainViewModel main = MainViewModel.Instance;", StringComparison.Ordinal) &&
              lordHudFeature.Contains("LordDisbandAction.RejectUnsafeMixedSelection") &&
              !lordHudFeature.Contains("CompactFrame") &&
              !lordHudFeature.Contains("ApplyCompactHud"),
            "Lord HUD hook preserves Vanilla first, shares slot counts, routes disband, and contains no compact layout");

        string groupPatch = File.ReadAllText(Path.Combine(
            modRoot, "Patches", "Assets", "GUI", "XAMLResources", "HUD_ControlGroups.xaml"));
        Check(groupPatch.Contains("x:Name=\"BugfixesAndQoLLordControlGroupIconSource\"") &&
              groupPatch.Contains("Source=\"{StaticResource BugfixesAndQoL-LordIcon}\""),
            "control-group HUD exposes the resolved Lord ImageSource to the managed hook");

        string iconFeature = File.ReadAllText(Path.Combine(
            modRoot, "src", "LordControlGroupIconFeature.cs"));
        Check(iconFeature.Contains("BindingFlags.Instance | BindingFlags.NonPublic") &&
              iconFeature.Contains("RequirePrivateField(\"RefTroopImages\"") &&
              iconFeature.Contains("RequirePrivateField(\"RefTroopValues\"") &&
              iconFeature.Contains("RequirePrivateField(\"RefTroopExtraValues\"") &&
              iconFeature.Contains("typeof(GameData)") &&
              iconFeature.Contains("\"setGameState\"") &&
              !iconFeature.Contains("panel.RefTroop"),
            "control-group UI hook validates Vanilla's HUD members and PlayState boundary");
        Check(iconFeature.IndexOf("populateOriginal(self);", StringComparison.Ordinal) <
                   iconFeature.IndexOf("ApplyLordIcons(self);", StringComparison.Ordinal) &&
              iconFeature.Contains("record[0] == lordUnitId && record[1] == lordGlobalId") &&
              iconFeature.Contains("active = false;") && iconFeature.Contains("!active"),
            "control-group UI hook preserves Vanilla first, identifies the Lord exactly, and gates partial teardown");
        Check(iconFeature.Contains("buttonClickedOriginal(self, command);") &&
              iconFeature.Contains("IsGroupMutationCommand(command)") &&
              iconFeature.Contains("pendingRefreshPanel = self;") &&
              iconFeature.Contains("hasObservedPatchedGameState = true;") &&
              iconFeature.Contains("if (!hasObservedPatchedGameState ||") &&
              !iconFeature.Contains("RefreshPanel(self);") &&
              !iconFeature.Contains("Application.onBeforeRender") &&
              iconFeature.IndexOf("setGameStateOriginal(self, gameState);", StringComparison.Ordinal) <
                  iconFeature.IndexOf("RefreshPanel(panel);", StringComparison.Ordinal) &&
              iconFeature.Contains("ClearPendingRefresh();") &&
              iconFeature.Contains("TryDisposeHook(ref setGameStateHook"),
            "control-group mutations wait for a fresh Vanilla PlayState and coalesce one refresh with teardown");
        Check(iconFeature.Contains("NativeGroupContainsLord(group, lordUnitId, lordGlobalId)") &&
              iconFeature.Contains("types[slot] = state.control_groups_type[summaryOffset + slot]") &&
              iconFeature.Contains("counts[slot] = state.control_groups_count[summaryOffset + slot]") &&
              !iconFeature.Contains("NativeGroupSnapshot") &&
              !iconFeature.Contains("summaryAlreadyIncludesLord") &&
              !iconFeature.Contains("HideGroup(") &&
              !iconFeature.Contains("PropEx.SetButtonVisibility"),
            "control-group UI keeps Vanilla summaries authoritative and only adds the exact Lord icon");
    }

    private static void CheckLordControlGroupTransactionModel(byte[] canonicalImage)
    {
        byte[] addOriginal = ParseExactBytes(LordControlGroupNativeDefinition.VanillaAddLordBranch);
        byte[] replaceOriginal = ParseExactBytes(LordControlGroupNativeDefinition.VanillaReplaceLordBranch);
        byte[] bypass = ParseExactBytes(LordControlGroupNativeDefinition.BypassLordBranch);
        var sites = new[]
        {
            new BytePatch(
                LordControlGroupNativeDefinition.AddLordBranchRva,
                addOriginal,
                bypass),
            new BytePatch(
                LordControlGroupNativeDefinition.ReplaceLordBranchRva,
                replaceOriginal,
                bypass),
            new BytePatch(
                LordControlGroupNativeDefinition.LordSummaryEntryRva,
                new[] { LordControlGroupNativeDefinition.VanillaUnmappedSummaryClass },
                new[] { LordControlGroupNativeDefinition.EuropeanArcherSummaryClass })
        };

        byte[] applied = (byte[])canonicalImage.Clone();
        Check(TryApplyTransaction(applied, sites),
            "canonical Lord control-group layout applies transactionally");
        foreach (BytePatch site in sites)
            Check(BytesMatch(applied, site.Rva, site.Replacement), $"Lord patch site 0x{site.Rva:X} applied");
        RollbackTransaction(applied, sites);
        foreach (BytePatch site in sites)
            Check(BytesMatch(applied, site.Rva, site.Original), $"Lord patch site 0x{site.Rva:X} rolled back");

        byte[] unknownLayout = (byte[])canonicalImage.Clone();
        unknownLayout[LordControlGroupNativeDefinition.ReplaceLordBranchRva + 1] ^= 0x01;
        byte[] unknownBefore = (byte[])unknownLayout.Clone();
        Check(!TryApplyTransaction(unknownLayout, sites),
            "unknown Lord control-group binary layout is rejected");
        Check(unknownLayout.SequenceEqual(unknownBefore),
            "unknown layout rejection makes no partial changes");

        byte[] partiallyPatched = (byte[])canonicalImage.Clone();
        Buffer.BlockCopy(
            bypass,
            0,
            partiallyPatched,
            LordControlGroupNativeDefinition.AddLordBranchRva,
            bypass.Length);
        byte[] partialBefore = (byte[])partiallyPatched.Clone();
        Check(!TryApplyTransaction(partiallyPatched, sites),
            "partially changed Lord control-group layout is rejected");
        Check(partiallyPatched.SequenceEqual(partialBefore),
            "partial-layout rejection does not change remaining sites");
    }

    private static bool TryApplyTransaction(byte[] image, BytePatch[] sites)
    {
        if (sites.Any(site => !BytesMatch(image, site.Rva, site.Original)))
            return false;
        foreach (BytePatch site in sites)
            Buffer.BlockCopy(site.Replacement, 0, image, site.Rva, site.Replacement.Length);
        return true;
    }

    private static void RollbackTransaction(byte[] image, BytePatch[] sites)
    {
        for (int i = sites.Length - 1; i >= 0; i--)
        {
            BytePatch site = sites[i];
            Check(BytesMatch(image, site.Rva, site.Replacement),
                $"Lord rollback owns patch site 0x{site.Rva:X}");
            Buffer.BlockCopy(site.Original, 0, image, site.Rva, site.Original.Length);
        }
    }

    private static bool BytesMatch(byte[] image, int rva, byte[] expected)
    {
        if (rva < 0 || rva > image.Length - expected.Length)
            return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (image[rva + i] != expected[i])
                return false;
        }
        return true;
    }

    private static byte[] ParseExactBytes(string value) =>
        value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => Convert.ToByte(token, 16))
            .ToArray();

    private static void CheckUnknownHashPolicy(string workspace)
    {
        string plague = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "PlagueNativePatternValidator.cs"));
        string recruitment = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "AiRecruitmentHorseDemandFix.cs"));
        string mountedStockpile = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "MountedStockpileMovementPatch.cs"));
        string healerAttackCommand = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "HealerAttackCommandPatch.cs"));
        string lordControlGroups = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "LordControlGroupNativePatch.cs"));
        string runtime = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLRuntime.cs"));
        Check(plague.Contains("if (!referenceHashMatches)"), "plague fixed-layout unknown-hash gate");
        Check(recruitment.Contains("if (!referenceHashMatches)"), "AI recruitment result-layout unknown-hash gate");
        Check(mountedStockpile.Contains("if (!referenceHashMatches)"), "mounted-stockpile unknown-hash gate");
        Check(healerAttackCommand.Contains("if (!referenceHashMatches)"),
            "Healer attack-command unknown-hash gate");
        Check(lordControlGroups.Contains("if (!referenceHashMatches)"),
            "Lord control-group unknown-hash gate");
        Check(lordControlGroups.Contains("addBranch.ValidateOriginal()") &&
              lordControlGroups.Contains("replaceBranch.ValidateOriginal()") &&
              lordControlGroups.Contains("lordSummaryEntry.ValidateOriginal()") &&
              lordControlGroups.IndexOf("lordSummaryEntry.ValidateOriginal()", StringComparison.Ordinal) <
                  lordControlGroups.IndexOf("addBranch.Apply()", StringComparison.Ordinal),
            "Lord control-group transaction validates all three sites before applying any site");
        Check(lordControlGroups.Contains("RestoreSite(lordSummaryEntry") &&
              lordControlGroups.Contains("RestoreSite(replaceBranch") &&
              lordControlGroups.Contains("RestoreSite(addBranch") &&
              lordControlGroups.Contains("applied = CurrentBytesMatch(replacement)"),
            "Lord control-group transaction rolls back all sites in reverse order, including late write failures");
        Check(runtime.Contains("settings.EnableMod && settings.EnableLordUnitControls") &&
              runtime.Contains("DisableLordControlGroupNativePatch()"),
            "Lord control-group patch follows the existing synchronized Lord-control setting reversibly");
        Check(healerAttackCommand.Contains("FindUniquePattern") &&
              healerAttackCommand.Contains("ReadAbsoluteTableRva") &&
              healerAttackCommand.Contains("ValidateDispatchTargets"),
            "Healer attack-command derives and validates both tables from unique code signatures");
        Check(healerAttackCommand.Contains("firstHealerEntry") &&
              healerAttackCommand.Contains("secondHealerEntry"),
            "Healer attack-command changes both audited table entries");
        Check(!healerAttackCommand.Contains("X64InlineHook") &&
              !healerAttackCommand.Contains("AddDetour") &&
              !healerAttackCommand.Contains("OnTick") &&
              !healerAttackCommand.Contains("OnStartMap"),
            "Healer attack-command uses no hook or recurring diagnostics");
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

    private static void CheckRelativeConditionalJump(
        byte[] image,
        int jumpRva,
        byte conditionOpcode,
        int expectedTargetRva,
        string label)
    {
        Check(jumpRva >= 0 && jumpRva <= image.Length - 6, label + " bounds");
        Check(image[jumpRva] == 0x0F && image[jumpRva + 1] == conditionOpcode,
            label + " opcode");
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

    private static string HashSlice(byte[] bytes, int offset, int length)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes, offset, length)).Replace("-", string.Empty);
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

    private readonly struct BytePatch
    {
        public BytePatch(int rva, byte[] original, byte[] replacement)
        {
            Rva = rva;
            Original = original;
            Replacement = replacement;
        }

        public int Rva { get; }
        public byte[] Original { get; }
        public byte[] Replacement { get; }
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
