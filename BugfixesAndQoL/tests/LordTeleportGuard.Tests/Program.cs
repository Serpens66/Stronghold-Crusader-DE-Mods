using APIShared.Units;
using BugfixesAndQoL.LordTeleportGuard;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static unsafe class Program
{
    private static int checks;
    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
        checks++;
    }

    private static int Main(string[] args)
    {
        try
        {
            Check(IntPtr.Size == 8, "x64 policy fixture");
            LordTeleportPolicy.ValidateLayouts();
            if (args.Length > 1) CheckArtifact(args[1]);
            if (args.Length > 0) CheckNativePolicyEvidence(File.ReadAllBytes(args[0]));
            CheckActivation();
            CheckDecisions();
            CheckIdentity();
            Console.WriteLine("PASS: actual BQoL Lord relocation policy, human/home and standing-origin exceptions, identity/activation/anchor checks=" + checks + "; shared native entry/ABI tested by APIShared.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static bool Decide(UnitRelocationOrigin origin = UnitRelocationOrigin.Native,
        eChimps type = eChimps.CHIMP_TYPE_LORD, bool alive = true, int owner = 1, bool ai = false,
        int targetX = 400, int targetY = 300) =>
        LordTeleportPolicy.ShouldCancel(origin, type, alive, owner, ai, 400, 300, targetX, targetY);

    private static void CheckDecisions()
    {
        for (int owner = 1; owner <= 8; owner++)
        {
            Check(Decide(owner: owner), "human native home fallback blocked for owner " + owner);
            Check(Decide(UnitRelocationOrigin.ExplicitApiRequest, owner: owner), "explicit request still obeys Lord policy " + owner);
            Check(!Decide(UnitRelocationOrigin.InvalidStandingTerrainRecovery, owner: owner), "standing terrain recovery is retained " + owner);
            Check(!Decide(owner: owner, ai: true), "AI home fallback retained " + owner);
            Check(!Decide(UnitRelocationOrigin.InvalidStandingTerrainRecovery, owner: owner, ai: true), "AI standing recovery retained " + owner);
        }
        Check(!Decide(targetX: 401), "other X passes");
        Check(!Decide(targetY: 301), "other Y passes");
        Check(!Decide(targetX: 401, targetY: 301), "other destination passes");
        Check(!Decide(type: eChimps.CHIMP_TYPE_SWORDSMAN), "other live type passes");
        Check(!Decide(type: eChimps.CHIMP_TYPE_PEASANT), "Peasant passes");
        Check(!Decide(alive: false), "not living passes");
        Check(!Decide(owner: 0), "neutral owner passes");
        Check(!Decide(owner: -1), "negative owner passes");
        Check(!Decide(owner: 9), "past final owner passes");
        Check(!Decide(UnitRelocationOrigin.InvalidStandingTerrainRecovery, targetX: 401), "standing exception does not require home match");
    }

    private static void CheckActivation()
    {
        Check(!LordTeleportPolicy.ShouldEnable(false, false, false), "unavailable OFF");
        Check(!LordTeleportPolicy.ShouldEnable(false, true, true), "unavailable cannot activate");
        Check(!LordTeleportPolicy.ShouldEnable(true, false, true), "master OFF");
        Check(!LordTeleportPolicy.ShouldEnable(true, true, false), "option OFF");
        Check(LordTeleportPolicy.ShouldEnable(true, true, true), "master and option ON");
    }

    private static void CheckIdentity()
    {
        GameUnit unit = default;
        unit.r_AliveState = AliveState.IsAlive; unit.r_GlobalId = 123;
        unit.r_UnitChimp = eChimps.CHIMP_TYPE_LORD; unit.r_ControllableForPlayerId = 1;
        Check(LordTeleportPolicy.MatchesIdentity(&unit, 1, 123, eChimps.CHIMP_TYPE_LORD, 1), "original living identity");
        Check(LordTeleportPolicy.MatchesIdentity(&unit, GameUnitManager.LastLiveUnitId, 123, eChimps.CHIMP_TYPE_LORD, 1), "last live slot");
        Check(!LordTeleportPolicy.MatchesIdentity(&unit, 0, 123, eChimps.CHIMP_TYPE_LORD, 1), "reserved slot");
        Check(!LordTeleportPolicy.MatchesIdentity(&unit, GameUnitManager.NativeUnitSlotCount, 123, eChimps.CHIMP_TYPE_LORD, 1), "outside live slots");
        Check(!LordTeleportPolicy.MatchesIdentity(&unit, 1, 0, eChimps.CHIMP_TYPE_LORD, 1), "empty global");
        Check(!LordTeleportPolicy.MatchesIdentity(&unit, 1, 124, eChimps.CHIMP_TYPE_LORD, 1), "reused slot");
        Check(!LordTeleportPolicy.MatchesIdentity(&unit, 1, 123, eChimps.CHIMP_TYPE_SWORDSMAN, 1), "changed type");
        Check(!LordTeleportPolicy.MatchesIdentity(&unit, 1, 123, eChimps.CHIMP_TYPE_LORD, 2), "changed owner");
        Check(!LordTeleportPolicy.MatchesIdentity(null, 1, 123, eChimps.CHIMP_TYPE_LORD, 1), "missing pointer");
        foreach (AliveState state in new[] { AliveState.None, AliveState.NeedsInit })
        {
            unit.r_AliveState = state;
            Check(!LordTeleportPolicy.MatchesIdentity(&unit, 1, 123, eChimps.CHIMP_TYPE_LORD, 1), "nonliving state " + state);
        }
    }

    private static void CheckNativePolicyEvidence(byte[] file)
    {
        using (var sha = SHA256.Create())
            Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == LordTeleportPolicy.NativeHash, "canonical native policy identity");
        const ulong image = 0x180000000UL;
        var memory = new byte[LordTeleportPolicy.AiLineUpAnchorRva + 7];
        Array.Copy(file, Raw(file, LordTeleportPolicy.AiLineUpAnchorRva), memory, LordTeleportPolicy.AiLineUpAnchorRva, 7);
        LordTeleportPolicy.ValidateAiLineUpAnchor(memory, image, image + LordTeleportPolicy.AiLineUpRva);
        Check(true, "actual native AI-lineup anchor");
        // REX.B is ignored for this RIP-relative operand; changing it does not
        // change the anchor's meaning. Corrupt the displacement instead.
        memory[LordTeleportPolicy.AiLineUpAnchorRva + 3] ^= 1;
        bool rejected = false;
        try { LordTeleportPolicy.ValidateAiLineUpAnchor(memory, image, image + LordTeleportPolicy.AiLineUpRva); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "changed AI-lineup anchor rejected");
        rejected = false;
        try { LordTeleportPolicy.ValidateAiLineUpAnchor(memory, image, image + LordTeleportPolicy.AiLineUpRva + 4); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "different public SE lineup binding rejected");
    }

    private static int Raw(byte[] file, int rva)
    {
        int pe = BitConverter.ToInt32(file, 0x3C), section = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
        for (int n = 0; n < BitConverter.ToUInt16(file, pe + 6); n++)
        {
            int header = section + n * 40, start = BitConverter.ToInt32(file, header + 12), length = BitConverter.ToInt32(file, header + 16);
            if (rva >= start && rva < start + length) return BitConverter.ToInt32(file, header + 20) + rva - start;
        }
        throw new InvalidOperationException("RVA has no raw bytes.");
    }

    private static void CheckArtifact(string path)
    {
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(path))
        {
            Check(assembly.Name.Name == "BugfixesAndQoL", "integrated artifact identity");
            bool marker = assembly.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute" &&
                (string)a.ConstructorArguments[0].Value == "SerpDevDiagnostics" && (string)a.ConstructorArguments[1].Value == "true");
            bool runtime = assembly.MainModule.Types.Any(t => t.FullName == "Shared.DevDiagRuntime");
#if SERP_DEVDIAG
            Check(marker && runtime, "diagnostic artifact carries release-rejection markers");
#else
            Check(!marker && !runtime, "normal artifact contains no development runtime");
#endif
            var plugin = assembly.MainModule.Types.Single(t => t.BaseType?.FullName == "BepInEx.BaseUnityPlugin");
            var identity = plugin.CustomAttributes.Single(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
            Check((string)identity.ConstructorArguments[0].Value == "BugfixesAndQoL_Serp" &&
                (string)identity.ConstructorArguments[2].Value + ".0" == assembly.Name.Version.ToString(), "compiled plugin and assembly versions agree");
            Check(plugin.CustomAttributes.Any(a => a.AttributeType.FullName == "BepInEx.BepInIncompatibility" &&
                (string)a.ConstructorArguments[0].Value == "LordTeleportGuardTest_Serp"), "old testmod explicitly incompatible");
            var lord = assembly.MainModule.Types.Single(t => t.FullName == "BugfixesAndQoL.LordTeleportGuard.LordTeleportGuardRuntime");
            var sharedCalls = lord.Methods.Where(m => m.IsConstructor).SelectMany(m => m.Body.Instructions)
                .Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>().Where(m =>
                    m.DeclaringType.FullName == "APIShared.Units.UnitRelocationService").Select(m => m.Name).ToArray();
            Check(sharedCalls.Contains("EnsureAvailable") && sharedCalls.Contains("TryRegisterPolicy"),
                "compiled Lord feature registers its rule through the shared relocation service");
            var model = assembly.MainModule.Types.Single(t => t.Name == "BugfixesAndQoLViewModel");
            var option = model.Properties.Single(p => p.Name == "EnableLordReturnTeleportFix");
            Check(option.CustomAttributes.Any(a => a.AttributeType.Name == "SyncHostOnlyAttribute"), "production setting is host synchronized");
            Check(option.SetMethod.Body.Instructions.Any(i => i.Operand is Mono.Cecil.MethodReference m && m.Name == "SetSetting"), "production setter uses central access/preset path");
            Check(model.Methods.Where(m => m.IsConstructor && !m.IsStatic).Any(m => m.Body.Instructions.Any(i =>
                i.Operand is Mono.Cecil.FieldReference f && f.Name == "enableLordReturnTeleportFix" &&
                i.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && i.Previous.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_1)), "compiled production initializer defaults to true");
            Check(model.Methods.Single(m => m.Name == "ResetToDefault").Body.Instructions.Any(i =>
                i.Operand is Mono.Cecil.MethodReference m && m.Name == "set_EnableLordReturnTeleportFix" &&
                i.Previous.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_1), "compiled reset assigns true");
            Check(!plugin.Methods.Any(m => new[] { "Update", "LateUpdate", "FixedUpdate", "OnDestroy", "OnDisable" }.Contains(m.Name)), "plugin has no short-lived worker or teardown");
            Check(!assembly.MainModule.AssemblyReferences.Any(r => new[] { "Newtonsoft.Json", "System.Text.Json", "System.Web.Extensions", "BugfixesAndQoL" }.Contains(r.Name)), "standalone dependency boundary");
        }
    }

}
