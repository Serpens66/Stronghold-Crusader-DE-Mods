using BugfixesAndQoL;
using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Linq;
using System.Reflection;
using Steamworks;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new Exception(description);
    }

    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, name);
    }

    private static bool Resolve(bool vanilla, FRONT_Multiplayer self) => vanilla;

    private static void Expect(string name, bool expected, bool vanilla = false, bool enabled = true,
        bool skirmish = false, bool coop = false, bool host = true, bool ready = true,
        bool valid = true, int members = 2, int maximum = 8, int humans = 1)
    {
        Check(LobbySaveLoadingPolicy.ResolveSoloLoad(vanilla, enabled, skirmish, coop, host, ready,
            valid, members, maximum, humans) == expected, name);
    }

    private static int Main()
    {
        try
        {
            Expect("solo host with AI", true);
            Expect("solo host without AI", true, members: 1);
            Expect("solo host fills capacity", true, members: 8);
            Expect("feature / mod disabled", false, enabled: false);
            Expect("singleplayer untouched", false, skirmish: true);
            Expect("Coop untouched", false, coop: true);
            Expect("client untouched", false, host: false);
            Expect("not ready", false, ready: false);
            Expect("unresolved roster", false, valid: false);
            Expect("no host", false, humans: 0);
            Expect("two humans retain Vanilla AI prohibition", false, humans: 2);
            Expect("invalid count", false, members: 0);
            Expect("over capacity", false, members: 9);
            Expect("missing capacity", false, maximum: 0);
            Expect("Vanilla true survives all rejected conditions", true, vanilla: true,
                enabled: false, skirmish: true, coop: true, host: false, ready: false, valid: false,
                members: 0, maximum: 0, humans: 0);
            var self = new Platform_Multiplayer.MPLobbyMember { id = new CSteamID(2001), ready = true };
            var ai = new Platform_Multiplayer.MPLobbyMember { id = new CSteamID(2), ready = false, SkirmishMember = true };
            var other = new Platform_Multiplayer.MPLobbyMember { id = new CSteamID(2002), ready = true };
            Check(LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self }, 1, 2001), "resolved solo roster");
            Check(LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, ai }, 2, 2001), "AI needs no human readiness");
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, other }, 2, 2001), "remote human blocks extension");
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self }, 1, 2002), "local identity absent");
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self }, 2, 2001), "stale lobby count");
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(null, 1, 2001), "missing roster");
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, self }, 2, 2001), "duplicate human identity");
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, (Platform_Multiplayer.MPLobbyMember)null }, 2, 2001), "missing member");
            self.ready = false;
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self }, 1, 2001), "member not ready");
            self.ready = true; ai.dummyToBeKicked = true;
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, ai }, 2, 2001), "pending member removal");
            ai.dummyToBeKicked = false;
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, new Platform_Multiplayer.MPLobbyMember() }, 2, 2001), "unresolved member is not AI");
            self.SkirmishMember = true; self.SkirmishHumanMember = true;
            Check(!LobbySaveLoadingPolicy.IsSoloReadyRoster(new[] { self, ai }, 2, 2001), "local Skirmish human not a Steam participant");
            self.SkirmishMember = false; self.SkirmishHumanMember = false;
            Check(LobbySaveLoadingPolicy.IsNormalMultiplayer(false, false, false), "normal multiplayer accepted");
            Check(!LobbySaveLoadingPolicy.IsNormalMultiplayer(true, false, false), "Skirmish excluded");
            Check(!LobbySaveLoadingPolicy.IsNormalMultiplayer(false, true, false), "Coop trail excluded");
            Check(!LobbySaveLoadingPolicy.IsNormalMultiplayer(false, false, true), "custom Coop independently excluded");
            Check(LobbySaveLoadingPolicy.IsNormalLobby(3001, "0", false, false, false, false, false), "normal Steam lobby");
            Check(!LobbySaveLoadingPolicy.IsNormalLobby(0, "0", false, false, false, false, false), "synthetic local lobby");
            foreach (string category in new[] { null, "", "1", "2" })
                Check(!LobbySaveLoadingPolicy.IsNormalLobby(3001, category, false, false, false, false, false), "unknown / Coop category");
            Check(!LobbySaveLoadingPolicy.IsNormalLobby(3001, "0", true, false, false, false, false), "Coop lobby with transient frontend flag");
            Check(!LobbySaveLoadingPolicy.IsNormalLobby(3001, "0", false, true, false, false, false), "local Coop");
            Check(!LobbySaveLoadingPolicy.IsNormalLobby(3001, "0", false, false, true, false, false), "Trail maker test");
            Check(!LobbySaveLoadingPolicy.IsNormalLobby(3001, "0", false, false, false, true, false), "customized Trail");
            Check(!LobbySaveLoadingPolicy.IsNormalLobby(3001, "0", false, false, false, false, true), "Extreme Trail customization");
            // Inspect the real callback without executing FRONT_Multiplayer's native Noesis static initializer.
            MethodInfo callback = typeof(LobbySaveLoadingFeature).GetMethod("ResolveSoloLoad", BindingFlags.Static | BindingFlags.NonPublic);
            using (var definition = new DynamicMethodDefinition(callback))
            {
                var fields = definition.Definition.Body.Instructions.Select(x => x.Operand as Mono.Cecil.FieldReference)
                    .Where(x => x != null && x.DeclaringType.FullName == typeof(FRONT_Multiplayer).FullName).Select(x => x.Name).ToArray();
                Check(fields.Contains("skirmishGame") && fields.Contains("coopGame") && fields.Contains("customCoopGame"),
                    "real callback supplies all three mode fields");
            }
            foreach (bool enabled in new[] { false, true })
            foreach (bool skirmish in new[] { false, true })
            foreach (bool coop in new[] { false, true })
                Check(LobbySaveLoadingPolicy.ShouldRestoreVisibility(enabled, skirmish, coop) ==
                    (enabled && skirmish && !coop), "visibility transitions / disabled settings");

            Check(LobbySaveLoadingFeature.RequireField("MPLocalReady", typeof(bool)).IsPrivate, "ready access");
            Check(LobbySaveLoadingFeature.RequireField("RefLoadButton", typeof(Noesis.Button)).IsPrivate, "button access");
            Check(LobbySaveLoadingFeature.RequireMethod("ShowSetupScreen", BindingFlags.NonPublic).IsPrivate, "setup signature");
            MethodInfo update = LobbySaveLoadingFeature.RequireMethod("Update", BindingFlags.Public);
            foreach (string name in new[] { "skirmishGame", "coopGame", "customCoopGame", "customizedTrail" })
            {
                FieldInfo field = typeof(FRONT_Multiplayer).GetField(name, BindingFlags.Public | BindingFlags.Static);
                Check(field != null && field.FieldType == typeof(bool), "public static mode field: " + name);
            }
            foreach (string name in new[] { "singlePlayerCoop", "trailMakerMode", "extremeTrailCustomised", "panelActive" })
            {
                FieldInfo field = typeof(FRONT_Multiplayer).GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Check(field != null && field.FieldType == typeof(bool), "public instance mode field: " + name);
            }
            Check(typeof(Platform_Multiplayer.MPLobby).GetField("coopTrailGame").FieldType == typeof(bool), "lobby Coop trail field");
            // Actual diagnostic wrappers must swallow logger failures independently of game state.
            Shared.DebugLogHelper.ThrowOnWrite = true;
            typeof(LobbySaveLoadingFeature).GetMethod("Info", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "test" });
            typeof(LobbySaveLoadingFeature).GetMethod("Error", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "test", new Exception("test") });
            Shared.DebugLogHelper.ThrowOnWrite = false;
            Check(true, "diagnostics cannot escape into the frontend");
            Console.WriteLine("IL fixture: " + update.DeclaringType + " " + update.Module.FullyQualifiedName +
                " token=" + update.MetadataToken.ToString("X") + " body=" + (update.GetMethodBody() != null));
            foreach (bool extenderPatched in new[] { false, true })
            using (var definition = new DynamicMethodDefinition(update))
            using (var context = new ILContext(definition.Definition))
            {
                context.ReferenceBag = RuntimeILReferenceBag.Instance;
                if (extenderPatched)
                {
                    // Same manipulator as the installed SE FRONT_Multiplayer_Update_ILHook.
                    var cursor = new ILCursor(context);
                    Check(cursor.TryGotoNext(MoveType.After, x => x.Match(OpCodes.Sub),
                        x => x.Match(OpCodes.Ldloc_S), x => x.Match(OpCodes.Bne_Un_S),
                        x => x.Match(OpCodes.Ldc_I4_0), x => x.Match(OpCodes.Stloc_S)), "Extender start patch match");
                    cursor.Index -= 2;
                    cursor.Remove();
                    cursor.Emit(OpCodes.Ldc_I4_1);
                }
                int setterIndex = LobbySaveLoadingIl.FindSetter(context);
                var setter = context.Body.Instructions[setterIndex];
                var before = context.Body.Instructions.ToArray();
                var oldOperations = before.Select(x => x.OpCode).ToArray();
                var oldOperands = before.Select(x => x.Operand).ToArray();
                var field = context.Body.Instructions[setterIndex - 10];
                object savedField = field.Operand;
                field.Operand = null;
                Reject(() => LobbySaveLoadingIl.FindSetter(context), "missing site is fail-closed");
                field.Operand = savedField;
                var boolOr = context.Body.Instructions[setterIndex - 1];
                boolOr.OpCode = OpCodes.And;
                Reject(() => LobbySaveLoadingIl.FindSetter(context), "changed Boolean rejected");
                boolOr.OpCode = OpCodes.Or;
                var branch = Instruction.Create(OpCodes.Br, setter);
                context.Body.Instructions.Insert(0, branch);
                Reject(() => LobbySaveLoadingIl.FindSetter(context), "incoming setter branch rejected");
                context.Body.Instructions.Remove(branch);
                LobbySaveLoadingIl.Patch(context, Resolve);
                Check(context.Body.Instructions.Count > before.Length, "callback inserted");
                for (int i = 0; i < before.Length; i++)
                    Check(context.Body.Instructions.Contains(before[i]) && before[i].OpCode == oldOperations[i] &&
                        ReferenceEquals(before[i].Operand, oldOperands[i]), "all original instructions preserved");
                Reject(() => LobbySaveLoadingIl.FindSetter(context), "double patch rejected");
                Check(definition.Generate() != null, "complete edited Update compiles");
            }
            Console.WriteLine("PASS: Lobby save loading: " + checks + " policy, interop and actual Vanilla/SE IL checks. Game UI/loading not executed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

// Tests compile the actual hook sources; Noesis/native game initialization is deliberately not invoked.
namespace BugfixesAndQoL
{
    internal sealed class BugfixesAndQoLViewModel
    {
        public bool EnableMod = true, EnableSoloMultiplayerSaveLoading = true, FixSkirmishLoadButtonVisibility = true;
    }
}
namespace Shared
{
    internal static class DebugLogHelper
    {
        public static bool ThrowOnWrite;
        public static void LogInfo(BepInEx.Logging.ManualLogSource log, string text)
        { if (ThrowOnWrite) throw new Exception("logger unavailable"); }
        public static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string key, params object[] text)
        { if (ThrowOnWrite) throw new Exception("logger unavailable"); }
    }
}
